using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Modules
{
    /// <summary>Pack 전용 생성·수정 로직.</summary>
    public class PackHandler : DeviceNodeHandler
    {
        public override DeviceType Kind => DeviceType.pack;
    }
}
