using System.Collections;
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

    private Coroutine hideCoroutine;
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
    #endregion

    #region Method
    /// <summary>
    /// _message를 alarmContent에 표시하고 alarmPopup을 켠 뒤 showSeconds(2~3초) 후 다시 끈다.
    /// 표시 중에 다시 호출하면 내용을 바꾸고 시간을 처음부터 다시 센다.
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

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        // 이 뷰 자체가 꺼져 있으면 코루틴을 돌릴 수 없으므로 바로 끄지 않고 켜진 채 둔다.
        if (isActiveAndEnabled)
        {
            hideCoroutine = StartCoroutine(HideAfterDelay());
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Clamp(showSeconds, 2.0f, 3.0f));
        hideCoroutine = null;
        Hide();
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
