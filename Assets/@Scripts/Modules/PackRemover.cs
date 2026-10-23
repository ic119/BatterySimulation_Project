using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container / Pack / Module / Cell을 데이터 모델과 씬 오브젝트 양쪽에서 지운다. 지울 대상이 이 PackRegistry에 등록된 것이 아니면 아무것도 하지 않고 false.
/// 등록부 갱신, 부모 목록에서 빼기, 씬 오브젝트 파괴만 맡는다. 지운 뒤의 안내 문구 같은 팩토리의 상태는 PackFactory가 정리한다.
/// </summary>
public class PackRemover
{
    private readonly PackRegistry registry;

    public PackRemover(PackRegistry _registry)
    {
        registry = _registry;
    }

    /// <summary>Pack을 그 안의 Module / Cell과 함께 지운다(데이터 모델과 씬 오브젝트 모두).</summary>
    public bool RemovePack(PackDataModel _pack)
    {
        if (_pack == null || !registry.RemovePack(_pack))
        {
            return false;
        }

        for (int i = 0; i < _pack.modulesList.Count; i++)
        {
            registry.RemoveModule(_pack.modulesList[i]);
        }

        if (registry.TryUnregisterPackTransform(_pack, out Transform packTransform))
        {
            DestroyDetached(packTransform);
        }

        _pack.parentContainer?.packsList.Remove(_pack);
        return true;
    }

    /// <summary>Module을 그 안의 Cell과 함께 지운다. 속한 Pack이나 Container가 있으면 그 목록에서도 뺀다.</summary>
    public bool RemoveModule(ModuleDataModel _module)
    {
        if (_module == null || !registry.RemoveModule(_module))
        {
            return false;
        }

        _module.parentPack?.modulesList.Remove(_module);
        _module.parentContainer?.modulesList.Remove(_module);
        DestroyDetached(_module.modulePosition);
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
        return true;
    }

    /// <summary>Container를 그 안의 Pack / 단독 Module / Cell과 함께 지운다.</summary>
    public bool RemoveContainer(ContainerDataModel _container)
    {
        if (_container == null || !registry.ContainsContainer(_container))
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

        registry.RemoveContainer(_container);
        DestroyDetached(_container.containerPosition);
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
}
