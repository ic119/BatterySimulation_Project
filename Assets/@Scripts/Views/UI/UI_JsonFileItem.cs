using Incheol.Utils;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 저장된 Json 파일 목록의 한 줄(파일 이름 + 삭제 버튼)을 이루는 프리팹(UI_JsonFileItem)에 붙는 컴포넌트.
/// 줄 전체가 버튼이며, 마우스를 올리거나 누르면 글자 색이 변한다.
/// 삭제 버튼은 실수로 지우지 않도록 두 번 눌러야 한다. 첫 번째는 "확인"으로 바뀌고, 일정 시간 안에 한 번 더 누르면 삭제된다.
/// </summary>
public class UI_JsonFileItem : MonoBehaviour
{
    private const string DeleteText = "삭제";
    private const string ConfirmText = "확인";

    #region Variable
    [Header("Json File Item Variable")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button itemButton;

    [Header("Delete Variable")]
    [SerializeField] private Button deleteButton;
    [SerializeField] private TMP_Text deleteLabel;
    [SerializeField, Min(0.5f), Tooltip("삭제 버튼을 한 번 누른 뒤 이 시간(초) 안에 다시 눌러야 삭제된다.")] private float confirmSeconds = 3.0f;
    [SerializeField] private Color deleteColor = new Color(1.0f, 0.5f, 0.5f, 1.0f);
    [SerializeField] private Color confirmColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);

    private UnityAction onDelete;
    private CancellationTokenSource confirmCts;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(OnClickDelete);
        }
    }

    private void OnDestroy()
    {
        CancelConfirm();

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(OnClickDelete);
        }
    }
    #endregion

    #region Method
    /// <summary>
    /// 표시할 이름을 정하고 클릭 / 삭제 콜백을 연결한다.
    /// _onClick이 null이면 누를 수 없는 안내 문구가 되고, _onDelete가 null이면 삭제 버튼을 숨긴다.
    /// </summary>
    public void Setup(string _label, UnityAction _onClick, UnityAction _onDelete = null)
    {
        label.text = _label;

        itemButton.onClick.RemoveAllListeners();
        if (_onClick != null)
        {
            itemButton.onClick.AddListener(_onClick);
        }

        itemButton.interactable = _onClick != null;
        label.raycastTarget = false;

        onDelete = _onDelete;
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(_onDelete != null);
        }

        ResetDelete();
    }

    private void OnClickDelete()
    {
        if (onDelete == null)
        {
            return;
        }

        // 첫 번째 누름: 확인 상태로 바꾸고 시간 안에 한 번 더 누르길 기다린다.
        if (confirmCts == null)
        {
            SetDeleteLabel(ConfirmText, confirmColor);
            confirmCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            _ = CancelConfirmAfterDelayAsync(confirmCts.Token);
            return;
        }

        // 두 번째 누름: 삭제한다.
        CancelConfirm();
        onDelete.Invoke();
    }

    private async Awaitable CancelConfirmAfterDelayAsync(CancellationToken _token)
    {
        bool elapsed = await AwaitableTimer.WaitRealtimeAsync(confirmSeconds, _token);
        if (!elapsed)
        {
            return;
        }

        confirmCts.Dispose();
        confirmCts = null;
        ResetDelete();
    }

    private void CancelConfirm()
    {
        if (confirmCts != null)
        {
            confirmCts.Cancel();
            confirmCts.Dispose();
            confirmCts = null;
        }
    }

    private void ResetDelete()
    {
        CancelConfirm();
        SetDeleteLabel(DeleteText, deleteColor);
    }

    private void SetDeleteLabel(string _text, Color _color)
    {
        if (deleteLabel != null)
        {
            deleteLabel.text = _text;
            deleteLabel.color = _color;
        }
    }
    #endregion
}
