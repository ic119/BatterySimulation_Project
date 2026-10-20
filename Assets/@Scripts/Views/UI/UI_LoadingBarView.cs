using UnityEngine;

namespace Incheol.View.UI
{
    public class UI_LoadingBarView : MonoBehaviour
    {
        #region Variable
        [SerializeField] private UI_ProgressBarView progressBarView;
        #endregion

        #region Method
        /// <summary>
        /// 진행률(0.0f~1.0f)을 UI_ProgressBarView에 반영한다.
        /// </summary>
        public void UpdateProgress(float progress)
        {
            if (progressBarView == null)
            {
                return;
            }

            progressBarView.SetProgress(progress);
        }

        /// <summary>
        /// 현재 수행 중/완료한 작업 이름을 UI_ProgressBarView의 타이틀 텍스트로 표시한다.
        /// </summary>
        public void UpdateTitle(string title)
        {
            if (progressBarView == null)
            {
                return;
            }

            progressBarView.SetTitle(title);
        }
        #endregion
    }
}
