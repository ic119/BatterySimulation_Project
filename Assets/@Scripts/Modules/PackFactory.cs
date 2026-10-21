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

    private readonly List<PackDataModel> packs = new List<PackDataModel>();
    private readonly List<ModuleDataModel> allModules = new List<ModuleDataModel>();
    private readonly Dictionary<PackDataModel, Transform> packTransforms = new Dictionary<PackDataModel, Transform>();
    private readonly List<ContainerDataModel> containerList = new List<ContainerDataModel>();
    private Transform root;
    private float packPadding;
    private float modulePadding;
    private float cellPadding;

    public IReadOnlyList<PackDataModel> Packs => packs;
    public IReadOnlyList<ModuleDataModel> Modules => allModules;
    public IReadOnlyList<ContainerDataModel> Containers => containerList;

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
    public bool CanCreateCell => allModules.Count > 0;

    /// <summary>가장 최근의 CreateModule / CreateCell이 "더 이상 놓을 공간이 없어서" 실패했을 때의 안내 문구. 그 밖의 경우(성공 포함)에는 빈 문자열.</summary>
    public string LastFullMessage { get; private set; } = string.Empty;

    /// <summary>가장 최근에 생성된 Module. 없으면 null.</summary>
    public ModuleDataModel LastModule => allModules.Count > 0 ? allModules[allModules.Count - 1] : null;

    /// <summary>가장 최근에 생성된 Pack. 없으면 null.</summary>
    public PackDataModel LastPack => packs.Count > 0 ? packs[packs.Count - 1] : null;

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

        packs.Clear();
        allModules.Clear();
        packTransforms.Clear();
        containerList.Clear();
        LastFullMessage = string.Empty;
    }

    /// <summary>Pack을 그 안의 Module / Cell과 함께 지운다(데이터 모델과 씬 오브젝트 모두). 이 팩토리가 만든 Pack이 아니면 false.</summary>
    public bool RemovePack(PackDataModel _pack)
    {
        if (_pack == null || !packs.Remove(_pack))
        {
            return false;
        }

        for (int i = 0; i < _pack.modulesList.Count; i++)
        {
            allModules.Remove(_pack.modulesList[i]);
        }

        if (packTransforms.TryGetValue(_pack, out Transform packTransform))
        {
            packTransforms.Remove(_pack);
            DestroyDetached(packTransform);
        }

        _pack.parentContainer?.packsList.Remove(_pack);

        LastFullMessage = string.Empty;
        return true;
    }

    /// <summary>Module을 그 안의 Cell과 함께 지운다. 속한 Pack이 있으면 그 목록에서도 뺀다. 이 팩토리가 만든 Module이 아니면 false.</summary>
    public bool RemoveModule(ModuleDataModel _module)
    {
        if (_module == null || !allModules.Remove(_module))
        {
            return false;
        }

        _module.parentPack?.modulesList.Remove(_module);
        _module.parentContainer?.modulesList.Remove(_module);
        DestroyDetached(_module.modulePosition);
        LastFullMessage = string.Empty;
        return true;
    }

    /// <summary>Cell을 지운다. 속한 Module의 목록에서도 뺀다. 이미 지워진 Cell이면 false.</summary>
    public bool RemoveCell(CellDataModel _cell)
    {
        if (_cell == null || _cell.parentModule == null || !_cell.parentModule.cellsList.Remove(_cell))
        {
            return false;
        }

        DestroyDetached(_cell.cellPosition);
        LastFullMessage = string.Empty;
        return true;
    }

    /// <summary>Destroy는 프레임 끝에야 반영되므로, 같은 프레임의 Bounds 계산에 섞이지 않게 먼저 떼어 내고 꺼 둔다.</summary>
    private static void DestroyDetached(Transform _target)
    {
        if (_target == null)
        {
            return;
        }

        _target.SetParent(null);
        _target.gameObject.SetActive(false);
        Object.Destroy(_target.gameObject);
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

        containerList.Add(container);
        return container;
    }

    /// <summary>Container를 그 안의 Pack / 단독 Module / Cell과 함께 지운다. 이 팩토리가 만든 Container가 아니면 false.</summary>
    public bool RemoveContainer(ContainerDataModel _container)
    {
        if (_container == null || !containerList.Contains(_container))
        {
            return false;
        }

        // 지우는 동안 목록이 바뀌므로 복사해서 돈다.
        foreach (PackDataModel pack in new List<PackDataModel>(_container.packsList))
        {
            RemovePack(pack);
        }

        foreach (ModuleDataModel module in new List<ModuleDataModel>(_container.modulesList))
        {
            RemoveModule(module);
        }

        containerList.Remove(_container);
        DestroyDetached(_container.containerPosition);
        LastFullMessage = string.Empty;
        return true;
    }

    /// <summary>씬 Transform으로 Container를 찾는다. 이 팩토리가 만든 Container가 아니면 null.</summary>
    public ContainerDataModel FindContainer(Transform _containerTransform)
    {
        if (_containerTransform == null)
        {
            return null;
        }

        for (int i = 0; i < containerList.Count; i++)
        {
            if (containerList[i].containerPosition == _containerTransform)
            {
                return containerList[i];
            }
        }

        return null;
    }

    /// <summary>Pack의 씬 Transform. PackDataModel에는 Transform 필드가 없어 따로 들고 있는 것을 꺼낸다. 없으면 null.</summary>
    public Transform GetPackTransform(PackDataModel _pack)
    {
        return _pack != null && packTransforms.TryGetValue(_pack, out Transform packTransform) ? packTransform : null;
    }

    /// <summary>씬 Transform으로 Pack을 찾는다. 이 팩토리가 만든 Pack이 아니면 null.</summary>
    public PackDataModel FindPack(Transform _packTransform)
    {
        if (_packTransform == null)
        {
            return null;
        }

        for (int i = 0; i < packs.Count; i++)
        {
            if (packTransforms.TryGetValue(packs[i], out Transform packTransform) && packTransform == _packTransform)
            {
                return packs[i];
            }
        }

        return null;
    }

    /// <summary>씬 Transform으로 Module을 찾는다. 이 팩토리가 만든 Module이 아니면 null.</summary>
    public ModuleDataModel FindModule(Transform _moduleTransform)
    {
        if (_moduleTransform == null)
        {
            return null;
        }

        for (int i = 0; i < allModules.Count; i++)
        {
            if (allModules[i].modulePosition == _moduleTransform)
            {
                return allModules[i];
            }
        }

        return null;
    }

    /// <summary>_prefab이 있으면 인스턴스화하고, 없으면 빈 GameObject를 만든다.</summary>
    private static GameObject Spawn(GameObject _prefab, string _objectName)
    {
        GameObject spawned = _prefab != null ? Object.Instantiate(_prefab) : new GameObject();
        spawned.name = _objectName;
        return spawned;
    }

    /// <summary>
    /// _child를 _parent 아래에 붙이되, 부모의 크기(스케일)가 곱해지지 않게 한다.
    /// 붙이기 전 _child의 스케일(= 프리팹 본래 크기)이 월드 스케일이 되도록 로컬 스케일을 부모 스케일로 나눠 보정한다.
    /// </summary>
    private static void AttachKeepingOwnScale(Transform _child, Transform _parent)
    {
        Vector3 ownScale = _child.localScale;
        _child.SetParent(_parent, false);

        Vector3 parentScale = _parent.lossyScale;
        _child.localScale = new Vector3(
            DivideSafe(ownScale.x, parentScale.x),
            DivideSafe(ownScale.y, parentScale.y),
            DivideSafe(ownScale.z, parentScale.z));
    }

    /// <summary>
    /// _target의 크기를 월드 크기 _size로 바꾼다. 크기의 어느 축이든 0 이하이면(= 지정하지 않음) 아무것도 하지 않는다.
    /// Pack / Module처럼 자식 Body가 크기를 가지면 Body의 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 바꾸고(Scale 패널과 같은 규칙),
    /// Cell처럼 본인이 몸체면 중심을 유지한 채 스케일을 바꾼다. 몸체는 단위 크기 메시(Cube 등)라는 전제다.
    /// </summary>
    private static void ResizeBody(Transform _target, Vector3 _size)
    {
        if (_target == null || _size.x <= 0.0f || _size.y <= 0.0f || _size.z <= 0.0f)
        {
            return;
        }

        Transform body = _target.Find("Body");
        if (body != null && body.TryGetComponent(out Renderer bodyRenderer))
        {
            Bounds current = bodyRenderer.bounds;
            Vector3 parentScale = _target.lossyScale;
            body.localScale = new Vector3(
                DivideSafe(_size.x, parentScale.x),
                DivideSafe(_size.y, parentScale.y),
                DivideSafe(_size.z, parentScale.z));
            body.position = new Vector3(
                current.min.x + _size.x * 0.5f,
                current.min.y + _size.y * 0.5f,
                current.max.z - _size.z * 0.5f);
            return;
        }

        if (_target.TryGetComponent(out Renderer ownRenderer))
        {
            Bounds current = ownRenderer.bounds;
            Vector3 scale = _target.localScale;
            _target.localScale = new Vector3(
                current.size.x > 0.0f ? scale.x * _size.x / current.size.x : scale.x,
                current.size.y > 0.0f ? scale.y * _size.y / current.size.y : scale.y,
                current.size.z > 0.0f ? scale.z * _size.z / current.size.z : scale.z);
            _target.position += current.center - ownRenderer.bounds.center;
        }
    }

    /// <summary>
    /// _item(Container 바로 아래의 Pack / 단독 Module)을 같은 Container에 이미 놓인 항목과 겹치지 않는 첫 자리로 옮긴다(Module / Cell을 놓을 때와 같은 겹침 검사).
    /// 첫 항목이거나 Renderer가 없어 크기를 잴 수 없으면 옮기지 않는다.
    /// </summary>
    private static void PlaceInContainer(Transform _item, Transform _container, float _padding)
    {
        Renderer[] rendererArray = _item.GetComponentsInChildren<Renderer>(true);
        if (rendererArray.Length == 0)
        {
            return;
        }

        Bounds own = rendererArray[0].bounds;
        for (int i = 1; i < rendererArray.Length; i++)
        {
            own.Encapsulate(rendererArray[i].bounds);
        }

        List<Bounds> occupied = PackLayout.GetOccupiedBounds(_container, _item);
        if (occupied.Count > 0 && PackLayout.TryFindFreeOffset(own, _padding, occupied, out Vector3 offset))
        {
            _item.position += offset;
        }
    }

    /// <summary>
    /// _packTransform(방금 만든 Pack)을 Container의 x(_columns) * z(_rows) 격자 칸에 놓고 칸(열, 줄)을 _pack에 기록한다.
    /// 칸의 위치는 (열, 줄)로만 정해지고(+x로 열, -z로 줄) 다른 Pack의 Renderer bounds는 보지 않는다.
    /// 칸 크기는 Pack 크기 + 축별 간격이며, Container에 사용자가 조절한 간격(SubDeviceSpacingData)이 있으면 그 축은 여백 대신 그 간격을 쓴다.
    /// 칸을 기록한 적 없는 Pack(격자를 정하기 전에 만든 Pack, 예전 파일의 Pack)은 현재 위치를 칸 크기로 나눠 어느 칸인지 구해 그 칸을 차지한 것으로 본다.
    /// (_requestedColumn, _requestedRow)가 비어 있으면 그 칸에(저장된 파일을 불러올 때), 아니면 첫 줄부터 x 방향으로 훑어 가장 먼저 나오는 빈 칸에 놓는다.
    /// 빈 칸이 없으면 false.
    /// </summary>
    private bool PlaceInGridSlot(PackDataModel _pack, Transform _packTransform, ContainerDataModel _container, int _columns, int _rows, int _requestedColumn, int _requestedRow)
    {
        _pack.slotColumn = -1;
        _pack.slotRow = -1;

        Renderer[] rendererArray = _packTransform.GetComponentsInChildren<Renderer>(true);
        if (_columns <= 0 || _rows <= 0 || rendererArray.Length == 0)
        {
            return false;
        }

        Bounds own = rendererArray[0].bounds;
        for (int i = 1; i < rendererArray.Length; i++)
        {
            own.Encapsulate(rendererArray[i].bounds);
        }

        Vector3 size = own.size;

        float paddingX = packPadding;
        float paddingZ = packPadding;
        if (_container.containerPosition.TryGetComponent(out SubDeviceSpacingData data))
        {
            paddingX = data.TryGet(0, out float spacingX) ? spacingX : paddingX;
            paddingZ = data.TryGet(2, out float spacingZ) ? spacingZ : paddingZ;
        }

        float pitchX = size.x + paddingX;
        float pitchZ = size.z + paddingZ;
        if (pitchX <= 0.0f || pitchZ <= 0.0f)
        {
            return false;
        }

        bool[,] used = new bool[_columns, _rows];
        for (int i = 0; i < _container.packsList.Count; i++)
        {
            PackDataModel other = _container.packsList[i];
            if (other == _pack)
            {
                continue;
            }

            // 칸을 기록한 적 없으면 지금 위치에서 칸을 구해 기록해 둔다(첫 Pack은 원점이라 0열 0줄).
            if (other.slotColumn < 0 || other.slotRow < 0)
            {
                Transform otherTransform = GetPackTransform(other);
                if (otherTransform != null)
                {
                    other.slotColumn = Mathf.Max(0, Mathf.RoundToInt(otherTransform.position.x / pitchX));
                    other.slotRow = Mathf.Max(0, Mathf.RoundToInt(-otherTransform.position.z / pitchZ));
                }
            }

            if (other.slotColumn >= 0 && other.slotColumn < _columns && other.slotRow >= 0 && other.slotRow < _rows)
            {
                used[other.slotColumn, other.slotRow] = true;
            }
        }

        int column = -1;
        int row = -1;
        if (_requestedColumn >= 0 && _requestedColumn < _columns && _requestedRow >= 0 && _requestedRow < _rows && !used[_requestedColumn, _requestedRow])
        {
            column = _requestedColumn;
            row = _requestedRow;
        }

        for (int r = 0; column < 0 && r < _rows; r++)
        {
            for (int c = 0; column < 0 && c < _columns; c++)
            {
                if (!used[c, r])
                {
                    column = c;
                    row = r;
                }
            }
        }

        if (column < 0)
        {
            return false;
        }

        _packTransform.position += new Vector3(pitchX * column, 0.0f, -pitchZ * row);
        _pack.slotColumn = column;
        _pack.slotRow = row;
        return true;
    }

    /// <summary>
    /// _target의 몸체(Pack / Module은 자식 Body, Cell은 본인의 Renderer) 중심이 월드 좌표 _center에 오도록 _target 전체를 옮긴다.
    /// 몸체 Renderer를 찾지 못하면 아무것도 하지 않는다.
    /// </summary>
    private static void AlignBodyCenter(Transform _target, Vector3 _center)
    {
        Transform body = _target.Find("Body");
        Renderer bodyRenderer = null;
        if ((body == null || !body.TryGetComponent(out bodyRenderer)) && !_target.TryGetComponent(out bodyRenderer))
        {
            return;
        }

        _target.position += _center - bodyRenderer.bounds.center;
    }

    private static float DivideSafe(float _value, float _divisor)
    {
        return Mathf.Approximately(_divisor, 0.0f) ? _value : _value / _divisor;
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
        AttachKeepingOwnScale(packObject.transform, container.containerPosition);
        packObject.transform.position = Vector3.zero;
        ResizeBody(packObject.transform, _size);

        bool placed = _columns > 0 && _rows > 0 && PlaceInGridSlot(pack, packObject.transform, container, _columns, _rows, _slotColumn, _slotRow);
        if (!placed)
        {
            PlaceInContainer(packObject.transform, container.containerPosition, packPadding);
        }

        // 저장된 몸체 중심이 주어지면(저장된 파일을 불러올 때) 칸 위치 대신 저장 당시 자리에 그대로 둔다.
        if (_bodyCenter.HasValue)
        {
            AlignBodyCenter(packObject.transform, _bodyCenter.Value);
        }

        packs.Add(pack);
        packTransforms[pack] = packObject.transform;

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
            if (!packTransforms.TryGetValue(_pack, out parent))
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

        AttachKeepingOwnScale(moduleObject.transform, parent != null ? parent : standaloneContainer.containerPosition);

        // 크기가 주어지면 칸을 찾기 전에 먼저 그 크기로 맞춘다(저장된 파일을 불러올 때).
        ResizeBody(moduleObject.transform, _size);

        if (_bodyCenter.HasValue)
        {
            // 저장된 몸체 중심이 주어지면(저장된 파일을 불러올 때) 칸을 찾지 않고 저장 당시 자리에 그대로 둔다.
            AlignBodyCenter(moduleObject.transform, _bodyCenter.Value);
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
            PlaceInContainer(moduleObject.transform, standaloneContainer.containerPosition, modulePadding);
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
        allModules.Add(module);

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

        if (!allModules.Contains(_module) || _module.modulePosition == null)
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"생성되지 않았거나 이미 제거된 Module입니다. Module : {_module.moduleName}, Cell : {_cellName}");
            return null;
        }

        // Cell이 붙기 전에 내부 공간을 구해 둔다(자식 Cell이 섞여 들어가지 않게).
        bool hasInner = PackLayout.TryGetModuleInnerBounds(_module, out Bounds inner);

        GameObject cellObject = Spawn(_prefab, $"Cell_{_cellName}");
        AttachKeepingOwnScale(cellObject.transform, _module.modulePosition);

        // 크기가 주어지면 칸을 찾기 전에 먼저 그 크기로 맞춘다(저장된 파일을 불러올 때).
        ResizeBody(cellObject.transform, _size);

        Renderer cellRenderer = cellObject.GetComponentInChildren<Renderer>();
        if (_bodyCenter.HasValue)
        {
            // 저장된 몸체 중심이 주어지면(저장된 파일을 불러올 때) 칸을 찾지 않고 저장 당시 자리에 그대로 둔다.
            AlignBodyCenter(cellObject.transform, _bodyCenter.Value);
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
