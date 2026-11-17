using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Modules;
using Incheol.Utils;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pack / ModuleAssembly / Module / ParallelAssembly / Cell을 각각 독립적으로 생성하고 Pack > ModuleAssembly > Module > ParallelAssembly > Cell 계층을 관리한다.
/// 규칙: Pack을 뺀 모든 종류는 바로 위 종류의 부모가 이미 만들어져 있어야만 만들 수 있다. 각 모델의 Position(Transform)은 씬에 생성되는 GameObject이며 자식은 부모의 자식이 된다.
/// Pack은 Root 아래 한 줄(+x 방향)로 놓이고, 그 아래 종류들은 각각 부모 몸체 내부 공간을 격자로 나눈 칸에 놓이며 칸이 없으면 생성하지 않는다(부모 크기는 자동으로 변하지 않는다).
/// </summary>
public class PackFactory
{
    // Renderer가 없어 크기를 잴 수 없는 빈 GameObject일 때만 쓰는 예전 방식의 간격.
    private const float FallbackSpacing = 1.0f;

    // PackRoot의 부모가 될 MainScene 오브젝트 이름.
    private const string GeneratePosName = "GeneratePos";

    // 만든 항목의 목록과 조회는 registry가, 지우기는 remover가 맡는다. 이 클래스는 만들고 놓는 일에 집중한다.
    private readonly PackRegistry registry = new PackRegistry();
    private readonly PackRemover remover;
    private readonly Dictionary<DeviceType, float> paddingDictionary = new Dictionary<DeviceType, float>();
    private Transform root;

    public PackFactory()
    {
        remover = new PackRemover(registry);
    }

    /// <summary>_kind 종류로 만들어진 모델(만들어진 순서).</summary>
    public IReadOnlyList<IDeviceNode> GetNodes(DeviceType _kind)
    {
        return registry.GetNodes(_kind);
    }

    /// <summary>
    /// _kind 항목 사이의 가로(x)/깊이(z) 여백. 칸 크기 = 항목 크기 + 여백이고 항목은 칸 가운데에 놓이므로 부모 벽과도 여백/2만큼 떨어진다.
    /// Pack은 Root 아래에서 서로 이 여백만큼 떨어진다. 0 미만은 0으로 본다.
    /// </summary>
    public void SetPadding(DeviceType _kind, float _padding)
    {
        paddingDictionary[_kind] = Mathf.Max(0.0f, _padding);
    }

    private float GetPadding(DeviceType _kind)
    {
        return paddingDictionary.TryGetValue(_kind, out float padding) ? padding : 0.0f;
    }

    /// <summary>가장 최근의 Create가 "더 이상 놓을 공간이 없어서" 실패했을 때의 안내 문구. 그 밖의 경우(성공 포함)에는 빈 문자열.</summary>
    public string LastFullMessage { get; private set; } = string.Empty;

    /// <summary>_kind 종류 중 가장 최근에 만든 모델. 없으면 null.</summary>
    public IDeviceNode GetLast(DeviceType _kind)
    {
        return registry.GetLast(_kind);
    }

    /// <summary>
    /// PackRoot가 매달릴 부모(MainScene의 GeneratePos). 지정하지 않으면 PackRoot를 만들 때 이름이 GeneratePosName인 씬 오브젝트를 찾고,
    /// 그것도 없으면 PackRoot를 씬 최상위에 둔다. 이미 만들어진 PackRoot는 옮기지 않으므로 첫 생성 전에 지정한다.
    /// </summary>
    public Transform GenerateParent { get; set; }

    /// <summary>모든 Pack GameObject가 매달리는 루트. 처음 필요할 때 만든다.</summary>
    public Transform Root
    {
        get
        {
            if (root == null)
            {
                root = new GameObject("PackRoot").transform;

                Transform parent = GenerateParent;
                if (parent == null)
                {
                    GameObject found = GameObject.Find(GeneratePosName);
                    parent = found != null ? found.transform : null;
                }

                if (parent != null)
                {
                    // GeneratePos의 위치/회전/크기를 그대로 따라가도록 로컬 값을 초기화한다.
                    root.SetParent(parent, false);
                }
            }

            return root;
        }
    }

    // 새로 만드는 Pack을 놓는 기준 위치. GeneratePos가 있으면 그 위치, 없으면 (0,0,0).
    private Vector3 Origin => Root.position;

    /// <summary>만들어 둔 모든 장치를 씬 오브젝트까지 지우고 처음 상태로 되돌린다(저장된 파일을 불러오기 전에 쓴다).</summary>
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

    /// <summary>_node를 그 아래 모든 자식과 함께 지운다(데이터 모델과 씬 오브젝트 모두). 이 팩토리가 만든 것이 아니면 false.</summary>
    public bool Remove(IDeviceNode _node)
    {
        bool removed = remover.Remove(_node);
        if (removed)
        {
            // 무언가 지워졌으면 "공간이 없다"는 이전 안내 문구는 더 이상 맞지 않으므로 비운다.
            LastFullMessage = string.Empty;
        }

        return removed;
    }

