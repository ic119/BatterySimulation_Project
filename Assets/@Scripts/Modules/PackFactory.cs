using Incheol.Modules;
using Incheol.Utils;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pack / Module / Cell을 각각 독립적으로 생성하고 PackDataModel → ModuleDataModel → CellDataModel 계층을 관리한다.
/// 규칙: Cell은 이미 생성된 Module이 있어야만 만들 수 있다. Module은 Pack 없이도 만들 수 있고, Pack이 주어지면 그 Pack에 속한다.
/// 각 모델의 position(Transform)은 씬에 생성되는 GameObject이며, Cell은 Module의 자식이 된다.
/// Pack 안의 Module과 Module 안의 Cell은 각각 부모 내부 공간을 격자로 나눈 칸에 놓이며, 칸이 없으면 생성하지 않는다(부모 크기는 자동으로 변하지 않는다).
/// </summary>
public class PackFactory
{
    // Renderer가 없어 크기를 잴 수 없는 빈 GameObject일 때만 쓰는 예전 방식의 간격.
    private const float ModuleSpacing = 3.0f;
    private const float CellSpacing = 1.0f;

    // 만든 항목의 목록과 조회는 registry가, 지우기는 remover가 맡는다. 이 클래스는 만들고 놓는 일에 집중한다.
    private readonly PackRegistry registry = new PackRegistry();
    private readonly PackRemover remover;
    private readonly PackGridPlacer placer;
    private Transform root;
    private float packPadding;
    private float modulePadding;
    private float cellPadding;

    public PackFactory()
    {
        remover = new PackRemover(registry);
        placer = new PackGridPlacer(registry);
    }

    public IReadOnlyList<PackDataModel> Packs => registry.Packs;
    public IReadOnlyList<ModuleDataModel> Modules => registry.Modules;
    public IReadOnlyList<ContainerDataModel> Containers => registry.Containers;

    /// <summary>같은 Container 안에서 Pack 사이의 가로(x) 여백. 0 미만은 0으로 본다.</summary>
    public float PackPadding
    {
        get => packPadding;
        set => packPadding = Mathf.Max(0.0f, value);
    }

    /// <summary>Module 한 칸의 가로(x)/깊이(z) 여백. 칸 크기 = Module 크기 + 여백이고 Module은 칸 가운데에 놓이므로 Pack 벽과도 여백/2만큼 떨어진다. 0 미만은 0으로 본다.</summary>
    public float ModulePadding
    {
        get => modulePadding;
        set => modulePadding = Mathf.Max(0.0f, value);
    }

    /// <summary>Cell 한 칸의 가로(x)/깊이(z) 여백. 칸 크기 = Cell 크기 + 여백이고 Cell은 칸 가운데에 놓이므로 벽과도 여백/2만큼 떨어진다. 0 미만은 0으로 본다.</summary>
    public float CellPadding
    {
        get => cellPadding;
        set => cellPadding = Mathf.Max(0.0f, value);
    }

    /// <summary>Cell 생성 가능 여부. 생성된 Module이 하나라도 있어야 true.</summary>
    public bool CanCreateCell => registry.Modules.Count > 0;

    /// <summary>가장 최근의 CreateModule / CreateCell이 "더 이상 놓을 공간이 없어서" 실패했을 때의 안내 문구. 그 밖의 경우(성공 포함)에는 빈 문자열.</summary>
    public string LastFullMessage { get; private set; } = string.Empty;

    /// <summary>가장 최근에 생성된 Module. 없으면 null.</summary>
    public ModuleDataModel LastModule => registry.LastModule;

    /// <summary>가장 최근에 생성된 Pack. 없으면 null.</summary>
    public PackDataModel LastPack => registry.LastPack;

    /// <summary>모든 Pack/Module/Cell GameObject가 매달리는 루트. 처음 필요할 때 만든다.</summary>
    public Transform Root
    {
        get
        {
            if (root == null)
            {
                root = new GameObject("PackRoot").transform;
            }

            return root;
        }
    }

    /// <summary>만들어 둔 Pack / Module / Cell을 씬 오브젝트까지 모두 지우고 처음 상태로 되돌린다(저장된 파일을 불러오기 전에 쓴다).</summary>
    public void Clear()
    {
        if (root != null)
        {
            Object.Destroy(root.gameObject);
            root = null;
        }

        registry.Clear();
        LastFullMessage = string.Empty;
    }

    /// <summary>Pack을 그 안의 Module / Cell과 함께 지운다(데이터 모델과 씬 오브젝트 모두). 이 팩토리가 만든 Pack이 아니면 false.</summary>
    public bool RemovePack(PackDataModel _pack)
    {
        return AfterRemove(remover.RemovePack(_pack));
    }

