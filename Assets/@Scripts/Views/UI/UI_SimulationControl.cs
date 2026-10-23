using Incheol.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private DeviceSelection selection;
    private UI_EditorView treeView;
    #endregion

    #region LifeCycle
    private void OnEnable()
    {
        RefreshName();
    }

    private void OnDisable()
    {
        ClearName();
    }
    #endregion

    #region Method
    public void Init(DeviceSelection _selection, UI_EditorView _treeView)
    {
        selection = _selection;
        treeView = _treeView;
        RefreshName();
    }

    /// <summary>
    /// SimulationTab이 켜져 있고 선택된 항목이 있으면 그 이름을 nameLabel에 표시하고, 아니면 비운다.
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

        nameLabel.text = treeView.GetLabel(selection.Model);
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