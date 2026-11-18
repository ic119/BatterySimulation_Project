using Incheol.Models;
using Incheol.Models.Define;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ModuleAssemblyDataModel : IDeviceNode
{
    public string moduleAssemblyName;
    public List<ModuleDataModel> modulesList = new List<ModuleDataModel>();
    public Transform moduleAssemblyPosition;
    public DeviceNodeValues values = new DeviceNodeValues();

    // React가 보낸 moduleAssembly JSON 키에 대응하는 값. (kind는 항상 moduleAssembly라 저장하지 않고, id는 values.deviceId,
    // name은 moduleAssemblyName, modules는 modulesList에 저장한다.)
    /// <summary>JSON position. 상위 부품(Pack) 안에서는 무시되고 상위 배치를 따르므로 참고용이다.</summary>
    public Vector3 jsonPosition;
    /// <summary>JSON stackingAxis. "X" | "Y" (StackingAxisType으로 비교해 저장)</summary>
    public StackingAxisType stackingAxis;
    /// <summary>JSON interModuleGap (≥ 0, mm)</summary>
    public float interModuleGap;
    /// <summary>JSON numLevels (1 ~ modules 개수). 나중에 Unity의 +Y 방향으로 이 수만큼 층을 쌓는 데 쓴다.</summary>
    public int numLevels;

    /// <summary>
    /// React가 보낸 moduleAssembly JSON 값을 이 모델에 저장한다.
    /// stackingAxis는 StackingAxisType(대소문자 무시)으로 비교하고, modules는 modulesList에 저장한다.
    /// 같은 id(deviceId)의 Module이 이미 있으면 갱신하고, 없으면 새로 추가한다.
    /// 알 수 없는 stackingAxis, modules 없음, numLevels가 1 ~ modules 개수를 벗어남, Module 저장 실패 중 하나면 false를 반환하고
    /// 이 모델의 값과 이번에 새로 추가한 Module은 바뀌지 않는다(이미 있던 Module은 실패 전까지 갱신됐을 수 있다).
    /// </summary>
    public bool ApplyFrom(ModuleAssemblyPartJson _json)
    {
        if (_json == null)
        {
            DebugLogManager.GenerateErrorMessage<ModuleAssemblyDataModel>("moduleAssembly JSON이 null이라 저장하지 못했습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(_json.stackingAxis) || !Enum.TryParse(_json.stackingAxis, true, out StackingAxisType parsedAxis) || !Enum.IsDefined(typeof(StackingAxisType), parsedAxis))
        {
            DebugLogManager.GenerateWarningMessage<ModuleAssemblyDataModel>($"알 수 없는 stackingAxis : '{_json.stackingAxis}' (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        if (_json.modules == null || _json.modules.Count == 0)
        {
            DebugLogManager.GenerateWarningMessage<ModuleAssemblyDataModel>($"modules가 없어 저장하지 않았습니다. (id={_json.id})");
            return false;
        }

        if (_json.numLevels < 1 || _json.numLevels > _json.modules.Count)
        {
            DebugLogManager.GenerateWarningMessage<ModuleAssemblyDataModel>($"numLevels는 1 ~ modules 개수({_json.modules.Count})여야 합니다 : {_json.numLevels} (id={_json.id}). 저장하지 않았습니다.");
            return false;
        }

        List<ModuleDataModel> added = new List<ModuleDataModel>();
        for (int i = 0; i < _json.modules.Count; i++)
        {
            ModulePartJson moduleJson = _json.modules[i];
            if (moduleJson == null || string.IsNullOrEmpty(moduleJson.id))
            {
                DebugLogManager.GenerateWarningMessage<ModuleAssemblyDataModel>($"modules[{i}]가 비어 있거나 id가 없어 저장하지 않았습니다. (id={_json.id})");
                Rollback(added);
                return false;
            }

            ModuleDataModel module = modulesList.Find(_item => _item != null && _item.values.deviceId == moduleJson.id);
            bool isNew = module == null;
            if (isNew)
            {
                module = new ModuleDataModel { parentModuleAssembly = this };
            }

            if (!module.ApplyFrom(moduleJson))
            {
                Rollback(added);
                return false;
            }

            if (isNew)
            {
                modulesList.Add(module);
                added.Add(module);
            }
        }

        stackingAxis = parsedAxis;
        values.deviceId = _json.id;
        moduleAssemblyName = _json.name;
        jsonPosition = _json.position;
        interModuleGap = _json.interModuleGap;
        numLevels = _json.numLevels;
        return true;
    }

    private void Rollback(List<ModuleDataModel> _added)
    {
        for (int i = 0; i < _added.Count; i++)
        {
            modulesList.Remove(_added[i]);
        }
    }

    /// <summary>이 ModuleAssembly가 속한 Pack. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public PackDataModel parentPack;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.moduleassembly;
    public string Name { get => moduleAssemblyName; set => moduleAssemblyName = value; }
    public Transform Position => moduleAssemblyPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentPack;
    public IReadOnlyList<IDeviceNode> Children => modulesList.ConvertAll(_item => (IDeviceNode)_item);
}
