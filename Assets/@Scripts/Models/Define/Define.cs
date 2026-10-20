namespace Incheol.Models.Define
{
    /// <summary>
    /// Addressable Asset 을 위한 Load Key 정의
    /// </summary>
    public enum AddressableAssetKey
    {
        None = 0,
        UI_MainScene = 1,
        UI_LoadingBarView = 2,
        PackPrefab = 3,
        ModulePrefab = 4,
        CellPrefab = 5,
        UI_TreeNodeItem = 6,
        UI_JsonFileItem = 7
    }

    /// <summaryㅕㅑ>
    /// 장비 타입 정의
    /// </summary>
    public enum DeviceType
    {
        Pakc = 0,
        Module = 1,
        Cell = 2
    }

}