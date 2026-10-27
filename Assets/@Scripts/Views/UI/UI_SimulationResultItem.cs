using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// 시뮬레이션 결과 목록의 한 줄을 이루는 프리팹(ResultListItem)에 붙는 컴포넌트.
/// 작업 1개의 결과(구조 수량, 이상 수량, 안정성)를 표시한다. UI_SimulationResultView가 작업이 끝날 때마다 줄을 만든다.
/// </summary>
public class UI_SimulationResultItem : MonoBehaviour
{
    #region Variable
    [Header("Result Item Variable")]
    [SerializeField, Tooltip("작업 번호 / 타입 / 지속 시간")] private TMP_Text titleText;
    [SerializeField, Tooltip("Pack / Module / Cell 전체 수량")] private TMP_Text structureText;
    [SerializeField, Tooltip("연출이 진행된(이상) Module / Cell 수량")] private TMP_Text abnormalText;
    [SerializeField, Tooltip("이상 수량 줄에서 이상 수량(숫자)에만 입히는 색")] private Color abnormalColor = Color.red;
    [SerializeField, Tooltip("이상 수량 줄에서 이상 수량을 뺀 나머지 글자의 색")] private Color abnormalLineColor = Color.black;
    [SerializeField, Tooltip("안정성(%)")] private TMP_Text stabilityText;
    #endregion

    #region Method
    /// <summary>_result의 값으로 각 문구를 채운다. 연결되지 않은 텍스트는 건너뛴다.</summary>
    public void Setup(SimulationStepResult _result)
    {
        if (titleText != null)
        {
            titleText.text = $"{_result.stepNumber}) {_result.simulationType} ( {_result.duringTime.ToString(CultureInfo.InvariantCulture)} sec )";
        }

        if (structureText != null)
        {
            structureText.text = $"Pack {_result.packCount}  |  Module {_result.moduleCount}  |  Cell {_result.cellCount}";
        }

        if (abnormalText != null)
        {
            string abnormalHex = ColorUtility.ToHtmlStringRGB(abnormalColor);
            abnormalText.color = abnormalLineColor;
            abnormalText.text = $"Abnormal  Module <color=#{abnormalHex}>{_result.abnormalModuleCount}</color>/{_result.moduleCount}  |  Cell <color=#{abnormalHex}>{_result.abnormalCellCount}</color>/{_result.cellCount}";
        }

        if (stabilityText != null)
        {
            stabilityText.text = $"Stability {_result.StabilityPercent.ToString("F1", CultureInfo.InvariantCulture)}%";
        }
    }
    #endregion
}
