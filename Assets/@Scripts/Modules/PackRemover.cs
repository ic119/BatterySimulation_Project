using UnityEngine;

/// <summary>
/// Pack / ModuleAssembly / Module / ParallelAssembly / Cell을 데이터 모델과 씬 오브젝트 양쪽에서 지운다. 지울 대상이 이 PackRegistry에 등록된 것이 아니면 아무것도 하지 않고 false.
/// 등록부 갱신, 부모 목록에서 빼기, 씬 오브젝트 파괴만 맡는다. 지운 뒤의 안내 문구 같은 팩토리의 상태는 PackFactory가 정리한다.
/// </summary>
public class PackRemover
{
    private readonly PackRegistry registry;

    public PackRemover(PackRegistry _registry)
    {
        registry = _registry;
    }

    /// <summary>_node를 그 아래 모든 자식과 함께 지운다(데이터 모델과 씬 오브젝트 모두). 부모의 목록에서도 뺀다.</summary>
    public bool Remove(IDeviceNode _node)
    {
        if (_node == null || !registry.Contains(_node))
        {
            return false;
        }

        UnregisterSubtree(_node);
        RemoveFromParent(_node);
        DestroyDetached(_node.Position);
        return true;
    }

    private void UnregisterSubtree(IDeviceNode _node)
    {
        registry.Remove(_node);
        foreach (IDeviceNode child in _node.Children)
        {
            UnregisterSubtree(child);
        }
    }

    private static void RemoveFromParent(IDeviceNode _node)
    {
        switch (_node)
        {
            case ModuleAssemblyDataModel moduleAssembly:
                moduleAssembly.parentPack?.moduleAssembliesList.Remove(moduleAssembly);
                break;
            case ModuleDataModel module:
                module.parentModuleAssembly?.modulesList.Remove(module);
                break;
            case ParallelAssemblyDataModel parallelAssembly:
                parallelAssembly.parentModule?.parallelAssembliesList.Remove(parallelAssembly);
                break;
            case CellDataModel cell:
                cell.parentParallelAssembly?.cellsList.Remove(cell);
                break;
        }
    }

    /// <summary>Destroy는 프레임 끝에야 반영되므로, 같은 프레임의 Bounds 계산에 섞이지 않게 먼저 떼어 내고 꺼 둔다.</summary>
    public static void DestroyDetached(Transform _target)
    {
        if (_target == null)
        {
            return;
        }

        _target.SetParent(null);
        _target.gameObject.SetActive(false);
        Object.Destroy(_target.gameObject);
    }
}
