using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Modules;
using System;
using UnityEngine;

/// <summary>
/// 저장된 Json(JsonDataController.SaveJson)의 Pack > ModuleAssembly > Module > ParallelAssembly > Cell을 PackFactory로 다시 만든다. UI는 알지 못하고, 만들어진 것은 콜백으로 알린다.
/// 저장된 이름, 자식 구조, 크기, 몸체 중심을 써서 저장 당시 크기와 자리에 그대로 놓고, 각 항목의 자식 간격도 되돌린다.
/// 부모가 가득 차 놓을 곳이 없는 항목은 직접 만들 때처럼 만들어지지 않고 건너뛴 개수로 센다(그 안의 자식도 건너뛴다).
/// </summary>
public class PackJsonBuilder
{
    /// <summary>불러온 뒤의 종류별 항목 번호와 건너뛴 개수. 번호는 이름이 "3"처럼 번호 문자열인 것 중 가장 큰 값이다.</summary>
    public struct Result
    {
        public int packCount;
        public int moduleAssemblyCount;
        public int moduleCount;
        public int parallelAssemblyCount;
        public int cellCount;
        public int skippedCount;
    }

    private readonly PackFactory packFactory;
    private readonly Action<IDeviceNode> nodeCreated;
    private Result result;

    /// <param name="_nodeCreated">장치가 만들어질 때마다(부모가 먼저, 자식이 나중 순서).</param>
    public PackJsonBuilder(PackFactory _packFactory, Action<IDeviceNode> _nodeCreated)
    {
        packFactory = _packFactory;
        nodeCreated = _nodeCreated;
    }

