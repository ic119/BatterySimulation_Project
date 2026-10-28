using System.Collections.Generic;
using Incheol.Modules;
using Incheol.Utils;
using UnityEngine;
using AddressableAssetKey = Incheol.Models.Define.AddressableAssetKey;

/// <summary>
/// 시뮬레이션 작업이 끝날 때마다 그 결과를 ResultListItem 줄로 contentRect 아래에 쌓아 보여준다.
/// 줄 프리팹은 Addressable(ResultListItem)로 불러온다. 결과를 만드는 일은 알지 못하고, 받은 결과를 보여주기만 한다.
/// </summary>
public class UI_SimulationResultView : MonoBehaviour
{
    #region Variable
    [Header("Simulation Result Variable")]
    [SerializeField] private RectTransform contentRect;

    private GameObject itemPrefab;
    private bool isLoadingPrefab;

    // 프리팹을 불러오는 동안 들어온 결과는 순서대로 모아 두었다가, 로드가 끝나면 한꺼번에 줄로 만든다.
    private readonly Queue<SimulationStepResult> pendingResults = new Queue<SimulationStepResult>();
    #endregion

    #region Method
    /// <summary>결과 1개를 목록 맨 아래에 추가한다. 프리팹이 아직 없으면 먼저 불러온다.</summary>
    public void AddResult(SimulationStepResult _result)
    {
        if (_result == null || contentRect == null)
        {
            return;
        }

        pendingResults.Enqueue(_result);

        if (itemPrefab != null)
        {
            CreatePendingItems();
            return;
        }

        if (isLoadingPrefab)
        {
            return;
        }

        isLoadingPrefab = true;
        string key = AddressableAssetKey.ResultListItem.ToString();
        AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
            key,
            prefab =>
            {
                isLoadingPrefab = false;
                if (this == null)
                {
                    return;
                }

                itemPrefab = prefab;
                CreatePendingItems();
            },
            () =>
            {
                isLoadingPrefab = false;
                pendingResults.Clear();
                DebugLogManager.GenerateErrorMessage<UI_SimulationResultView>($"{key} 로드 실패");
            });
    }

    /// <summary>줄을 모두 지우고 기다리던 결과도 버린다. 새 시뮬레이션을 시작할 때와 대상이 바뀔 때 호출한다.</summary>
    public void Clear()
    {
        pendingResults.Clear();

        if (contentRect == null)
        {
            return;
        }

        contentRect.DestroyChildren();
    }

    private void CreatePendingItems()
    {
        while (pendingResults.Count > 0)
        {
            SimulationStepResult result = pendingResults.Dequeue();
            GameObject itemObject = AddressableAssetManager.Instance.InstantiatePrefab(itemPrefab, contentRect);
            itemObject.name = $"ResultListItem_{result.stepNumber}";
            itemObject.GetComponent<UI_SimulationResultItem>().Setup(result);
        }
    }
    #endregion
}
