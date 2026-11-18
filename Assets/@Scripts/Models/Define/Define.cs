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
        ModuleAssemblyPrefab = 4,
        ModulePrefab = 5,
        ParallelAssemblyPrefab = 6,
        Cell_PouchType = 7,
        UI_TreeNodeItem = 8,
        UI_JsonFileItem = 9,
        SimulationItem = 10,
        ResultListItem = 11,
    }
    
    /// <summary>
    /// 시뮬레이션 타입 정의
    /// </summary>
    public enum SimulationType
    {
        None = 0,
        Charge = 1,
        Discharge = 2,
        ThermalDetection = 3
    }

    public enum DeviceType
    {
        pack,
        moduleassembly,
        module,
        parallelassembly,
        cell
    }

    public enum CellType
    {
        None,
        Cylindrical,
        Pouch,
        Prismatic
    }

    public enum StackingAxisType
    {
        X,
        Y
    }

    public enum TopologyType
    {
        None,
        Hexagonal,
        NStack,
        SingleStack,
        Square
    }
}