using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>ParallelAssembly 전용 생성·수정 로직.</summary>
    public class ParallelAssemblyHandler : DeviceNodeHandler
    {
        public override DeviceType Kind => DeviceType.parallelassembly;
    }
}
