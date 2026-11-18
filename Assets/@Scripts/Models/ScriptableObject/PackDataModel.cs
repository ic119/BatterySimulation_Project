using Incheol.Models;
using Incheol.Models.Define;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PackDataModel : IDeviceNode
{
    public string packName;
    public DeviceNodeValues values = new DeviceNodeValues();
    public List<ModuleAssemblyDataModel> moduleAssembliesList = new List<ModuleAssemblyDataModel>();

    // React가 보낸 pack JSON 키에 대응하는 값. (kind는 항상 pack이라 저장하지 않고, id는 values.deviceId,
    // name은 packName, moduleAssemblies는 moduleAssembliesList에 저장한다.)
    /// <summary>JSON position. 씬 상태를 읽어 덮어쓰는 values.position과 달리 React가 보낸 값 그대로다.</summary>
    public Vector3 jsonPosition;
    /// <summary>JSON stackingAxis. "X" | "Y" (StackingAxisType으로 비교해 저장)</summary>
    public StackingAxisType stackingAxis;
    /// <summary>JSON interModuleAssemblyGap (≥ 0, mm). 나중에 ModuleAssembly 사이 간격을 조절하는 데 쓴다.</summary>
    public float interModuleAssemblyGap;

    /// <summary>
    /// React가 보낸 pack JSON 값을 이 모델에 저장한다.
    /// stackingAxis는 StackingAxisType(대소문자 무시)으로 비교하고, moduleAssemblies는 moduleAssembliesList에 저장한다.
    /// 같은 id(deviceId)의 ModuleAssembly가 이미 있으면 갱신하고, 없으면 새로 추가한다.
    /// 알 수 없는 stackingAxis, moduleAssemblies 없음, ModuleAssembly 저장 실패 중 하나면 false를 반환하고
    /// 이 모델의 값과 이번에 새로 추가한 ModuleAssembly는 바뀌지 않는다(이미 있던 ModuleAssembly는 실패 전까지 갱신됐을 수 있다).
    /// </summary>
    public bool ApplyFrom(PackPartJson _json)
    {
        if (_json == null)
        {
            DebugLogManager.GenerateErrorMessage<PackDataModel>("pack JSON이 null이라 저장하지 못했습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.stackingAxis) || !Enum.TryParse(_json.stackingAxis, true, out StackingAxisType parsedAxis) || !Enum.IsDefined(typeof(StackingAxisType), parsedAxis))
        {
            DebugLogManager.GenerateWarningMessage<PackDataModel>($"알 수 없는 stackingAxis : '{_json.stackingAxis}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        if (_json.moduleAssemblies == null || _json.moduleAssemblies.Count == 0)
        {
            DebugLogManager.GenerateWarningMessage<PackDataModel>($"moduleAssemblies가 없어 저장하지 않았습니다. (id={_json.id})");
            return false;
        }

        List<ModuleAssemblyDataModel> added = new List<ModuleAssemblyDataModel>();
        for (int i = 0; i < _json.moduleAssemblies.Count; i++)
        {
            ModuleAssemblyPartJson maJson = _json.moduleAssemblies[i];
            if (maJson == null || string.IsNullOrEmpty(maJson.id))
            {
                DebugLogManager.GenerateWarningMessage<PackDataModel>($"moduleAssemblies[{i}]가 비어 있거나 id가 없어 저장하지 않았습니다. (id={_json.id})");
                Rollback(added);
                return false;
            }

            ModuleAssemblyDataModel moduleAssembly = moduleAssembliesList.Find(_item => _item != null && _item.values.deviceId == maJson.id);
            bool isNew = moduleAssembly == null;
            if (isNew)
            {
                moduleAssembly = new ModuleAssemblyDataModel { parentPack = this };
            }

            if (!moduleAssembly.ApplyFrom(maJson))
            {
                Rollback(added);
                return false;
            }

            if (isNew)
            {
                moduleAssembliesList.Add(moduleAssembly);
                added.Add(moduleAssembly);
            }
        }

        stackingAxis = parsedAxis;
        values.deviceId = _json.id;
        packName = _json.name;
        jsonPosition = _json.position;
        interModuleAssemblyGap = _json.interModuleAssemblyGap;
        return true;
    }

    private void Rollback(List<ModuleAssemblyDataModel> _added)
    {
        for (int i = 0; i < _added.Count; i++)
        {
            moduleAssembliesList.Remove(_added[i]);
        }
    }

    /// <summary>이 Pack의 씬 오브젝트. Root 바로 아래에 놓인다. 저장하지 않는다.</summary>
    [NonSerialized] public Transform packPosition;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.pack;
    public string Name { get => packName; set => packName = value; }
    public Transform Position => packPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => null;
    public IReadOnlyList<IDeviceNode> Children => moduleAssembliesList.ConvertAll(_item => (IDeviceNode)_item);
}
