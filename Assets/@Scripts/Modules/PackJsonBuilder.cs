using Incheol.Modules;
using System;
using UnityEngine;

/// <summary>
/// 저장된 Json(JsonDataController.SaveJson)의 Pack / Module / Cell을 PackFactory로 다시 만든다. UI는 알지 못하고, 만들어진 것은 콜백으로 알린다.
/// 저장된 이름, 자식 구조, 크기(Scale), 몸체 중심을 써서 Pack / Module / Cell을 저장 당시 크기와 자리에 그대로 놓고, Container의 격자와 Container / Pack / Module의 간격도 되돌린다.
/// 몸체 중심이 없던 예전 파일은 Module / Cell을 PackFactory의 자동 배치(첫 빈 칸 + Padding)로 놓는다.
/// 부모가 가득 차 놓을 곳이 없는 항목은 직접 만들 때처럼 만들어지지 않고 건너뛴 개수로 센다.
/// </summary>
public class PackJsonBuilder
{
    /// <summary>불러온 뒤의 항목 번호와 건너뛴 개수. 번호는 이름이 "3"처럼 번호 문자열인 것 중 가장 큰 값이다.</summary>
    public struct Result
    {
        public int containerCount;
        public int packCount;
        public int moduleCount;
        public int cellCount;
        public int skippedCount;
    }

    private readonly PackFactory packFactory;
    private readonly Action<ContainerDataModel> containerCreated;
    private readonly Action<PackDataModel, Transform> packCreated;
    private readonly Action<ModuleDataModel, PackDataModel> moduleCreated;
    private readonly Action<CellDataModel> cellCreated;
    private readonly Action<ContainerDataModel, int, int> containerGridRestored;

    private Result result;

    // 지금 만드는 Container의 Pack 격자. 0이면 격자 없이(예전 파일) 만든다.
    private int currentColumns;
    private int currentRows;

    /// <param name="_containerCreated">Container가 만들어질 때마다.</param>
    /// <param name="_packCreated">Pack이 만들어질 때마다(Pack, 그 Transform).</param>
    /// <param name="_moduleCreated">Module이 만들어질 때마다(Module, 부모 Pack. 단독 Module이면 null).</param>
    /// <param name="_cellCreated">Cell이 만들어질 때마다.</param>
    /// <param name="_containerGridRestored">저장된 Pack 생성 격자를 Container에 되돌릴 때마다(Container, x 개수, z 줄 수).</param>
    public PackJsonBuilder(PackFactory _packFactory, Action<ContainerDataModel> _containerCreated, Action<PackDataModel, Transform> _packCreated, Action<ModuleDataModel, PackDataModel> _moduleCreated, Action<CellDataModel> _cellCreated, Action<ContainerDataModel, int, int> _containerGridRestored = null)
    {
        containerGridRestored = _containerGridRestored;
        packFactory = _packFactory;
        containerCreated = _containerCreated;
        packCreated = _packCreated;
        moduleCreated = _moduleCreated;
        cellCreated = _cellCreated;
    }

    /// <summary>_data의 Pack과 단독 Module을 차례로 만든다. 이미 있는 것은 지우지 않으므로 호출하기 전에 비워 둔다.</summary>
    public Result Build(JsonDataController.SaveJson _data, GameObject _packPrefab, GameObject _modulePrefab, GameObject _cellPrefab)
    {
        result = default;

        for (int c = 0; c < _data.containers.Count; c++)
        {
            JsonDataController.ContainerJson containerJson = _data.containers[c];
            ContainerDataModel container = NewContainer(containerJson.name);
            RestoreContainerLayout(container, containerJson);
            BuildContents(container, containerJson.packs, containerJson.standaloneModules, _packPrefab, _modulePrefab, _cellPrefab);

            // 간격은 자식을 모두 저장 당시 자리에 놓은 뒤에 기억시킨다. 먼저 기억시키면 자식이 생길 때마다 간격을 다시 적용해 저장 당시 자리가 달라질 수 있다.
            RestoreSpacing(container.containerPosition, containerJson.spacing, containerJson.spacingApplied);
        }

        // Container가 없던 예전 파일: Pack과 단독 Module마다 새 Container에 담는다.
        for (int i = 0; i < _data.packs.Count; i++)
        {
            ContainerDataModel container = NewContainer((result.containerCount + 1).ToString());
            BuildContents(container, new System.Collections.Generic.List<JsonDataController.PackJson> { _data.packs[i] }, null, _packPrefab, _modulePrefab, _cellPrefab);
        }

        for (int i = 0; i < _data.standaloneModules.Count; i++)
        {
            ContainerDataModel container = NewContainer((result.containerCount + 1).ToString());
            BuildContents(container, null, new System.Collections.Generic.List<JsonDataController.ModuleJson> { _data.standaloneModules[i] }, _packPrefab, _modulePrefab, _cellPrefab);
        }

        return result;
    }

    /// <summary>
    /// 저장된 Pack 생성 격자(x * z)를 Container에 되돌린다. Pack을 만들기 전에 불러야 한다.
    /// 격자가 없던 예전 파일은 x를 Pack 개수(최소 1), z를 1로 본다.
    /// </summary>
    private void RestoreContainerLayout(ContainerDataModel _container, JsonDataController.ContainerJson _json)
    {
        int columns = _json.packColumns > 0 ? _json.packColumns : Mathf.Max(1, _json.packs.Count);
        int rows = _json.packRows > 0 ? _json.packRows : 1;
        currentColumns = columns;
        currentRows = rows;
        containerGridRestored?.Invoke(_container, columns, rows);
    }

