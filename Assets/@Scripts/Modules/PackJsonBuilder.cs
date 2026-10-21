using System;
using UnityEngine;

/// <summary>
/// 저장된 Json(JsonDataController.SaveJson)의 Pack / Module / Cell을 PackFactory로 다시 만든다. UI는 알지 못하고, 만들어진 것은 콜백으로 알린다.
/// 저장된 이름, 자식 구조, 크기를 쓰고, 위치는 저장값 대신 PackFactory의 자동 배치(첫 빈 칸 + Padding)가 정한다.
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

    private Result result;

    /// <param name="_containerCreated">Container가 만들어질 때마다.</param>
    /// <param name="_packCreated">Pack이 만들어질 때마다(Pack, 그 Transform).</param>
    /// <param name="_moduleCreated">Module이 만들어질 때마다(Module, 부모 Pack. 단독 Module이면 null).</param>
    /// <param name="_cellCreated">Cell이 만들어질 때마다.</param>
    public PackJsonBuilder(PackFactory _packFactory, Action<ContainerDataModel> _containerCreated, Action<PackDataModel, Transform> _packCreated, Action<ModuleDataModel, PackDataModel> _moduleCreated, Action<CellDataModel> _cellCreated)
    {
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
            BuildContents(container, containerJson.packs, containerJson.standaloneModules, _packPrefab, _modulePrefab, _cellPrefab);
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

    private ContainerDataModel NewContainer(string _name)
    {
        ContainerDataModel container = packFactory.CreateContainer(_name);
        containerCreated?.Invoke(container);
        result.containerCount = Mathf.Max(result.containerCount, ParseCount(_name));
        return container;
    }

    private void BuildContents(ContainerDataModel _container, System.Collections.Generic.List<JsonDataController.PackJson> _packList, System.Collections.Generic.List<JsonDataController.ModuleJson> _moduleList, GameObject _packPrefab, GameObject _modulePrefab, GameObject _cellPrefab)
    {
        for (int i = 0; _packList != null && i < _packList.Count; i++)
        {
            JsonDataController.PackJson packJson = _packList[i];
            PackDataModel pack = packFactory.CreatePack(packJson.name, _packPrefab, packJson.size, _container);
            packCreated?.Invoke(pack, packFactory.GetPackTransform(pack));
            result.packCount = Mathf.Max(result.packCount, ParseCount(packJson.name));

            for (int j = 0; j < packJson.modules.Count; j++)
            {
                BuildModule(pack, _container, packJson.modules[j], _modulePrefab, _cellPrefab);
            }
        }

        for (int i = 0; _moduleList != null && i < _moduleList.Count; i++)
        {
            BuildModule(null, _container, _moduleList[i], _modulePrefab, _cellPrefab);
        }
    }

    private void BuildModule(PackDataModel _pack, ContainerDataModel _container, JsonDataController.ModuleJson _moduleJson, GameObject _modulePrefab, GameObject _cellPrefab)
    {
        ModuleDataModel module = packFactory.CreateModule(_pack, _moduleJson.name, _modulePrefab, _moduleJson.size, _container);
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
            CellDataModel cell = packFactory.CreateCell(module, cellJson.name, _cellPrefab, cellJson.size);
            if (cell == null)
            {
                result.skippedCount++;
                continue;
            }

            cellCreated?.Invoke(cell);
            result.cellCount = Mathf.Max(result.cellCount, ParseCount(cellJson.name));
        }
    }

    /// <summary>이름이 "3"처럼 번호 문자열이면 그 번호, 아니면 0. 불러온 뒤 새로 만드는 항목의 번호가 겹치지 않게 이어 붙이는 데 쓴다.</summary>
    private static int ParseCount(string _name)
    {
        return int.TryParse(_name, out int number) ? number : 0;
    }
}
