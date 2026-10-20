using Incheol.Utils;
using System.Threading;
using TMPro;
using UnityEngine;

public class UI_AlarmPopupView : MonoBehaviour
{
    #region Variable
    [Header("AlarmPopup Variable")]
    [SerializeField] private GameObject alarmPopup;
    [SerializeField] private TextMeshProUGUI alarmContent;

    [Header("AlarmPopup Duration")]
    [SerializeField, Range(2.0f, 3.0f), Tooltip("알람이 켜져 있는 시간(초). 2~3초 사이.")] private float showSeconds = 2.5f;

    private CancellationTokenSource hideCts;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        // 평소에는 보이지 않게 둔다.
        Hide();
    }

    private void OnDisable()
    {
        Init();
    }

    private void OnDestroy()
    {
        CancelHide();
    }
    #endregion

    #region Method
    /// <summary>
    /// _message를 alarmContent에 표시하고 alarmPopup을 켠 뒤 showSeconds(2~3초) 후 비활성화
    /// </summary>
    public void Show(string _message)
    {
        if (alarmContent != null)
        {
            alarmContent.text = _message;
        }

        if (alarmPopup != null)
        {
            alarmPopup.SetActive(true);
        }

        // 표시 중에 다시 부르면 이전 타이머를 취소하고 시간을 처음부터 다시 센다.
        CancelHide();
        hideCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        _ = HideAfterDelayAsync(hideCts.Token);
    }

    private async Awaitable HideAfterDelayAsync(CancellationToken _token)
    {
        bool elapsed = await AwaitableTimer.WaitRealtimeAsync(Mathf.Clamp(showSeconds, 2.0f, 3.0f), _token);
        if (!elapsed)
        {
            return;
        }

        hideCts.Dispose();
        hideCts = null;
        Hide();
    }

    private void CancelHide()
    {
        if (hideCts != null)
        {
            hideCts.Cancel();
            hideCts.Dispose();
            hideCts = null;
        }
    }

    private void Hide()
    {
        if (alarmPopup != null)
        {
            alarmPopup.SetActive(false);
        }

        Init();
    }

    private void Init()
    {
        if (alarmContent != null && !string.IsNullOrEmpty(alarmContent.text))
        {
            alarmContent.text = string.Empty;
        }
    }
    #endregion
}
