using Incheol.Presenter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_SelectedDeviceNameView : MonoBehaviour
{
    #region Variable
    [Header("Change Name Variable")]
    [SerializeField] private TMP_InputField changeNameInputField;
    [SerializeField] private Button applyButton;

    private MainSceneManager mainSceneManager;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (applyButton != null)
        {
            applyButton.onClick.AddListener(OnClickApply);
        }

        Clear();
    }

    private void OnDestroy()
    {
        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(OnClickApply);
        }
    }
    #endregion

    #region Method
    /// <summary>선택된 항목의 이름을 입력 필드에 채운다. 사용자가 입력 중이던 내용은 덮어쓴다.</summary>
    public void ShowSelected(string _name)
    {
        if (changeNameInputField != null)
        {
            changeNameInputField.interactable = true;
            changeNameInputField.SetTextWithoutNotify(_name);
        }

        if (applyButton != null)
        {
            applyButton.interactable = true;
        }
    }

    /// <summary>선택이 없을 때 입력 필드를 비우고 입력을 막는다.</summary>
    public void Clear()
    {
        if (changeNameInputField != null)
        {
            changeNameInputField.SetTextWithoutNotify(string.Empty);
            changeNameInputField.interactable = false;
        }

        if (applyButton != null)
        {
            applyButton.interactable = false;
        }
    }

    private void OnClickApply()
    {
        if (changeNameInputField == null)
        {
            return;
        }

        if (mainSceneManager == null)
        {
            mainSceneManager = FindAnyObjectByType<MainSceneManager>();
        }

        mainSceneManager?.RenameSelected(changeNameInputField.text);
    }
    #endregion
}