    /// <summary>Module을 그 안의 Cell과 함께 지운다. 속한 Pack이 있으면 그 목록에서도 뺀다. 이 팩토리가 만든 Module이 아니면 false.</summary>
    public bool RemoveModule(ModuleDataModel _module)
    {
        return AfterRemove(remover.RemoveModule(_module));
    }

    /// <summary>Cell을 지운다. 속한 Module의 목록에서도 뺀다. 이미 지워진 Cell이면 false.</summary>
    public bool RemoveCell(CellDataModel _cell)
    {
        return AfterRemove(remover.RemoveCell(_cell));
    }

    /// <summary>Container를 그 안의 Pack / 단독 Module / Cell과 함께 지운다. 이 팩토리가 만든 Container가 아니면 false.</summary>
    public bool RemoveContainer(ContainerDataModel _container)
    {
        return AfterRemove(remover.RemoveContainer(_container));
    }

    /// <summary>무언가 지워졌으면 "공간이 없다"는 이전 안내 문구는 더 이상 맞지 않으므로 비운다.</summary>
    private bool AfterRemove(bool _removed)
    {
        if (_removed)
        {
            LastFullMessage = string.Empty;
        }

        return _removed;
    }

    /// <summary>Root 아래에 Pack과 단독 Module을 담을 빈 Container를 만든다. 사용자가 직접 만들지 않고 Pack / 단독 Module을 만들 때 쓴다.</summary>
    public ContainerDataModel CreateContainer(string _containerName)
    {
        GameObject containerObject = new GameObject($"Container_{_containerName}");
        containerObject.transform.SetParent(Root, false);
        containerObject.transform.position = Vector3.zero;

        ContainerDataModel container = new ContainerDataModel
        {
            containerName = _containerName,
            packsList = new List<PackDataModel>(),
            modulesList = new List<ModuleDataModel>(),
            containerPosition = containerObject.transform,
        };

        registry.AddContainer(container);
        return container;
    }

    /// <summary>씬 Transform으로 Container를 찾는다. 이 팩토리가 만든 Container가 아니면 null.</summary>
    public ContainerDataModel FindContainer(Transform _containerTransform)
    {
        return registry.FindContainer(_containerTransform);
    }

    /// <summary>Pack의 씬 Transform. PackDataModel에는 Transform 필드가 없어 등록부가 따로 들고 있는 것을 꺼낸다. 없으면 null.</summary>
    public Transform GetPackTransform(PackDataModel _pack)
    {
        return registry.GetPackTransform(_pack);
    }

    /// <summary>씬 Transform으로 Pack을 찾는다. 이 팩토리가 만든 Pack이 아니면 null.</summary>
    public PackDataModel FindPack(Transform _packTransform)
    {
        return registry.FindPack(_packTransform);
    }

    /// <summary>씬 Transform으로 Module을 찾는다. 이 팩토리가 만든 Module이 아니면 null.</summary>
    public ModuleDataModel FindModule(Transform _moduleTransform)
    {
        return registry.FindModule(_moduleTransform);
    }

    /// <summary>_prefab이 있으면 인스턴스화하고, 없으면 빈 GameObject를 만든다.</summary>
    private static GameObject Spawn(GameObject _prefab, string _objectName)
    {
        GameObject spawned = _prefab != null ? Object.Instantiate(_prefab) : new GameObject();
        spawned.name = _objectName;
        return spawned;
    }

    /// <summary>
    /// 새 Pack을 만들고 등록한다. _prefab(PackPrefab)을 주면 그것을 인스턴스화한다.
    /// _columns(x) / _rows(z)를 주면 Container 안의 격자에서 가장 먼저 나오는 빈 칸(_slotColumn / _slotRow가 비어 있으면 그 칸)에 놓는다. 주지 않으면 x 방향으로 빈자리를 찾는다.
    /// </summary>
    public PackDataModel CreatePack(string _packName, GameObject _prefab = null, Vector3 _size = default, ContainerDataModel _container = null, int _columns = 0, int _rows = 0, int _slotColumn = -1, int _slotRow = -1, Vector3? _bodyCenter = null)
    {
        // Container를 주지 않으면 이 Pack만 담는 새 Container를 만든다.
        ContainerDataModel container = _container != null ? _container : CreateContainer(_packName);
        PackDataModel pack = new PackDataModel
        {
            packName = _packName,
            modulesList = new List<ModuleDataModel>(),
            parentContainer = container,
        };
        container.packsList.Add(pack);

        // PackDataModel에는 Transform 필드가 없으므로 Module이 매달릴 부모 GameObject를 따로 들고 있는다.
        GameObject packObject = Spawn(_prefab, $"Pack_{_packName}");
        DeviceBody.AttachKeepingOwnScale(packObject.transform, container.containerPosition);
        packObject.transform.position = Vector3.zero;
        DeviceBody.Resize(packObject.transform, _size);

        bool placed = _columns > 0 && _rows > 0 && placer.PlaceInSlot(pack, packObject.transform, container, _columns, _rows, _slotColumn, _slotRow, packPadding);
        if (!placed)
        {
            PackGridPlacer.PlaceInContainer(packObject.transform, container.containerPosition, packPadding);
        }

        // 저장된 몸체 중심이 주어지면(저장된 파일을 불러올 때) 칸 위치 대신 저장 당시 자리에 그대로 둔다.
        if (_bodyCenter.HasValue)
        {
            DeviceBody.AlignCenter(packObject.transform, _bodyCenter.Value);
        }

        registry.AddPack(pack, packObject.transform);

        return pack;
    }

