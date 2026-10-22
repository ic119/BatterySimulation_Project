using Incheol.Presenter;
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

    private MainSceneManager mainSceneManager;
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
    public void Init(MainSceneManager _manager)
    {
        mainSceneManager = _manager;
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

        if (!isActiveAndEnabled || mainSceneManager == null || mainSceneManager.SelectedModel == null)
        {
            ClearName();
            return;
        }

        nameLabel.text = mainSceneManager.GetSelectedLabel();
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