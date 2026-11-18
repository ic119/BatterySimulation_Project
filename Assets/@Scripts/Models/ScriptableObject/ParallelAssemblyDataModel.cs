using Incheol.Models;
using Incheol.Models.Define;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ParallelAssemblyDataModel : IDeviceNode
{
    public string parallelAssemblyName;
    public List<CellDataModel> cellsList = new List<CellDataModel>();
    public Transform parallelAssemblyPosition;
    public DeviceNodeValues values = new DeviceNodeValues();

    // React가 보낸 parallelAssembly JSON 키에 대응하는 값. (kind는 항상 parallelAssembly라 저장하지 않고, id는 values.deviceId,
    // name은 parallelAssemblyName, cell은 cellsList에 저장한다.)
    /// <summary>JSON position. 씬 상태를 읽어 덮어쓰는 values.position과 달리 React가 보낸 값 그대로다. 상위 부품 안에서는 상위 배치를 따르므로 참고용이다.</summary>
    public Vector3 jsonPosition;
    /// <summary>JSON stackingAxis. "X" | "Y" (StackingAxisType으로 비교해 저장)</summary>
    public StackingAxisType stackingAxis;
    /// <summary>JSON topology (TopologyType으로 비교해 저장)</summary>
    public TopologyType topology;
    /// <summary>JSON numParallelCells (≥ 1)</summary>
    public int numParallelCells;
    /// <summary>JSON rows (1 ~ numParallelCells)</summary>
    public int rows;
    /// <summary>JSON interCellGap (≥ 0)</summary>
    public float interCellGap;

    /// <summary>
    /// React가 보낸 parallelAssembly JSON 값을 이 모델에 저장한다.
    /// topology는 TopologyType, stackingAxis는 StackingAxisType(둘 다 대소문자 무시)으로 비교하고, cell은 cellsList에 저장한다.
    /// 같은 id(deviceId)의 Cell이 이미 cellsList에 있으면 그 Cell을 갱신하고, 없으면 새로 추가한다.
    /// 알 수 없는 값이거나 cell을 저장하지 못하면 아무것도 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool ApplyFrom(ParallelAssemblyPartJson _json)
    {
        if (_json == null)
        {
            DebugLogManager.GenerateErrorMessage<ParallelAssemblyDataModel>("parallelAssembly JSON이 null이라 저장하지 못했습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.topology) || !Enum.TryParse(_json.topology, true, out TopologyType parsedTopology) || !Enum.IsDefined(typeof(TopologyType), parsedTopology))
        {
            DebugLogManager.GenerateWarningMessage<ParallelAssemblyDataModel>($"알 수 없는 topology : '{_json.topology}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.stackingAxis) || !Enum.TryParse(_json.stackingAxis, true, out StackingAxisType parsedAxis) || !Enum.IsDefined(typeof(StackingAxisType), parsedAxis))
        {
            DebugLogManager.GenerateWarningMessage<ParallelAssemblyDataModel>($"알 수 없는 stackingAxis : '{_json.stackingAxis}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        // cell을 가장 먼저 저장한다. CellDataModel.ApplyFrom은 실패하면 아무것도 바꾸지 않으므로, 실패해도 이 모델은 그대로다.
        if (_json.cell == null || string.IsNullOrEmpty(_json.cell.id))
        {
            DebugLogManager.GenerateWarningMessage<ParallelAssemblyDataModel>($"cell이 없어 저장하지 않았습니다. (id={_json.id})");
            return false;
        }

        CellDataModel cell = cellsList.Find(_item => _item != null && _item.values.deviceId == _json.cell.id);
        bool isNewCell = cell == null;
        if (isNewCell)
        {
            cell = new CellDataModel { parentParallelAssembly = this };
        }

        if (!cell.ApplyFrom(_json.cell))
        {
            return false;
        }

        if (isNewCell)
        {
            cellsList.Add(cell);
        }

        topology = parsedTopology;
        stackingAxis = parsedAxis;
        values.deviceId = _json.id;
        parallelAssemblyName = _json.name;
        jsonPosition = _json.position;
        numParallelCells = _json.numParallelCells;
        rows = _json.rows;
        interCellGap = _json.interCellGap;
        return true;
    }

    /// <summary>이 ParallelAssembly가 속한 Module. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ModuleDataModel parentModule;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.parallelassembly;
    public string Name { get => parallelAssemblyName; set => parallelAssemblyName = value; }
    public Transform Position => parallelAssemblyPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentModule;
    public IReadOnlyList<IDeviceNode> Children => cellsList.ConvertAll(_item => (IDeviceNode)_item);
}