    /// <summary>
    /// Module을 만든다. _pack이 주어지면 그 Pack에 속하고, null이면 Pack 없이 단독으로 만든다.
    /// Pack 없이 만든 Module은 항상 (0,0,0)에 생성된다. Pack 안의 Module은 Pack의 자식이며 Pack 내부 공간을 격자로 나눈 칸에 놓인다(Cell과 같은 규칙, Pack 크기는 변하지 않는다).
    /// 주어진 Pack이 이 팩토리에서 만든 것이 아니거나 Pack 내부에 놓을 칸이 없으면 생성하지 않고 null을 반환한다.
    /// </summary>
    public ModuleDataModel CreateModule(PackDataModel _pack, string _moduleName, GameObject _prefab = null, Vector3 _size = default, ContainerDataModel _container = null, Vector3? _bodyCenter = null)
    {
        LastFullMessage = string.Empty;
        Transform parent = null;
        Renderer packBody = null;

        if (_pack != null)
        {
            if (!registry.TryGetPackTransform(_pack, out parent))
            {
                DebugLogManager.GenerateErrorMessage<PackFactory>($"등록되지 않은 Pack입니다. Module : {_moduleName}");
                return null;
            }

            // Module이 붙기 전에 Pack 몸체를 찾아 둔다(자식 Module이 섞여 들어가지 않게).
            PackLayout.TryGetPackBody(_pack, parent, out packBody);
        }

        GameObject moduleObject = Spawn(_prefab, $"Module_{_moduleName}");
        // Pack 없이 만든 Module은 Container에 담긴다(주지 않으면 이 Module만 담는 새 Container).
        ContainerDataModel standaloneContainer = null;
        if (_pack == null)
        {
            standaloneContainer = _container != null ? _container : CreateContainer(_moduleName);
        }

        DeviceBody.AttachKeepingOwnScale(moduleObject.transform, parent != null ? parent : standaloneContainer.containerPosition);

        // 크기가 주어지면 칸을 찾기 전에 먼저 그 크기로 맞춘다(저장된 파일을 불러올 때).
        DeviceBody.Resize(moduleObject.transform, _size);

        if (_bodyCenter.HasValue)
        {
            // 저장된 몸체 중심이 주어지면(저장된 파일을 불러올 때) 칸을 찾지 않고 저장 당시 자리에 그대로 둔다.
            DeviceBody.AlignCenter(moduleObject.transform, _bodyCenter.Value);
        }
        else if (_pack != null)
        {
            Renderer moduleRenderer = moduleObject.GetComponentInChildren<Renderer>();
            if (packBody != null && moduleRenderer != null)
            {
                // Pack 크기는 그대로 두고, Cell과 같은 규칙으로 Pack 내부 공간의 칸에 놓는다.
                // Pack 크기를 바꾼 뒤에는 칸 배열이 달라져 개수만으로는 위치를 알 수 없으므로, 이미 놓인 Module과 겹치지 않는 첫 빈 칸을 찾는다.
                if (!PackLayout.TryFindFreeSlot(packBody.bounds, moduleRenderer.bounds.size, modulePadding, PackLayout.GetOccupiedBounds(_pack), parent, out Vector3 slotCenter, out int capacity))
                {
                    DebugLogManager.GenerateErrorMessage<PackFactory>($"Pack에 Module을 놓을 공간이 없습니다. Module : {_moduleName}, Pack : {_pack.packName}, 용량 : {capacity}개, 현재 : {_pack.modulesList.Count}개");
                    LastFullMessage = $"Pack {_pack.packName}에 더 이상 Module을 추가할 수 없습니다.";
                    moduleObject.transform.SetParent(null);
                    moduleObject.SetActive(false);
                    Object.Destroy(moduleObject);
                    return null;
                }

                // pivot이 Renderer 중심과 다를 수 있어 중심이 칸 중심에 오도록 pivot을 보정한다.
                moduleObject.transform.position += slotCenter - moduleRenderer.bounds.center;
            }
            else
            {
                // 측정할 Renderer가 없으면(빈 GameObject) 예전 방식으로 옆으로 밀다.
                moduleObject.transform.localPosition = new Vector3(_pack.modulesList.Count * ModuleSpacing, 0.0f, 0.0f);
            }
        }
        else
        {
            moduleObject.transform.position = Vector3.zero;
            PackGridPlacer.PlaceInContainer(moduleObject.transform, standaloneContainer.containerPosition, modulePadding);
        }

        ModuleDataModel module = new ModuleDataModel
        {
            moduleName = _moduleName,
            cellsList = new List<CellDataModel>(),
            modulePosition = moduleObject.transform,
            parentPack = _pack,
            parentContainer = standaloneContainer,
        };

        _pack?.modulesList.Add(module);
        standaloneContainer?.modulesList.Add(module);
        registry.AddModule(module);

        return module;
    }


