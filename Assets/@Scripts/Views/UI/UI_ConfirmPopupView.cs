using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 되돌릴 수 없는 동작(삭제 등) 전에 "확인 / 취소"를 묻는 팝업.
/// confirmPopup은 화면 전체를 덮는 오브젝트라 팝업이 떠 있는 동안 뒤의 UI는 눌리지 않는다.
/// </summary>
public class UI_ConfirmPopupView : MonoBehaviour
{
    #region Variable
    [Header("ConfirmPopup Variable")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private TextMeshProUGUI confirmContent;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action onConfirm;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnClickConfirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Hide);
        }

        // 평소에는 보이지 않게 둔다.
        Hide();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnClickConfirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Hide);
        }
    }
    #endregion

    #region Method
    /// <summary>_message를 보여 주고, 확인을 누르면 _onConfirm을 실행한다. 취소하면 아무 것도 하지 않는다.</summary>
    public void Show(string _message, Action _onConfirm)
    {
        onConfirm = _onConfirm;

        if (confirmContent != null)
        {
            confirmContent.text = _message;
        }

        if (confirmPopup != null)
        {
            confirmPopup.SetActive(true);
            confirmPopup.transform.SetAsLastSibling();
        }
    }

    private void OnClickConfirm()
    {
        Action action = onConfirm;
        Hide();
        action?.Invoke();
    }

    private void Hide()
    {
        onConfirm = null;

        if (confirmPopup != null)
        {
            confirmPopup.SetActive(false);
        }
    }
    #endregion
}
