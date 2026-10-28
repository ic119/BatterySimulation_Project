using SimulationType = Incheol.Models.Define.SimulationType;

/// <summary>
/// 시뮬레이션 작업(step) 1개가 끝났을 때의 결과. 결과 목록(UI_SimulationResultView)의 한 줄이 이 값을 표시한다.
/// 수량은 시뮬레이션 대상 Container 기준이며, 이상(abnormal)은 그 작업의 연출이 진행된(= 점멸한) Module / Cell을 뜻한다.
/// </summary>
public class SimulationStepResult
{
    /// <summary>실행 순서(1부터)</summary>
    public int stepNumber;
    public SimulationType simulationType;
    public float duringTime;

    public int packCount;
    public int moduleCount;
    public int cellCount;

    public int abnormalModuleCount;
    public int abnormalCellCount;

    // ThermalDetection 작업에서만 채워지는 값(섭씨). 다른 타입에서는 0이다.
    /// <summary>측정된 가장 높은 온도</summary>
    public float maxTemperature;

    /// <summary>측정된 가장 낮은 온도</summary>
    public float minTemperature;

    /// <summary>온도가 과하게 오른 부분(열원)의 수량</summary>
    public int overheatCount;

    /// <summary>Module + Cell의 전체 수량</summary>
    public int TotalCount => moduleCount + cellCount;

    /// <summary>이상이 있는 Module + Cell의 수량</summary>
    public int AbnormalCount => abnormalModuleCount + abnormalCellCount;

    /// <summary>
    /// 안정성(%) = (전체 수량 - 이상 수량) / 전체 수량 x 100. 이상이 없으면 100, 전부 이상이면 0.
    /// 전체 수량이 0이면 판단할 대상이 없으므로 100으로 둔다.
    /// </summary>
    public float StabilityPercent
    {
        get
        {
            int total = TotalCount;
            if (total <= 0)
            {
                return 100f;
            }

            return (1f - (float)AbnormalCount / total) * 100f;
        }
    }
}