    /// <summary>_node의 씬 Transform. 없으면 null.</summary>
    public Transform GetTransform(IDeviceNode _node)
    {
        return _node != null ? _node.Position : null;
    }

    /// <summary>씬 Transform으로 모델을 찾는다. 이 팩토리가 만든 것이 아니면 null.</summary>
    public IDeviceNode Find(Transform _transform)
    {
        return registry.Find(_transform);
    }

    /// <summary>React가 쓰는 deviceId로 모델을 찾는다. 비어 있거나 이 팩토리가 만든 것이 아니면 null.</summary>
    public IDeviceNode FindByDeviceId(string _deviceId)
    {
        return registry.TryGetByDeviceId(_deviceId, out IDeviceNode node) ? node : null;
    }

    /// <summary>_prefab이 있으면 인스턴스화하고, 없으면 빈 GameObject를 만든다.</summary>
    private static GameObject Spawn(GameObject _prefab, string _objectName)
    {
        GameObject spawned = _prefab != null ? Object.Instantiate(_prefab) : new GameObject();
        spawned.name = _objectName;
        return spawned;
    }

    /// <summary>
    /// _space 안에서 _occupied와 겹치지 않는 첫 빈 칸을 찾아 _item의 몸체 중심을 그 칸 중심에 맞춘다(pivot이 Renderer 중심과 다를 수 있어 pivot을 보정한다).
    /// 빈 칸이 없으면 아무것도 건드리지 않고 false를 돌려준다. 로그와 안내 문구, _item 정리는 부르는 쪽이 한다.
    /// </summary>
    private static bool TryPlaceInFreeSlot(Transform _item, Renderer _itemRenderer, Bounds _space, float _padding, List<Bounds> _occupied, Transform _parent, out int _capacity)
    {
        if (!PackLayout.TryFindFreeSlot(_space, _itemRenderer.bounds.size, _padding, _occupied, _parent, out Vector3 slotCenter, out _capacity))
        {
            return false;
        }

        _item.position += slotCenter - _itemRenderer.bounds.center;
        return true;
    }