    /// <summary>
    /// _data의 Pack과 그 아래 모든 항목을 차례로 만든다. 이미 있는 것은 지우지 않으므로 호출하기 전에 비워 둔다.
    /// 현재 저장 형식이 아닌 _data이면 아무것도 만들지 않는다. 종류별 프리팹은 _prefabs[종류]로 넘긴다.
    /// </summary>
    public Result Build(JsonDataController.SaveJson _data, System.Collections.Generic.IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
    {
        result = default;
        if (_data == null || _data.version != JsonDataController.CurrentVersion)
        {
            return result;
        }

        for (int p = 0; p < _data.packs.Count; p++)
        {
            JsonDataController.PackJson packJson = _data.packs[p];
            IDeviceNode pack = CreateNode(DeviceType.Pack, null, packJson, _prefabs);
            if (pack == null)
            {
                result.skippedCount += CountAll(packJson);
                continue;
            }

            for (int a = 0; a < packJson.moduleAssemblies.Count; a++)
            {
                BuildModuleAssembly(pack, packJson.moduleAssemblies[a], _prefabs);
            }

            RestoreSpacing(pack.Position, packJson.childSpacing);
        }

        return result;
    }

    private void BuildModuleAssembly(IDeviceNode _pack, JsonDataController.ModuleAssemblyJson _json, System.Collections.Generic.IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
    {
        IDeviceNode moduleAssembly = CreateNode(DeviceType.ModuleAssembly, _pack, _json, _prefabs);
        if (moduleAssembly == null)
        {
            result.skippedCount += CountAll(_json);
            return;
        }

        for (int i = 0; i < _json.modules.Count; i++)
        {
            BuildModule(moduleAssembly, _json.modules[i], _prefabs);
        }

        RestoreSpacing(moduleAssembly.Position, _json.childSpacing);
    }

    private void BuildModule(IDeviceNode _moduleAssembly, JsonDataController.ModuleJson _json, System.Collections.Generic.IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
    {
        IDeviceNode module = CreateNode(DeviceType.Module, _moduleAssembly, _json, _prefabs);
        if (module == null)
        {
            result.skippedCount += CountAll(_json);
            return;
        }

        for (int i = 0; i < _json.parallelAssemblies.Count; i++)
        {
            BuildParallelAssembly(module, _json.parallelAssemblies[i], _prefabs);
        }

        RestoreSpacing(module.Position, _json.childSpacing);
    }

    private void BuildParallelAssembly(IDeviceNode _module, JsonDataController.ParallelAssemblyJson _json, System.Collections.Generic.IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
    {
        IDeviceNode parallelAssembly = CreateNode(DeviceType.ParallelAssembly, _module, _json, _prefabs);
        if (parallelAssembly == null)
        {
            result.skippedCount += CountAll(_json);
            return;
        }

        for (int i = 0; i < _json.cells.Count; i++)
        {
            if (CreateNode(DeviceType.Cell, parallelAssembly, _json.cells[i], _prefabs) == null)
            {
                result.skippedCount++;
            }
        }

        RestoreSpacing(parallelAssembly.Position, _json.childSpacing);
    }

    /// <summary>_json 한 항목을 PackFactory로 만들고 알린다. 못 만들면 null.</summary>
    private IDeviceNode CreateNode(DeviceType _kind, IDeviceNode _parent, JsonDataController.NodeJson _json, System.Collections.Generic.IReadOnlyDictionary<DeviceType, GameObject> _prefabs)
    {
        _prefabs.TryGetValue(_kind, out GameObject prefab);
        Vector3? bodyCenter = _json.hasBodyCenter ? _json.bodyCenter : (Vector3?)null;
        IDeviceNode node = packFactory.Create(_kind, _parent, _json.name, prefab, _json.size, bodyCenter);
        if (node == null)
        {
            return null;
        }

        // 몸체 중심이 없는 Pack은 저장된 pivot 위치에 둔다. 몸체 중심이 있으면 Create가 저장 당시 몸체 자리에 이미 놓았고, Pack 아래 항목은 칸에 놓인 자리를 그대로 둔다.
        if (!_json.hasBodyCenter && _kind == DeviceType.Pack && node.Position != null)
        {
            node.Position.position = _json.position;
        }

        nodeCreated?.Invoke(node);
        SetMax(_kind, _json.name);
        return node;
    }

    /// <summary>이름이 "3"처럼 번호 문자열이면 종류별 최대 번호에 반영한다. 불러온 뒤 새로 만드는 항목의 번호가 겹치지 않게 이어 붙이는 데 쓴다.</summary>
    private void SetMax(DeviceType _kind, string _name)
    {
        int number = int.TryParse(_name, out int parsed) ? parsed : 0;
        switch (_kind)
        {
            case DeviceType.Pack:
                result.packCount = Mathf.Max(result.packCount, number);
                break;
            case DeviceType.ModuleAssembly:
                result.moduleAssemblyCount = Mathf.Max(result.moduleAssemblyCount, number);
                break;
            case DeviceType.Module:
                result.moduleCount = Mathf.Max(result.moduleCount, number);
                break;
            case DeviceType.ParallelAssembly:
                result.parallelAssemblyCount = Mathf.Max(result.parallelAssemblyCount, number);
                break;
            case DeviceType.Cell:
                result.cellCount = Mathf.Max(result.cellCount, number);
                break;
        }
    }

    /// <summary>
    /// 저장된 자식 간격(applied가 true인 축)을 _target에 기억시킨다. 이후 새로 만드는 자식이 같은 간격으로 놓이도록 하는 용도이며,
    /// 먼저 기억시키면 자식이 생길 때마다 간격을 다시 적용해 저장 당시 자리가 달라질 수 있으므로 자식을 모두 놓은 뒤에 불러야 한다.
    /// </summary>
    private static void RestoreSpacing(Transform _target, JsonDataController.SpacingJson _spacing)
    {
        if (_target == null || _spacing == null || _spacing.values == null || _spacing.applied == null)
        {
            return;
        }

        for (int axis = 0; axis < 3 && axis < _spacing.values.Length && axis < _spacing.applied.Length; axis++)
        {
            if (!_spacing.applied[axis])
            {
                continue;
            }

            if (!_target.TryGetComponent(out SubDeviceSpacingData data))
            {
                data = _target.gameObject.AddComponent<SubDeviceSpacingData>();
            }

            data.Set(axis, _spacing.values[axis]);
        }
    }

    // 만들지 못한 항목 아래의 항목 수(본인 포함). 건너뛴 개수로 센다.
    private static int CountAll(JsonDataController.PackJson _json)
    {
        int count = 1;
        for (int i = 0; i < _json.moduleAssemblies.Count; i++)
        {
            count += CountAll(_json.moduleAssemblies[i]);
        }

        return count;
    }

    private static int CountAll(JsonDataController.ModuleAssemblyJson _json)
    {
        int count = 1;
        for (int i = 0; i < _json.modules.Count; i++)
        {
            count += CountAll(_json.modules[i]);
        }

        return count;
    }

    private static int CountAll(JsonDataController.ModuleJson _json)
    {
        int count = 1;
        for (int i = 0; i < _json.parallelAssemblies.Count; i++)
        {
            count += CountAll(_json.parallelAssemblies[i]);
        }

        return count;
    }

    private static int CountAll(JsonDataController.ParallelAssemblyJson _json)
    {
        return 1 + _json.cells.Count;
    }
}
