using Incheol.Models;
using Incheol.Models.Define;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CellDataModel : IDeviceNode
{
    public string cellName;
    public CellType cellType;
    public DeviceNodeValues values = new DeviceNodeValues();
    public Transform cellPosition;

    // React가 보낸 cell JSON 키에 대응하는 값. (kind는 항상 cell이라 저장하지 않고, id는 values.deviceId, name은 cellName, geometry.shape는 cellType에 저장한다.)
    /// <summary>JSON position. 씬 상태를 읽어 덮어쓰는 values.position과 달리 React가 보낸 값 그대로다. 상위 부품 안에서는 상위 배치를 따르므로 참고용이다.</summary>
    public Vector3 jsonPosition;
    /// <summary>JSON stackingAxis. "X" | "Y" (StackingAxisType으로 비교해 저장)</summary>
    public StackingAxisType stackingAxis;
    /// <summary>JSON geometry.length (mm)</summary>
    public float length;
    /// <summary>JSON geometry.thickness (mm)</summary>
    public float thickness;
    /// <summary>JSON geometry.height (mm)</summary>
    public float height;

    /// <summary>
    /// React가 보낸 cell JSON 값을 이 모델에 저장한다.
    /// geometry.shape는 CellType, stackingAxis는 StackingAxisType(둘 다 대소문자 무시)으로 비교하며,
    /// 알 수 없는 값이면 아무것도 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool ApplyFrom(CellPartJson _json)
    {
        if (_json == null)
        {
            DebugLogManager.GenerateErrorMessage<CellDataModel>("cell JSON이 null이라 저장하지 못했습니다.");
            return false;
        }

        string shape = _json.geometry != null ? _json.geometry.shape : null;
        if (string.IsNullOrEmpty(shape) || !Enum.TryParse(shape, true, out CellType parsedType) || !Enum.IsDefined(typeof(CellType), parsedType))
        {
            DebugLogManager.GenerateWarningMessage<CellDataModel>($"알 수 없는 geometry.shape : '{shape}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.stackingAxis) || !Enum.TryParse(_json.stackingAxis, true, out StackingAxisType parsedAxis) || !Enum.IsDefined(typeof(StackingAxisType), parsedAxis))
        {
            DebugLogManager.GenerateWarningMessage<CellDataModel>($"알 수 없는 stackingAxis : '{_json.stackingAxis}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        cellType = parsedType;
        stackingAxis = parsedAxis;
        values.deviceId = _json.id;
        cellName = _json.name;
        jsonPosition = _json.position;
        length = _json.geometry.length;
        thickness = _json.geometry.thickness;
        height = _json.geometry.height;
        return true;
    }

    /// <summary>이 Cell이 속한 ParallelAssembly. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ParallelAssemblyDataModel parentParallelAssembly;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.cell;
    public string Name { get => cellName; set => cellName = value; }
    public Transform Position => cellPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentParallelAssembly;
    public IReadOnlyList<IDeviceNode> Children => Array.Empty<IDeviceNode>();
}
