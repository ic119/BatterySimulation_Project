using System;
using System.Collections.Generic;
using System.Globalization;
using Incheol.Modules;
using Incheol.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SimulationType = Incheol.Models.Define.SimulationType;

public class UI_SimulationControl : MonoBehaviour
{
    #region Variable
    [Header("Selected Device NameLabel")]
    [SerializeField] private TextMeshProUGUI nameLabel;

    [Header("Device SimulationType Dropdown")]
    [SerializeField] private TMP_Dropdown simulationDropdown;

    [Header("Simulation DuringTime InputField")]
    [SerializeField] private TMP_InputField duringTimeInputField;

    [Header("Setting Apply Button")]
    [SerializeField] private Button applyButton;

    [Header("Simulation Step Text")]
    [SerializeField] private TextMeshProUGUI simulationStepText;

    [Header("Alarm")]
    [SerializeField, Tooltip("SimulationTab의 알림 팝업. 저장할 값이 비어 있을 때 알린다.")] private UI_AlarmPopupView alarmPopupView;

    private DeviceSelection selection;
    private UI_EditorView treeView;
    #endregion

    #region LifeCycle
    private void OnEnable()
    {
        RefreshName();
        RefreshSettings();
    }

    private void OnDisable()
    {
        ClearName();
    }

    private void OnDestroy()
    {
        if (selection != null)
        {
            selection.Changed -= RefreshSettings;
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplySettings);
        }
    }
    #endregion

    #region Method
    public void Init(DeviceSelection _selection, UI_EditorView _treeView)
    {
        if (selection != null)
        {
            selection.Changed -= RefreshSettings;
        }

        selection = _selection;
        treeView = _treeView;
        selection.Changed += RefreshSettings;

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplySettings);
            applyButton.onClick.AddListener(ApplySettings);
        }

        BuildDropdownOptions();
        RefreshName();
        RefreshSettings();
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
    /// 선택된 항목의 오브젝트에 저장된 SimulationDataModel을 dropdown / inputField에 채운다.
    /// 저장된 값이 없거나 선택이 없으면 기본값(None, 빈 입력)으로 되돌린다. 선택이 바뀔 때와 탭이 켜질 때 호출한다.
    /// </summary>
    private void RefreshSettings()
    {
        if (simulationDropdown == null || duringTimeInputField == null)
        {
            return;
        }

        SimulationDataModel data = null;
        if (selection != null && selection.HasSelection)
        {
            SimulationData holder = selection.Target.GetComponent<SimulationData>();
            data = holder != null ? holder.Model : null;
        }

        if (data == null)
        {
            simulationDropdown.SetValueWithoutNotify((int)SimulationType.None);
            duringTimeInputField.SetTextWithoutNotify(string.Empty);
            return;
        }

        simulationDropdown.SetValueWithoutNotify((int)data.simulationType);
        duringTimeInputField.SetTextWithoutNotify(data.duringTime.ToString(CultureInfo.InvariantCulture));
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
        data.simulationType = (SimulationType)simulationDropdown.value;
        data.duringTime = duringTime;
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