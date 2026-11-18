using Incheol.Models;
using Incheol.Models.Define;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ModuleDataModel : IDeviceNode
{
    public string moduleName;
    public List<ParallelAssemblyDataModel> parallelAssembliesList = new List<ParallelAssemblyDataModel>();
    public Transform modulePosition;
    public DeviceNodeValues values = new DeviceNodeValues();

    // React가 보낸 module JSON 키에 대응하는 값. (kind는 항상 module이라 저장하지 않고, id는 values.deviceId, name은 moduleName,
    // parallelAssembly는 parallelAssembliesList에 저장한다.)
    /// <summary>JSON position. 씬 상태를 읽어 덮어쓰는 values.position과 달리 React가 보낸 값 그대로다. 상위 부품 안에서는 상위 배치를 따르므로 참고용이다.</summary>
    public Vector3 jsonPosition;
    /// <summary>JSON stackingAxis. "X" | "Y" (StackingAxisType으로 비교해 저장)</summary>
    public StackingAxisType stackingAxis;
    /// <summary>JSON topology (TopologyType으로 비교해 저장)</summary>
    public TopologyType topology;
    /// <summary>JSON numSeriesAssemblies (≥ 1). 이 수만큼 ParallelAssembly를 만든다. JSON의 parallelAssembly 하나가 그 견본이다.</summary>
    public int numSeriesAssemblies;
    /// <summary>JSON interParallelAssemblyGap (≥ 0, mm)</summary>
    public float interParallelAssemblyGap;

    /// <summary>
    /// React가 보낸 module JSON 값을 이 모델에 저장한다.
    /// topology는 TopologyType, stackingAxis는 StackingAxisType(둘 다 대소문자 무시)으로 비교하고, parallelAssembly는 parallelAssembliesList에 저장한다.
    /// 같은 id(deviceId)의 ParallelAssembly가 이미 있으면 갱신하고, 없으면 새로 추가한다.
    /// 알 수 없는 값이거나 numSeriesAssemblies가 1 미만이거나 parallelAssembly를 저장하지 못하면 아무것도 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool ApplyFrom(ModulePartJson _json)
    {
        if (_json == null)
        {
            DebugLogManager.GenerateErrorMessage<ModuleDataModel>("module JSON이 null이라 저장하지 못했습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.topology) || !Enum.TryParse(_json.topology, true, out TopologyType parsedTopology) || !Enum.IsDefined(typeof(TopologyType), parsedTopology))
        {
            DebugLogManager.GenerateWarningMessage<ModuleDataModel>($"알 수 없는 topology : '{_json.topology}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.stackingAxis) || !Enum.TryParse(_json.stackingAxis, true, out StackingAxisType parsedAxis) || !Enum.IsDefined(typeof(StackingAxisType), parsedAxis))
        {
            DebugLogManager.GenerateWarningMessage<ModuleDataModel>($"알 수 없는 stackingAxis : '{_json.stackingAxis}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        if (_json.numSeriesAssemblies < 1)
        {
            DebugLogManager.GenerateWarningMessage<ModuleDataModel>($"numSeriesAssemblies는 1 이상이어야 합니다 : {_json.numSeriesAssemblies} (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        // parallelAssembly를 먼저 저장한다. 실패해도 이 모델은 그대로다.
        if (_json.parallelAssembly == null || string.IsNullOrEmpty(_json.parallelAssembly.id))
        {
            DebugLogManager.GenerateWarningMessage<ModuleDataModel>($"parallelAssembly가 없어 저장하지 않았습니다. (id={_json.id})");
            return false;
        }

        ParallelAssemblyDataModel parallelAssembly = parallelAssembliesList.Find(_item => _item != null && _item.values.deviceId == _json.parallelAssembly.id);
        bool isNew = parallelAssembly == null;
        if (isNew)
        {
            parallelAssembly = new ParallelAssemblyDataModel { parentModule = this };
        }

        if (!parallelAssembly.ApplyFrom(_json.parallelAssembly))
        {
            return false;
        }

        if (isNew)
        {
            parallelAssembliesList.Add(parallelAssembly);
        }

        topology = parsedTopology;
        stackingAxis = parsedAxis;
        values.deviceId = _json.id;
        moduleName = _json.name;
        jsonPosition = _json.position;
        numSeriesAssemblies = _json.numSeriesAssemblies;
        interParallelAssemblyGap = _json.interParallelAssemblyGap;
        return true;
    }

    /// <summary>이 Module이 속한 ModuleAssembly. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ModuleAssemblyDataModel parentModuleAssembly;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.module;
    public string Name { get => moduleName; set => moduleName = value; }
    public Transform Position => modulePosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentModuleAssembly;
    public IReadOnlyList<IDeviceNode> Children => parallelAssembliesList.ConvertAll(_item => (IDeviceNode)_item);
}
