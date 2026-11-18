/// <summary>
/// React가 보내는 길이(mm)와 Unity 월드 단위의 변환.
/// 이 프로젝트의 월드 단위는 1 = 1m다(Cell 프리팹이 약 0.01 x 0.07 x 0.30, 길이 오차 DeviceBody.Tolerance = 0.001).
/// React 길이는 mm가 기본이므로 Unity에 적용하기 전에 반드시 FromMm으로 바꾼다. 그대로 쓰면 1000배 크게 만들어진다.
/// </summary>
public static class DeviceUnit
{
    /// <summary>1mm를 Unity 월드 단위로 바꾼 값.</summary>
    public const float MmToUnit = 0.001f;

    /// <summary>mm 길이를 Unity 월드 단위로 바꾼다.</summary>
    public static float FromMm(float _mm)
    {
        return _mm * MmToUnit;
    }

    /// <summary>Unity 월드 단위 길이를 mm로 바꾼다.</summary>
    public static float ToMm(float _unit)
    {
        return _unit / MmToUnit;
    }
}
