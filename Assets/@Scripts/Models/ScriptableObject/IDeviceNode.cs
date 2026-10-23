/// <summary>
/// 트리에 놓이고 선택할 수 있는 장치 모델(Container / Pack / Module / Cell)의 공통 형태.
/// 선택 / 트리 / 삭제처럼 종류를 가리지 않는 곳에서 object 대신 이 타입을 쓴다.
/// </summary>
public interface IDeviceNode
{
    /// <summary>이 모델의 종류.</summary>
    Incheol.Models.Define.DeviceType Kind { get; }

    /// <summary>종류 접두어("Pack " 등)가 없는 이름. containerName / packName / moduleName / cellName을 그대로 읽고 쓴다.</summary>
    string Name { get; set; }
}
