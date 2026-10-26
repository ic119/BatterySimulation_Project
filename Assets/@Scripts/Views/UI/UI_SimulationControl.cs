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

    [Header("Alarm")]
    [SerializeField, Tooltip("SimulationTab의 알림 팝업. 저장할 값이 비어 있을 때 알린다.")] private UI_AlarmPopupView alarmPopupView;

    private DeviceSelection selection;
    private UI_EditorView treeView;
    #endregion

    #region LifeCycle
    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
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

        BuildDropdownOptions();
        Refresh();
    }

    /// <summary>
    /// 선택에 맞춰 simulationRoot의 활성 상태, nameLabel, 입력(dropdown / inputField)을 모두 갱신한다.
    /// 선택이 바뀔 때와 탭이 켜질 때 호출한다.
    /// </summary>
    private void Refresh()
    {
        RefreshRoot();
        RefreshName();
        ResetInput();
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

        alarmPopupView?.Show("저장되었습니다.");
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