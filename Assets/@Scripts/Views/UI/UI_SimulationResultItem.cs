using System.Globalization;
using TMPro;
using UnityEngine;
using SimulationType = Incheol.Models.Define.SimulationType;

/// <summary>
/// 시뮬레이션 결과 목록의 한 줄을 이루는 프리팹(ResultListItem)에 붙는 컴포넌트.
/// 작업 1개의 결과를 표시한다. UI_SimulationResultView가 작업이 끝날 때마다 줄을 만든다.
/// Charge / Discharge는 구조 수량, 이상 수량, 안정성을, ThermalDetection은 구조 수량, 최고 / 최저 온도, 과열 수량을 보여준다.
/// </summary>
public class UI_SimulationResultItem : MonoBehaviour
{
    #region Variable
    [Header("Result Item Variable")]
    [SerializeField, Tooltip("작업 번호 / 타입 / 지속 시간")] private TMP_Text titleText;
    [SerializeField, Tooltip("Pack / Module / Cell 전체 수량")] private TMP_Text structureText;
    [SerializeField, Tooltip("Charge / Discharge: 연출이 진행된(이상) Module / Cell 수량. ThermalDetection: 최고 / 최저 온도")] private TMP_Text abnormalText;
    [SerializeField, Tooltip("강조할 값(이상 수량, 최고 온도, 과열 수량)에만 입히는 색")] private Color abnormalColor = Color.red;
    [SerializeField, Tooltip("강조할 값을 뺀 나머지 글자의 색")] private Color abnormalLineColor = Color.black;
    [SerializeField, Tooltip("Charge / Discharge: 안정성(%). ThermalDetection: 과열 수량")] private TMP_Text stabilityText;
    #endregion

    #region Method
    /// <summary>_result의 타입에 맞는 문구로 각 텍스트를 채운다. 연결되지 않은 텍스트는 건너뛴다.</summary>
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

        switch (_result.simulationType)
        {
            case SimulationType.ThermalDetection:
                SetupThermal(_result);
                break;
            default:
                SetupCharge(_result);
                break;
        }
    }

    /// <summary>ThermalDetection: 최고 / 최저 온도와 과열 수량을 표시한다.</summary>
    private void SetupThermal(SimulationStepResult _result)
    {
        if (abnormalText != null)
        {
            string abnormalHex = ColorUtility.ToHtmlStringRGB(abnormalColor);
            abnormalText.color = abnormalLineColor;
            abnormalText.text = $"Temperature  Max <color=#{abnormalHex}>{_result.maxTemperature.ToString("F1", CultureInfo.InvariantCulture)}°C</color>  |  Min {_result.minTemperature.ToString("F1", CultureInfo.InvariantCulture)}°C";
        }

        if (stabilityText != null)
        {
            string abnormalHex = ColorUtility.ToHtmlStringRGB(abnormalColor);
            stabilityText.text = $"Overheat <color=#{abnormalHex}>{_result.overheatCount}</color> spots";
        }
    }

    /// <summary>Charge / Discharge: 이상 수량과 안정성을 표시한다.</summary>
    private void SetupCharge(SimulationStepResult _result)
    {
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
