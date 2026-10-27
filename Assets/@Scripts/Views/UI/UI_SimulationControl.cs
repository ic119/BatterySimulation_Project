using System;
using System.Collections.Generic;
using System.Globalization;
using Incheol.Modules;
using Incheol.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AddressableAssetKey = Incheol.Models.Define.AddressableAssetKey;
using DeviceType = Incheol.Models.Define.DeviceType;
using SimulationType = Incheol.Models.Define.SimulationType;

public class UI_SimulationControl : MonoBehaviour
{
    #region Variable
    [Header("Simulation Area Container")]
    [SerializeField] private GameObject simulationRoot;

    [Header("Selected Device NameLabel")]
    [SerializeField] private TextMeshProUGUI nameLabel;

    [Header("Device SimulationType Dropdown")]
    [SerializeField] private TMP_Dropdown simulationDropdown;

    [Header("Simulation DuringTime InputField")]
    [SerializeField] private TMP_InputField duringTimeInputField;

    [Header("Setting Apply Button")]
    [SerializeField] private Button applyButton;

    [Header("Simulation Start Button")]
    [SerializeField] private Button startButton;

    [Header("SimulationProcess ScrollView")]
    [SerializeField] private RectTransform contentRect;

    [Header("Pack CustomShader")]
    [SerializeField] private Shader customOutline;
    [SerializeField] private Material packPrefabOutLine;
    [SerializeField] private Shader electricChargeFlow;
    [SerializeField] private Material electricChargeFlowMaterial;


    [Header("Alarm")]
    [SerializeField, Tooltip("SimulationTab의 알림 팝업. 저장할 값이 비어 있을 때 알린다.")] private UI_AlarmPopupView alarmPopupView;

    private DeviceSelection selection;
    private UI_EditorView treeView;

    // 프리팹 로드를 기다리는 사이 목록이 다시 그려지면, 이전 요청의 콜백이 줄을 중복으로 만들지 않도록 구분한다.
    private int itemVersion;