    /// <summary>
    /// _kind 종류의 장치를 만들고 등록한다. Pack은 _parent가 null이어야 하고, 나머지는 바로 위 종류의 _parent(이 팩토리가 만든 것)가 있어야 한다.
    /// _prefab을 주면 그것을 인스턴스화한다. _size가 주어지면 칸을 찾기 전에 그 크기로 맞추고, _bodyCenter가 주어지면(저장된 파일을 불러올 때) 칸을 찾지 않고 저장 당시 자리에 그대로 둔다.
    /// 부모 몸체 안에 놓을 칸이 없으면 만들지 않고 null을 돌려준다(LastFullMessage에 안내 문구가 담긴다).
    /// </summary>
    public IDeviceNode Create(DeviceType _kind, IDeviceNode _parent, string _name, GameObject _prefab = null, Vector3 _size = default, Vector3? _bodyCenter = null, string _deviceId = null)
    {
        LastFullMessage = string.Empty;

        // deviceId는 React가 장치를 가리키는 열쇠라 겹치면 안 된다. 비어 있으면(Unity에서 직접 만든 장치) 검사하지 않는다.
        if (registry.ContainsDeviceId(_deviceId))
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"이미 같은 deviceId가 있어 {_kind}을(를) 만들지 않습니다. deviceId : {_deviceId}, {_kind} : {_name}");
            return null;
        }

        bool hasParentKind = DeviceHierarchy.TryGetParentKind(_kind, out DeviceType parentKind);
        if (hasParentKind)
        {
            if (_parent == null || _parent.Kind != parentKind || !registry.Contains(_parent) || _parent.Position == null)
            {
                DebugLogManager.GenerateErrorMessage<PackFactory>($"{_kind}은(는) 생성된 {parentKind}이(가) 있어야 만들 수 있습니다. {_kind} : {_name}");
                return null;
            }
        }
        else if (_parent != null)
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"{_kind}은(는) 부모를 가질 수 없습니다. {_kind} : {_name}");
            return null;
        }

        Transform parentTransform = hasParentKind ? _parent.Position : Root;

        // 자식이 붙기 전에 부모의 내부 공간을 구해 둔다(자식 Renderer가 섞여 들어가지 않게).
        bool hasInner = false;
        Bounds inner = default;
        if (hasParentKind)
        {
            hasInner = PackLayout.TryGetInnerBounds(parentTransform, out inner);
        }

        GameObject itemObject = Spawn(_prefab, $"{_kind}_{_name}");
        DeviceBody.AttachKeepingOwnScale(itemObject.transform, parentTransform);
        if (!hasParentKind)
        {
            itemObject.transform.position = Origin;
        }

        DeviceBody.Resize(itemObject.transform, _size);

        Renderer itemRenderer = itemObject.GetComponentInChildren<Renderer>();
        if (_bodyCenter.HasValue)
        {
            DeviceBody.AlignCenter(itemObject.transform, _bodyCenter.Value);
        }
        else if (!hasParentKind)
        {
            PackGridPlacer.PlaceInRoot(itemObject.transform, parentTransform, GetPadding(_kind));
        }
        else if (hasInner && itemRenderer != null)
        {
            // 부모 크기를 바꾼 뒤에는 칸 배열이 달라져 개수만으로는 위치를 알 수 없으므로, 이미 놓인 자식과 겹치지 않는 첫 빈 칸을 찾는다.
            List<Bounds> occupied = PackLayout.GetOccupiedBounds(parentTransform, itemObject.transform);
            if (!TryPlaceInFreeSlot(itemObject.transform, itemRenderer, inner, GetPadding(_kind), occupied, parentTransform, out int capacity))
            {
                DebugLogManager.GenerateErrorMessage<PackFactory>($"{_parent.Kind}에 {_kind}을(를) 놓을 공간이 없습니다. {_kind} : {_name}, {_parent.Kind} : {_parent.Name}, 용량 : {capacity}개, 현재 : {_parent.Children.Count}개");
                LastFullMessage = $"{_parent.Kind} {_parent.Name}에 더 이상 {_kind}을(를) 추가할 수 없습니다.";
                PackRemover.DestroyDetached(itemObject.transform);
                return null;
            }
        }
        else
        {
            // 측정할 Renderer가 없으면(빈 GameObject) 예전 방식으로 옆으로 밀어 놓는다.
            itemObject.transform.localPosition = new Vector3(_parent != null ? _parent.Children.Count * FallbackSpacing : 0.0f, 0.0f, 0.0f);
        }

        IDeviceNode node = NewNode(_kind, _parent, _name, itemObject.transform);
        node.Values.deviceId = _deviceId;
        registry.Add(node);
        node.Values.Capture(itemObject.transform);

        if (hasParentKind && !PackLayout.IsInsideParent(itemObject.transform, out Vector3 overflow))
        {
            DebugLogManager.GenerateErrorMessage<PackFactory>($"{_kind}이(가) {_parent.Kind} 내부 공간을 벗어났습니다. {_kind} : {_name}, {_parent.Kind} : {_parent.Name}, 초과량(x,y,z) : {overflow}");
        }

        return node;
    }

    /// <summary>만들어 둔 모든 장치 모델의 값(위치, 크기, 몸체 중심, 자식 간격)을 씬의 현재 상태로 다시 채운다. 크기나 간격을 바꾼 뒤, 저장하기 전에 부른다.</summary>
    public void RefreshValues()
    {
        foreach (DeviceType kind in System.Enum.GetValues(typeof(DeviceType)))
        {
            IReadOnlyList<IDeviceNode> nodes = GetNodes(kind);
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Values.Capture(nodes[i].Position);
            }
        }
    }

    /// <summary>_kind에 맞는 모델을 만들어 _parent의 자식 목록에 넣는다.</summary>
    private static IDeviceNode NewNode(DeviceType _kind, IDeviceNode _parent, string _name, Transform _position)
    {
        switch (_kind)
        {
            case DeviceType.ModuleAssembly:
                {
                    ModuleAssemblyDataModel moduleAssembly = new ModuleAssemblyDataModel { moduleAssemblyName = _name, moduleAssemblyPosition = _position, parentPack = (PackDataModel)_parent };
                    moduleAssembly.parentPack.moduleAssembliesList.Add(moduleAssembly);
                    return moduleAssembly;
                }
            case DeviceType.Module:
                {
                    ModuleDataModel module = new ModuleDataModel { moduleName = _name, modulePosition = _position, parentModuleAssembly = (ModuleAssemblyDataModel)_parent };
                    module.parentModuleAssembly.modulesList.Add(module);
                    return module;
                }
            case DeviceType.ParallelAssembly:
                {
                    ParallelAssemblyDataModel parallelAssembly = new ParallelAssemblyDataModel { parallelAssemblyName = _name, parallelAssemblyPosition = _position, parentModule = (ModuleDataModel)_parent };
                    parallelAssembly.parentModule.parallelAssembliesList.Add(parallelAssembly);
                    return parallelAssembly;
                }
            case DeviceType.Cell:
                {
                    CellDataModel cell = new CellDataModel { cellName = _name, cellPosition = _position, parentParallelAssembly = (ParallelAssemblyDataModel)_parent };
                    cell.parentParallelAssembly.cellsList.Add(cell);
                    return cell;
                }
            default:
                return new PackDataModel { packName = _name, packPosition = _position };
        }
    }
}