    /// <summary>
    /// Module 아래에 Cell을 만든다. 이미 생성된 Module이 있어야만 만들 수 있으며,
    /// _module이 null이거나 이 팩토리에서 만든 Module이 아니면 생성하지 않고 null을 반환한다.
    /// Module 내부 공간이 가득 차서 더 놓을 칸이 없을 때도 생성하지 않고 null을 반환한다(칸 배치는 TryGetSlotCenter 참고).
    /// </summary>
    public CellDataModel CreateCell(ModuleDataModel _module, string _cellName, GameObject _prefab = null, Vector3 _size = default, Vector3? _bodyCenter = null)
    {
        LastFullMessage = string.Empty;
        if (_module == null)
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"Cell은 생성된 Module이 있어야 만들 수 있습니다. Cell : {_cellName}");
            return null;
        }

        if (!registry.ContainsModule(_module) || _module.modulePosition == null)
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"생성되지 않았거나 이미 제거된 Module입니다. Module : {_module.moduleName}, Cell : {_cellName}");
            return null;
        }

        // Cell이 붙기 전에 내부 공간을 구해 둔다(자식 Cell이 섞여 들어가지 않게).
        bool hasInner = PackLayout.TryGetModuleInnerBounds(_module, out Bounds inner);

        GameObject cellObject = Spawn(_prefab, $"Cell_{_cellName}");
        DeviceBody.AttachKeepingOwnScale(cellObject.transform, _module.modulePosition);

        // 크기가 주어지면 칸을 찾기 전에 먼저 그 크기로 맞춘다(저장된 파일을 불러올 때).
        DeviceBody.Resize(cellObject.transform, _size);

        Renderer cellRenderer = cellObject.GetComponentInChildren<Renderer>();
        if (_bodyCenter.HasValue)
        {
            // 저장된 몸체 중심이 주어지면(저장된 파일을 불러올 때) 칸을 찾지 않고 저장 당시 자리에 그대로 둔다.
            DeviceBody.AlignCenter(cellObject.transform, _bodyCenter.Value);
        }
        else if (hasInner && cellRenderer != null)
        {
            // Module 크기를 바꾼 뒤에는 칸 배열이 달라져 개수만으로는 위치를 알 수 없으므로, 이미 놓인 Cell과 겹치지 않는 첫 빈 칸을 찾는다.
            if (!PackLayout.TryFindFreeSlot(inner, cellRenderer.bounds.size, cellPadding, PackLayout.GetOccupiedBounds(_module), _module.modulePosition, out Vector3 slotCenter, out int capacity))
            {
                DebugLogManager.GenerateErrorMessage<PackFactory>($"Module에 Cell을 놓을 공간이 없습니다. Cell : {_cellName}, Module : {_module.moduleName}, 용량 : {capacity}개, 현재 : {_module.cellsList.Count}개");
                LastFullMessage = $"Module {_module.moduleName}에 더 이상 Cell을 추가할 수 없습니다.";
                cellObject.transform.SetParent(null);
                cellObject.SetActive(false);
                Object.Destroy(cellObject);
                return null;
            }

            // pivot이 Renderer 중심과 다를 수 있어 중심이 칸 중심에 오도록 pivot을 보정한다.
            cellObject.transform.position += slotCenter - cellRenderer.bounds.center;
        }
        else
        {
            // 측정할 Renderer가 없으면(빈 GameObject) 예전 방식으로 위로 쌓는다.
            cellObject.transform.localPosition = new Vector3(0.0f, _module.cellsList.Count * CellSpacing, 0.0f);
        }

        CellDataModel cell = new CellDataModel
        {
            cellName = _cellName,
            cellPosition = cellObject.transform,
            parentModule = _module,
        };

        _module.cellsList.Add(cell);

        if (!PackLayout.IsCellInsideModule(cell, out Vector3 overflow))
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"Cell이 Module 내부 공간을 벗어났습니다. Cell : {_cellName}, Module : {_module.moduleName}, 초과량(x,y,z) : {overflow}");
        }

        return cell;
    }
}
