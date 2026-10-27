using System;
using System.Globalization;
using Incheol.Modules;
using Incheol.Utils;
using UnityEngine;
using AddressableAssetKey = Incheol.Models.Define.AddressableAssetKey;
using DeviceType = Incheol.Models.Define.DeviceType;

/// <summary>
/// 선택된 Container의 SimulationDataModel.stepList를 contentRect 아래에 SimulationItem 줄로 보여주고, 줄의 삭제 버튼을 처리한다.
/// 줄 프리팹은 Addressable(SimulationItem)로 불러온다. 입력 / 저장 / 실행은 알지 못한다(UI_SimulationControl이 맡는다).
/// </summary>
public class UI_SimulationStepList
{
    private readonly RectTransform contentRect;
    private readonly Action onStepRemoved;

    private DeviceSelection selection;

    // 프리팹 로드를 기다리는 사이 목록이 다시 그려지면, 이전 요청의 콜백이 줄을 중복으로 만들지 않도록 구분한다.
    private int itemVersion;

    /// <param name="_contentRect">줄을 만들 ScrollView의 Content</param>
    /// <param name="_onStepRemoved">삭제 버튼으로 stepList가 바뀌고 목록을 다시 그린 뒤 호출된다(startButton 상태 갱신 등).</param>
    public UI_SimulationStepList(RectTransform _contentRect, Action _onStepRemoved)
    {
        contentRect = _contentRect;
        onStepRemoved = _onStepRemoved;
    }

    /// <summary>
    /// _selection의 Container 오브젝트에 SimulationDataModel이 있고 stepList가 1개 이상이면, stepList 순서대로 줄을 만든다.
    /// 그 외에는 줄을 모두 지운다.
    /// </summary>
    public void Refresh(DeviceSelection _selection)
    {
        selection = _selection;

        if (contentRect == null)
        {
            return;
        }

        int version = ++itemVersion;
        ClearItems();

        if (selection == null || !selection.HasSelection || selection.Kind != DeviceType.Container)
        {
            return;
        }

        SimulationData holder = selection.Target.GetComponent<SimulationData>();
        if (holder == null || holder.Model.StepCount < 1)
        {
            return;
        }

        SimulationDataModel data = holder.Model;
        string key = AddressableAssetKey.SimulationItem.ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab => CreateItems(prefab, data, version),
            () => DebugLogManager.GenerateErrorMessage<UI_SimulationStepList>($"{key} 로드 실패"));
    }

    /// <summary>
    /// 줄을 모두 지운다. Destroy는 프레임 끝에 처리되므로 먼저 contentRect에서 떼어, 바로 이어서 새 줄을 만들어도
    /// 옛 줄이 레이아웃에 남아 새 줄과 섞이거나 순서가 어긋나지 않게 한다.
    /// </summary>
    private void ClearItems()
    {
        for (int i = contentRect.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRect.GetChild(i);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private void CreateItems(GameObject _prefab, SimulationDataModel _data, int _version)
    {
        // 로드를 기다리는 사이 뷰가 파괴되었거나 목록이 다시 그려졌으면 이 요청은 버린다.
        if (contentRect == null || _version != itemVersion)
        {
            return;
        }

        for (int i = 0; i < _data.stepList.Count; i++)
        {
            SimulationStepModel step = _data.stepList[i];
            GameObject itemObject = AddressableAssetManager.Instance.InstantiatePrefab(_prefab, contentRect);
            itemObject.name = $"SimulationItem_{i}";
            itemObject.GetComponent<UI_SimulationItem>().Setup(
                $"{i + 1}) {step.simulationType} ({step.duringTime.ToString(CultureInfo.InvariantCulture)})",
                () => OnDeleteStep(_data, step));
        }
    }

    /// <summary>
    /// 삭제 버튼을 눌렀을 때: _step을 stepList에서 지워 뒤의 작업을 앞으로 당겨 순서를 다시 정렬하고, 그 순서대로 줄을 다시 만든다(번호도 새로 매겨진다).
    /// 이미 지워졌거나(같은 프레임에 중복 클릭) 다른 항목으로 선택이 바뀐 뒤의 오래된 줄이 눌린 경우에는 아무 것도 지우지 않고 현재 상태로 목록만 다시 그린다.
    /// </summary>
    private void OnDeleteStep(SimulationDataModel _data, SimulationStepModel _step)
    {
        bool isCurrent = selection != null && selection.HasSelection && selection.Target != null
            && selection.Target.TryGetComponent(out SimulationData holder) && holder.Model == _data;

        if (!isCurrent || !_data.RemoveStep(_step))
        {
            DebugLogManager.GenerateWarningMessage<UI_SimulationStepList>("이미 지워졌거나 선택이 바뀐 작업이라 삭제하지 않고 목록만 새로 그렸습니다.");
        }

        Refresh(selection);
        onStepRemoved?.Invoke();
    }
}
