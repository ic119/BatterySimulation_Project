using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>Module 전용 생성·수정 로직.</summary>
    public class ModuleHandler : DeviceNodeHandler
    {
        public override DeviceType Kind => DeviceType.module;
    }
}