    private SimulationRunner runner;
    private SimulationEffectPlayer effectPlayer;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        runner = new SimulationRunner(this);
        runner.Finished += RefreshStartButton;
        effectPlayer = new SimulationEffectPlayer(runner, customOutline, packPrefabOutLine, electricChargeFlow, electricChargeFlowMaterial);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        runner.Stop();
        effectPlayer.Clear();
        RefreshStartButton();
        ClearName();
    }

    private void OnDestroy()
    {
        if (selection != null)
        {
            selection.Changed -= Refresh;
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplySettings);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartSimulation);
        }
    }
    #endregion

    #region Method
    public void Init(DeviceSelection _selection, UI_EditorView _treeView)
    {
        if (selection != null)
        {
            selection.Changed -= Refresh;
        }

        selection = _selection;
        treeView = _treeView;
        selection.Changed += Refresh;

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplySettings);
            applyButton.onClick.AddListener(ApplySettings);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartSimulation);
            startButton.onClick.AddListener(StartSimulation);
        }

        BuildDropdownOptions();
        Refresh();
    }

    /// <summary>
    /// 선택에 맞춰 simulationRoot의 활성 상태, nameLabel, 입력(dropdown / inputField), 작업 목록(contentRect)을 모두 갱신한다.
    /// 선택이 바뀔 때와 탭이 켜질 때 호출한다.
    /// </summary>
    private void Refresh()
    {
        RefreshRoot();
        RefreshName();
        ResetInput();
        RefreshItems();
    }

    /// <summary>
    /// 선택된 항목이 있고 Container일 때만 simulationRoot를 켠다. 그 외에는 끈다.
    /// </summary>
    private void RefreshRoot()
    {
        if (simulationRoot == null)
        {
            return;
        }

        bool isContainerSelected = selection != null && selection.Model != null && selection.Kind == DeviceType.Container;
        simulationRoot.SetActive(isContainerSelected);
    }

    /// <summary>
    /// simulationDropdown의 options를 SimulationType 정의 순서대로 구축한다. 옵션의 index는 SimulationType 값과 같다.
    /// </summary>
    private void BuildDropdownOptions()
    {
        if (simulationDropdown == null)
        {
            return;
        }

        List<TMP_Dropdown.OptionData> optionList = new List<TMP_Dropdown.OptionData>();
        foreach (SimulationType type in Enum.GetValues(typeof(SimulationType)))
        {
            optionList.Add(new TMP_Dropdown.OptionData(type.ToString()));
        }

        simulationDropdown.ClearOptions();
        simulationDropdown.AddOptions(optionList);
        simulationDropdown.SetValueWithoutNotify((int)SimulationType.None);
    }

    /// <summary>
    /// SimulationTab이 켜져 있고 선택된 항목이 있으면 "이름 (DeviceType)" 형식으로 nameLabel에 표시하고, 아니면 비운다.
    /// 탭이 켜질 때와 선택 / 이름이 바뀔 때 호출한다.
    /// </summary>
    public void RefreshName()
    {
        if (nameLabel == null)
        {
            return;
        }

        if (!isActiveAndEnabled || selection == null || treeView == null || selection.Model == null)
        {
            ClearName();
            return;
        }

        nameLabel.text = $"{treeView.GetLabel(selection.Model)} ({selection.Kind})";
    }

    /// <summary>
    /// dropdown / inputField를 기본값(None, 빈 입력)으로 되돌려 다음 작업을 입력할 수 있게 한다. 선택이 바뀔 때와 탭이 켜질 때 호출한다.
    /// </summary>
    private void ResetInput()
    {
        if (simulationDropdown != null)
        {
            simulationDropdown.SetValueWithoutNotify((int)SimulationType.None);
        }

        if (duringTimeInputField != null)
        {
            duringTimeInputField.SetTextWithoutNotify(string.Empty);
        }
    }

    /// <summary>
    /// simulationDropdown의 value와 duringTimeInputField의 값을 선택된 항목의 오브젝트에 SimulationDataModel로 저장한다.
    /// 저장용 SimulationData 컴포넌트가 없으면 추가한다. 선택이 없거나 지속 시간이 올바른 숫자가 아니면 저장하지 않는다.
    /// </summary>
    private void ApplySettings()
    {
        if (selection == null || !selection.HasSelection || treeView == null)
        {
            DebugLogManager.GenerateWarningMessage<UI_SimulationControl>("선택된 항목이 없어 시뮬레이션 설정을 저장하지 않았습니다.");
            return;
        }

        if (simulationDropdown == null || simulationDropdown.options.Count == 0)
        {
            ShowAlarm("시뮬레이션 타입이 비어있습니다.");
            return;
        }

        if (duringTimeInputField == null || string.IsNullOrWhiteSpace(duringTimeInputField.text))
        {
            ShowAlarm("지속 시간이 비어있습니다.");
            return;
        }

        if (!float.TryParse(duringTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float duringTime) || duringTime < 0f)
        {
            ShowAlarm("지속 시간이 올바른 숫자가 아닙니다.");
            return;
        }

        SimulationData holder = selection.Target.GetComponent<SimulationData>();
        if (holder == null)
        {
            holder = selection.Target.gameObject.AddComponent<SimulationData>();
        }

        SimulationDataModel data = holder.Model;
        data.deviceName = treeView.GetLabel(selection.Model);
        data.deviceType = selection.Kind;
        data.AddStep((SimulationType)simulationDropdown.value, duringTime);
        RefreshItems();

        alarmPopupView?.Show("저장되었습니다.");
    }

    /// <summary>
    /// 선택된 Container의 오브젝트에 SimulationDataModel이 있고 stepList가 1개 이상이면, contentRect 아래에 stepList 순서대로 SimulationItem 줄을 만든다.
    /// 그 외에는 줄을 모두 지운다. 줄 프리팹은 Addressable(SimulationItem)로 불러온다.
    /// </summary>
    private void RefreshItems()
    {
        RefreshStartButton();

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
            () => DebugLogManager.GenerateErrorMessage<UI_SimulationControl>($"{key} 로드 실패"));
    }

    /// <summary>
    /// 선택된 Container의 SimulationDataModel에 stepList가 1개 이상이고 시뮬레이션이 실행 중이 아닐 때만 startButton을 활성화하고,
    /// 그 외(선택 없음, 데이터 없음, 0개, 실행 중)에는 비활성화한다.
    /// </summary>
    private void RefreshStartButton()
    {
        if (startButton == null)
        {
            return;
        }

        bool hasStep = false;
        if (selection != null && selection.HasSelection && selection.Kind == DeviceType.Container
            && selection.Target.TryGetComponent(out SimulationData holder))
        {
            hasStep = holder.Model.StepCount != 0;
        }

        startButton.interactable = hasStep && !runner.IsRunning;
    }

    /// <summary>
    /// startButton 클릭 시: 선택된 Container의 작업을 SimulationRunner로 실행한다. 실행 중에는 다시 시작하지 않는다.
    /// </summary>
    private void StartSimulation()
    {
        // 첫 작업의 StepBegan이 Start() 안에서 바로 호출되므로, 연출 대상은 Start() 전에 지정해야 한다. 실행 중에는 바꾸지 않는다.
        if (selection != null && !runner.IsRunning)
        {
            effectPlayer.Target = selection.Target;
        }

        if (selection == null || !selection.HasSelection || selection.Kind != DeviceType.Container
            || !selection.Target.TryGetComponent(out SimulationData holder) || !runner.Start(holder.Model))
        {
            DebugLogManager.GenerateWarningMessage<UI_SimulationControl>("실행할 시뮬레이션 작업이 없거나 이미 실행 중입니다.");
            return;
        }

        RefreshStartButton();
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
            Destroy(child.gameObject);
        }
    }

    private void CreateItems(GameObject _prefab, SimulationDataModel _data, int _version)
    {
        // 로드를 기다리는 사이 뷰가 파괴되었거나 목록이 다시 그려졌으면 이 요청은 버린다.
        if (this == null || contentRect == null || _version != itemVersion)
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
            DebugLogManager.GenerateWarningMessage<UI_SimulationControl>("이미 지워졌거나 선택이 바뀐 작업이라 삭제하지 않고 목록만 새로 그렸습니다.");
        }

        RefreshItems();
    }

    private void ShowAlarm(string _message)
    {
        DebugLogManager.GenerateWarningMessage<UI_SimulationControl>(_message);
        alarmPopupView?.Show(_message);
    }

    private void ClearName()
    {
        if (nameLabel != null)
        {
            nameLabel.text = null;
        }
    }
    #endregion
}