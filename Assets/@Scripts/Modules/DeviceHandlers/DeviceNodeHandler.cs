using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>
    /// 장치 종류(Pack / ModuleAssembly / Module / ParallelAssembly / Cell)별 생성·수정 로직을 담는 핸들러의 공통 기반.
    /// 종류와 상관없이 같은 로직(이름, 크기, 간격, 표시, 삭제 등)은 여기에 두고, 종류마다 다른 로직은 각 핸들러에 둔다.
    /// </summary>
    public abstract class DeviceNodeHandler
    {
        /// <summary>이 핸들러가 다루는 장치 종류.</summary>
        public abstract DeviceType Kind { get; }
    }
}
