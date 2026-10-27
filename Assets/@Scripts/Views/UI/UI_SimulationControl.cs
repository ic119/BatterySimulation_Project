using System;
using System.Collections.Generic;
using System.Globalization;
using Incheol.Modules;
using Incheol.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private Material packPrefabOutLine;
    [SerializeField] private Material electricChargeFlowMaterial;


    [Header("Alarm")]
    [SerializeField, Tooltip("SimulationTab의 알림 팝업. 저장할 값이 비어 있을 때 알린다.")] private UI_AlarmPopupView alarmPopupView;

    private DeviceSelection selection;
    private UI_EditorView treeView;
    private PackFactory packFactory;

    private UI_SimulationStepList stepList;
    private SimulationRunner runner;
    private SimulationEffectPlayer effectPlayer;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        EnsureCreated();
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
    /// <summary>
    /// stepList / runner / effectPlayer를 한 번만 만든다. 탭이 꺼진 채로 Init이 먼저 불리면 Awake가 아직 실행되지 않았으므로 Init에서도 호출한다.
    /// </summary>
    private void EnsureCreated()
    {
        if (runner != null)
        {
            return;
        }

        stepList = new UI_SimulationStepList(contentRect, RefreshStartButton);
        runner = new SimulationRunner(this);
        runner.Finished += RefreshStartButton;
        effectPlayer = new SimulationEffectPlayer(runner, packPrefabOutLine, electricChargeFlowMaterial);
    }

    public void Init(DeviceSelection _selection, UI_EditorView _treeView, PackFactory _packFactory)
    {
        EnsureCreated();

        if (selection != null)
        {
            selection.Changed -= Refresh;
        }

        selection = _selection;
        treeView = _treeView;
        packFactory = _packFactory;
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
        // 연출 중인 Container가 아닌 다른 항목이 선택되면, 진행 중이던 시뮬레이션을 멈추고 Pack의 머티리얼을 되돌린다.
        if (effectPlayer.Container != null && !ReferenceEquals(selection != null ? selection.Model : null, effectPlayer.Container))
        {
            runner.Stop();
            effectPlayer.Clear();
        }

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
    /// 작업 목록(stepList 줄)을 다시 그리고 startButton 상태를 갱신한다.
    /// </summary>
    private void RefreshItems()
    {
        RefreshStartButton();
        stepList.Refresh(selection);
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
        if (selection != null && !runner.IsRunning && selection.Model is ContainerDataModel container)
        {
            effectPlayer.SetContainer(container, packFactory);
        }

        if (selection == null || !selection.HasSelection || selection.Kind != DeviceType.Container
            || !selection.Target.TryGetComponent(out SimulationData holder) || !runner.Start(holder.Model))
        {
            DebugLogManager.GenerateWarningMessage<UI_SimulationControl>("실행할 시뮬레이션 작업이 없거나 이미 실행 중입니다.");
            return;
        }

        RefreshStartButton();
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