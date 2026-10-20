using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Incheol.View.UI
{
    public class UI_ProgressBarView : MonoBehaviour
    {
        #region Variable
        [Header("UI 변수")]
        [SerializeField] private TextMeshProUGUI title_Text;
        [SerializeField] private TextMeshProUGUI percent_Text;
        [SerializeField] private Slider slider;
        #endregion

        #region Method
        /// <summary>
        /// Progress 값을 0.0f에서 1.0f 사이로 설정하고 percent_Text와 slider를 업데이트합니다.
        /// </summary>
        /// <param name="progress">0.0f ~ 1.0f 사이의 진행도</param>
        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (slider != null)
            {
                slider.normalizedValue = progress;
            }

            if (percent_Text != null)
            {
                percent_Text.text = string.Format("{0}%", Mathf.RoundToInt(progress * 100f));
            }
        }

        /// <summary>
        /// 프로그레스바의 타이틀 텍스트(예: "Loading...", "데이터 다운로드 중...")를 설정합니다.
        /// </summary>
        public void SetTitle(string title)
        {
            if (title_Text != null)
            {
                title_Text.text = title;
            }
        }
        #endregion
    }
}
