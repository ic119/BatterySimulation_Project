using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PackFactory가 만든 Container / Pack / Module의 등록부. 목록과 조회만 맡고 씬 오브젝트는 만들거나 지우지 않는다.
/// PackDataModel에는 Transform 필드가 없어서 Pack의 씬 오브젝트는 여기서 따로 들고 있는다.
/// Cell은 Module의 cellsList가 이미 들고 있으므로 등록하지 않는다.
/// </summary>
public class PackRegistry
{
    private readonly List<ContainerDataModel> containerList = new List<ContainerDataModel>();
    private readonly List<PackDataModel> packList = new List<PackDataModel>();
    private readonly List<ModuleDataModel> moduleList = new List<ModuleDataModel>();
    private readonly Dictionary<PackDataModel, Transform> packTransformDictionary = new Dictionary<PackDataModel, Transform>();

    public IReadOnlyList<ContainerDataModel> Containers => containerList;
    public IReadOnlyList<PackDataModel> Packs => packList;
    public IReadOnlyList<ModuleDataModel> Modules => moduleList;

    /// <summary>가장 최근에 등록된 Module. 없으면 null.</summary>
    public ModuleDataModel LastModule => moduleList.Count > 0 ? moduleList[moduleList.Count - 1] : null;

    /// <summary>가장 최근에 등록된 Pack. 없으면 null.</summary>
    public PackDataModel LastPack => packList.Count > 0 ? packList[packList.Count - 1] : null;

    #region Add
    public void AddContainer(ContainerDataModel _container)
    {
        containerList.Add(_container);
    }

    public void AddPack(PackDataModel _pack, Transform _packTransform)
    {
        packList.Add(_pack);
        packTransformDictionary[_pack] = _packTransform;
    }

    public void AddModule(ModuleDataModel _module)
    {
        moduleList.Add(_module);
    }
    #endregion

    #region Remove
    /// <summary>등록부에서만 뺀다(씬 오브젝트와 부모의 목록은 건드리지 않는다). 등록되어 있지 않았으면 false.</summary>
    public bool RemoveContainer(ContainerDataModel _container)
    {
        return containerList.Remove(_container);
    }

    /// <summary>등록부에서만 뺀다. 등록되어 있지 않았으면 false.</summary>
    public bool RemovePack(PackDataModel _pack)
    {
        return packList.Remove(_pack);
    }

    /// <summary>등록부에서만 뺀다. 등록되어 있지 않았으면 false.</summary>
    public bool RemoveModule(ModuleDataModel _module)
    {
        return moduleList.Remove(_module);
    }

    /// <summary>Pack의 씬 오브젝트 기록을 지우고 그 Transform을 돌려준다. 기록이 없었으면 false.</summary>
    public bool TryUnregisterPackTransform(PackDataModel _pack, out Transform _packTransform)
    {
        if (_pack == null || !packTransformDictionary.TryGetValue(_pack, out _packTransform))
        {
            _packTransform = null;
            return false;
        }

        packTransformDictionary.Remove(_pack);
        return true;
    }

    public void Clear()
    {
        containerList.Clear();
        packList.Clear();
        moduleList.Clear();
        packTransformDictionary.Clear();
    }
    #endregion

    #region Query
    public bool ContainsContainer(ContainerDataModel _container)
    {
        return containerList.Contains(_container);
    }

    public bool ContainsModule(ModuleDataModel _module)
    {
        return moduleList.Contains(_module);
    }

    /// <summary>Pack의 씬 Transform. 없으면 false.</summary>
    public bool TryGetPackTransform(PackDataModel _pack, out Transform _packTransform)
    {
        if (_pack == null)
        {
            _packTransform = null;
            return false;
        }

        return packTransformDictionary.TryGetValue(_pack, out _packTransform);
    }

    /// <summary>Pack의 씬 Transform. 없으면 null.</summary>
    public Transform GetPackTransform(PackDataModel _pack)
    {
        return TryGetPackTransform(_pack, out Transform packTransform) ? packTransform : null;
    }

    /// <summary>씬 Transform으로 Container를 찾는다. 등록되지 않았으면 null.</summary>
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

    /// <summary>씬 Transform으로 Pack을 찾는다. 등록되지 않았으면 null.</summary>
    public PackDataModel FindPack(Transform _packTransform)
    {
        if (_packTransform == null)
        {
            return null;
        }

        for (int i = 0; i < packList.Count; i++)
        {
            if (packTransformDictionary.TryGetValue(packList[i], out Transform packTransform) && packTransform == _packTransform)
            {
                return packList[i];
            }
        }

        return null;
    }

    /// <summary>씬 Transform으로 Module을 찾는다. 등록되지 않았으면 null.</summary>
    public ModuleDataModel FindModule(Transform _moduleTransform)
    {
        if (_moduleTransform == null)
        {
            return null;
        }

        for (int i = 0; i < moduleList.Count; i++)
        {
            if (moduleList[i].modulePosition == _moduleTransform)
            {
                return moduleList[i];
            }
        }

        return null;
    }
    #endregion
}
