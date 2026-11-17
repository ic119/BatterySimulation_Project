using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>ModuleAssembly 전용 생성·수정 로직.</summary>
    public class ModuleAssemblyHandler : DeviceNodeHandler
    {
        public override DeviceType Kind => DeviceType.ModuleAssembly;
    }
}
