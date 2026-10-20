using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_TabBarView : MonoBehaviour
{
    #region Variable
    [Header("Tab Button Variable")]
    [SerializeField] private Button editorButton;
    [SerializeField] private Button simulationButton;

    [Header("Tab Variable")]
    [SerializeField] private GameObject editorTab;
    [SerializeField] private GameObject simulationTab;

    [Header("Tab Color Variable")]
    [SerializeField, Tooltip("열려 있는 탭의 버튼 색. 그 버튼은 눌러도 반응하지 않는 상태(Disabled)라서 Button의 Disabled Color로 쓰인다. Transition이 Color Tint일 때 적용된다.")] private Color selectedTabColor = new Color(0.6f, 0.8f, 1.0f, 1.0f);
    [SerializeField, Tooltip("열려 있는 탭의 버튼 글자 색. 닫힌 탭의 버튼은 원래 글자 색으로 돌아간다.")] private Color selectedTextColor = Color.white;

    // 버튼 아래의 글자와 그 원래 색. 열린 탭의 버튼만 selectedTextColor로 바꾸고, 닫히면 이 색으로 되돌린다.
    private TMP_Text editorLabel;
    private TMP_Text simulationLabel;
    private Color editorLabelColor;
    private Color simulationLabelColor;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        ApplySelectedColor(editorButton);
        ApplySelectedColor(simulationButton);

        editorLabel = editorButton != null ? editorButton.GetComponentInChildren<TMP_Text>(true) : null;
        simulationLabel = simulationButton != null ? simulationButton.GetComponentInChildren<TMP_Text>(true) : null;
        editorLabelColor = editorLabel != null ? editorLabel.color : Color.white;
        simulationLabelColor = simulationLabel != null ? simulationLabel.color : Color.white;

        if (editorButton != null)
        {
            editorButton.onClick.AddListener(OnClickEditorButton);
        }

        if (simulationButton != null)
        {
            simulationButton.onClick.AddListener(OnClickSimulationButton);
        }

        // 시작할 때는 editorTab이 열려 있다.
        ShowEditorTab();
    }

    private void OnDestroy()
    {
        if (editorButton != null)
        {
            editorButton.onClick.RemoveListener(OnClickEditorButton);
        }

        if (simulationButton != null)
        {
            simulationButton.onClick.RemoveListener(OnClickSimulationButton);
        }
    }
    #endregion

    #region Method
    /// <summary>editorButton을 누르면 editorTab을 켜고 simulationTab을 끈다.</summary>
    private void OnClickEditorButton()
    {
        ShowEditorTab();
    }

    /// <summary>simulationButton을 누르면 simulationTab을 켜고 editorTab을 끈다.</summary>
    private void OnClickSimulationButton()
    {
        ShowSimulationTab();
    }

    private void ShowEditorTab()
    {
        SetTabActive(true);
    }

    private void ShowSimulationTab()
    {
        SetTabActive(false);
    }

    /// <summary>열려 있는 탭의 버튼은 이미 눌린 상태로 보이게 selectedTabColor로 바꾸고, Disabled 상태로 만들어 다시 눌리지 않게 한다.</summary>
    private void ApplySelectedColor(Button _button)
    {
        if (_button == null)
        {
            return;
        }

        ColorBlock colors = _button.colors;
        colors.disabledColor = selectedTabColor;
        _button.colors = colors;
    }

    /// <summary>_editorActive가 true면 editorTab만, false면 simulationTab만 켠다. 열린 탭의 버튼은 누를 수 없게 하고 닫힌 탭의 버튼은 누를 수 있게 한다.</summary>
    private void SetTabActive(bool _editorActive)
    {
        if (editorButton != null)
        {
            editorButton.interactable = !_editorActive;
        }

        if (simulationButton != null)
        {
            simulationButton.interactable = _editorActive;
        }

        if (editorLabel != null)
        {
            editorLabel.color = _editorActive ? selectedTextColor : editorLabelColor;
        }

        if (simulationLabel != null)
        {
            simulationLabel.color = _editorActive ? simulationLabelColor : selectedTextColor;
        }

        if (editorTab != null)
        {
            editorTab.SetActive(_editorActive);
        }

        if (simulationTab != null)
        {
            simulationTab.SetActive(!_editorActive);
        }
    }
    #endregion
}