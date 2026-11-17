using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PackFactory가 만든 Pack / ModuleAssembly / Module / ParallelAssembly / Cell의 등록부. 종류별 목록과 조회만 맡고 씬 오브젝트는 만들거나 지우지 않는다.
/// 부모와 자식 관계는 각 모델(Parent / Children)이 들고 있으므로 여기서는 "만들어진 순서"와 "씬 Transform으로 찾기"만 다룬다.
/// </summary>
public class PackRegistry
{
    private readonly Dictionary<DeviceType, List<IDeviceNode>> nodeDictionary = new Dictionary<DeviceType, List<IDeviceNode>>();

    // deviceId로 모델을 바로 찾기 위한 색인. deviceId가 비어 있는 모델은 넣지 않는다.
    private readonly Dictionary<string, IDeviceNode> deviceIdDictionary = new Dictionary<string, IDeviceNode>();

    public PackRegistry()
    {
        foreach (DeviceType kind in System.Enum.GetValues(typeof(DeviceType)))
        {
            nodeDictionary[kind] = new List<IDeviceNode>();
        }
    }

    /// <summary>_kind 종류로 등록된 모델(만들어진 순서).</summary>
    public IReadOnlyList<IDeviceNode> GetNodes(DeviceType _kind)
    {
        return nodeDictionary[_kind];
    }

    public bool Add(IDeviceNode _node)
    {
        if (_node == null || nodeDictionary[_node.Kind].Contains(_node))
        {
            return false;
        }

        nodeDictionary[_node.Kind].Add(_node);

        string deviceId = _node.Values != null ? _node.Values.deviceId : null;
        if (!string.IsNullOrEmpty(deviceId))
        {
            deviceIdDictionary[deviceId] = _node;
        }

        return true;
    }

    /// <summary>등록부에서만 뺀다(씬 오브젝트와 부모의 목록은 건드리지 않는다). 등록되어 있지 않았으면 false.</summary>
    public bool Remove(IDeviceNode _node)
    {
        if (_node == null || !nodeDictionary[_node.Kind].Remove(_node))
        {
            return false;
        }

        string deviceId = _node.Values != null ? _node.Values.deviceId : null;
        if (!string.IsNullOrEmpty(deviceId) && deviceIdDictionary.TryGetValue(deviceId, out IDeviceNode registered) && registered == _node)
        {
            deviceIdDictionary.Remove(deviceId);
        }

        return true;
    }

    public bool Contains(IDeviceNode _node)
    {
        return _node != null && nodeDictionary[_node.Kind].Contains(_node);
    }

    /// <summary>_kind 종류 중 가장 최근에 등록된 모델. 없으면 null.</summary>
    public IDeviceNode GetLast(DeviceType _kind)
    {
        List<IDeviceNode> list = nodeDictionary[_kind];
        return list.Count > 0 ? list[list.Count - 1] : null;
    }

    /// <summary>씬 Transform으로 모델을 찾는다. 등록되지 않았으면 null.</summary>
    public IDeviceNode Find(Transform _transform)
    {
        if (_transform == null)
        {
            return null;
        }

        foreach (List<IDeviceNode> list in nodeDictionary.Values)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Position == _transform)
                {
                    return list[i];
                }
            }
        }

        return null;
    }

    /// <summary>_deviceId로 등록된 모델. 비어 있거나 등록되지 않았으면 false.</summary>
    public bool TryGetByDeviceId(string _deviceId, out IDeviceNode _node)
    {
        _node = null;
        return !string.IsNullOrEmpty(_deviceId) && deviceIdDictionary.TryGetValue(_deviceId, out _node);
    }

    public bool ContainsDeviceId(string _deviceId)
    {
        return !string.IsNullOrEmpty(_deviceId) && deviceIdDictionary.ContainsKey(_deviceId);
    }

    public void Clear()
    {
        deviceIdDictionary.Clear();

        foreach (List<IDeviceNode> list in nodeDictionary.Values)
        {
            list.Clear();
        }
    }
}