    /// <summary>
    /// 저장된 자식 간격(_values 중 _applied가 true인 축)을 _target(Container / Pack / Module)에 기억시킨다.
    /// 이후 새로 만드는 자식이 같은 간격으로 놓이도록 하는 용도이며, 불러오는 도중에는 자식을 모두 놓은 뒤에 불러야 한다.
    /// </summary>
    private static void RestoreSpacing(Transform _target, float[] _values, bool[] _applied)
    {
        if (_target == null || _values == null || _applied == null)
        {
            return;
        }

        for (int axis = 0; axis < 3 && axis < _values.Length && axis < _applied.Length; axis++)
        {
            if (!_applied[axis])
            {
                continue;
            }

            if (!_target.TryGetComponent(out SubDeviceSpacingData data))
            {
                data = _target.gameObject.AddComponent<SubDeviceSpacingData>();
            }

            data.Set(axis, _values[axis]);
        }
    }

    private ContainerDataModel NewContainer(string _name)
    {
        ContainerDataModel container = packFactory.CreateContainer(_name);
        currentColumns = 0;
        currentRows = 0;
        containerCreated?.Invoke(container);
        result.containerCount = Mathf.Max(result.containerCount, ParseCount(_name));
        return container;
    }

    private void BuildContents(ContainerDataModel _container, System.Collections.Generic.List<JsonDataController.PackJson> _packList, System.Collections.Generic.List<JsonDataController.ModuleJson> _moduleList, GameObject _packPrefab, GameObject _modulePrefab, GameObject _cellPrefab)
    {
        for (int i = 0; _packList != null && i < _packList.Count; i++)
        {
            JsonDataController.PackJson packJson = _packList[i];
            // 저장된 칸 번호를 그대로 쓴다(번호가 비어 있던 예전 파일은 번호가 낮은 빈 칸). 삭제로 비어 있던 칸도 저장 때 그대로 비어 있게 된다.
            PackDataModel pack = packFactory.CreatePack(packJson.name, _packPrefab, packJson.size, _container, currentColumns, currentRows, packJson.hasSlot ? packJson.slotColumn : -1, packJson.hasSlot ? packJson.slotRow : -1, GetBodyCenter(packJson.hasBodyCenter, packJson.bodyCenter));

            // 몸체 중심이 없는 예전 파일은 저장된 pivot 위치에 둔다. 새 파일은 CreatePack이 저장 당시 몸체 자리에 이미 놓았다.
            Transform packTransform = packFactory.GetPackTransform(pack);
            if (packTransform != null && !packJson.hasBodyCenter)
            {
                packTransform.position = packJson.position;
            }

            packCreated?.Invoke(pack, packTransform);
            result.packCount = Mathf.Max(result.packCount, ParseCount(packJson.name));

            for (int j = 0; j < packJson.modules.Count; j++)
            {
                BuildModule(pack, _container, packJson.modules[j], _modulePrefab, _cellPrefab);
            }

            RestoreSpacing(packTransform, packJson.moduleSpacing?.values, packJson.moduleSpacing?.applied);
        }

        for (int i = 0; _moduleList != null && i < _moduleList.Count; i++)
        {
            BuildModule(null, _container, _moduleList[i], _modulePrefab, _cellPrefab);
        }
    }

    private void BuildModule(PackDataModel _pack, ContainerDataModel _container, JsonDataController.ModuleJson _moduleJson, GameObject _modulePrefab, GameObject _cellPrefab)
    {
        ModuleDataModel module = packFactory.CreateModule(_pack, _moduleJson.name, _modulePrefab, _moduleJson.size, _container, GetBodyCenter(_moduleJson.hasBodyCenter, _moduleJson.bodyCenter));
        if (module == null)
        {
            // 이 Module이 못 만들어지면 그 안의 Cell도 만들 곳이 없다.
            result.skippedCount += 1 + _moduleJson.cells.Count;
            return;
        }

        moduleCreated?.Invoke(module, _pack);
        result.moduleCount = Mathf.Max(result.moduleCount, ParseCount(_moduleJson.name));

        for (int i = 0; i < _moduleJson.cells.Count; i++)
        {
            JsonDataController.CellJson cellJson = _moduleJson.cells[i];
            CellDataModel cell = packFactory.CreateCell(module, cellJson.name, _cellPrefab, cellJson.size, GetBodyCenter(cellJson.hasBodyCenter, cellJson.bodyCenter));
            if (cell == null)
            {
                result.skippedCount++;
                continue;
            }

            cellCreated?.Invoke(cell);
            result.cellCount = Mathf.Max(result.cellCount, ParseCount(cellJson.name));
        }

        RestoreSpacing(module.modulePosition, _moduleJson.cellSpacing?.values, _moduleJson.cellSpacing?.applied);
    }

    /// <summary>저장된 몸체 중심이 있으면 그 값을, 없던 예전 파일이면 null(= 자동 배치)을 돌려준다.</summary>
    private static Vector3? GetBodyCenter(bool _hasBodyCenter, Vector3 _bodyCenter)
    {
        return _hasBodyCenter ? _bodyCenter : (Vector3?)null;
    }

    /// <summary>이름이 "3"처럼 번호 문자열이면 그 번호, 아니면 0. 불러온 뒤 새로 만드는 항목의 번호가 겹치지 않게 이어 붙이는 데 쓴다.</summary>
    private static int ParseCount(string _name)
    {
        return int.TryParse(_name, out int number) ? number : 0;
    }
}
