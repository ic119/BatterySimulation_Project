using Incheol.Modules;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 저장 데이터(SaveJson) 형식과 Json 파일 입출력. 멤버가 모두 static이라 씬에 두거나 컴포넌트로 붙일 필요가 없다.
/// (static class는 DebugLogManager.GenerateLogMessage&lt;T&gt;의 T로 쓸 수 없어 일반 class로 둔다.)
/// </summary>
public class JsonDataController
{
    #region Json Data
    [Serializable]
    public class CellJson
    {
        public string name;
        public Vector3 position;
        public Vector3 size;

        /// <summary>몸체의 월드 중심. 불러올 때 이 자리에 그대로 놓는다. hasBodyCenter가 false면 이 값이 없던 예전 파일이다.</summary>
        public bool hasBodyCenter;
        public Vector3 bodyCenter;
    }

    /// <summary>항목(Pack / Module)에 적용한 자식 간격(축 x, y, z 순서). applied가 true인 축만 유효하다. 없던 예전 파일은 모두 false.</summary>
    [Serializable]
    public class SpacingJson
    {
        public float[] values = new float[3];
        public bool[] applied = new bool[3];
    }

    [Serializable]
    public class ModuleJson
    {
        public string name;
        public Vector3 position;
        public Vector3 size;

        /// <summary>몸체의 월드 중심. 불러올 때 이 자리에 그대로 놓는다. hasBodyCenter가 false면 이 값이 없던 예전 파일이다.</summary>
        public bool hasBodyCenter;
        public Vector3 bodyCenter;

        /// <summary>이 Module 안의 Cell 사이 간격.</summary>
        public SpacingJson cellSpacing = new SpacingJson();

        public List<CellJson> cells = new List<CellJson>();
    }

    [Serializable]
    public class PackJson
    {
        public string name;
        public Vector3 position;
        public Vector3 size;

        /// <summary>몸체의 월드 중심. 불러올 때 이 자리에 그대로 놓는다. hasBodyCenter가 false면 이 값이 없던 예전 파일이다.</summary>
        public bool hasBodyCenter;
        public Vector3 bodyCenter;

        /// <summary>Container의 Pack 격자에서 놓인 열(x 방향)과 줄(z 방향). hasSlot이 false면 칸 정보가 없던 예전 파일이다.</summary>
        public bool hasSlot;
        public int slotColumn;
        public int slotRow;

        /// <summary>이 Pack 안의 Module 사이 간격.</summary>
        public SpacingJson moduleSpacing = new SpacingJson();

        public List<ModuleJson> modules = new List<ModuleJson>();
    }

    [Serializable]
    public class ContainerJson
    {
        public string name;

        /// <summary>Pack 생성 격자(UI_CreateDeviceCount의 x 개수 / z 줄 수). 0이면 이 값이 없던 예전 파일이다.</summary>
        public int packColumns;
        public int packRows;

        /// <summary>Container에 적용한 Pack 사이 간격(축 x, y, z 순서). spacingApplied가 true인 축만 유효하다.</summary>
        public float[] spacing = new float[3];
        public bool[] spacingApplied = new bool[3];

        public List<PackJson> packs = new List<PackJson>();

        /// <summary>어느 Pack에도 속하지 않고 이 Container에 단독으로 만든 Module.</summary>
        public List<ModuleJson> standaloneModules = new List<ModuleJson>();
    }

    [Serializable]
    public class SaveJson
    {
        /// <summary>
        /// 저장 형식의 버전. 이 값이 없던 예전 파일은 0으로 읽힌다. 새로 저장할 때는 항상 CurrentVersion을 쓴다.
        /// 불러올 때 JsonDataMigration이 현재 버전의 구조로 바꾸고, CurrentVersion보다 높은 파일(더 새로운 프로그램이 저장한 것)은 읽지 않는다.
        /// </summary>
        public int version;

        public List<ContainerJson> containers = new List<ContainerJson>();

        /// <summary>Container가 없던 예전(버전 0) 파일의 Pack. 새로 저장할 때는 쓰지 않고, 불러올 때 JsonDataMigration이 Pack마다 새 Container에 옮겨 담고 비운다.</summary>
        public List<PackJson> packs = new List<PackJson>();

        /// <summary>Container가 없던 예전(버전 0) 파일의 단독 Module. 불러올 때 JsonDataMigration이 새 Container에 옮겨 담고 비운다.</summary>
        public List<ModuleJson> standaloneModules = new List<ModuleJson>();
    }

    /// <summary>
    /// 저장 형식의 현재 버전.
    /// 0: 버전 필드가 없던 파일(Container가 없던 가장 오래된 형식부터 Container가 있던 중간 형식까지. 몸체 중심, 칸, 격자는 있는 파일도 있고 없는 파일도 있다).
    /// 1: 항상 containers 아래에 담긴다. 형식을 바꿀 때는 이 값을 올리고 JsonDataMigration에 이전 버전에서 올리는 단계를 추가한다.
    /// </summary>
    public const int CurrentVersion = 1;
    #endregion

    #region Method
    /// <summary>_path의 Json 파일을 지운다. 이미 없거나 지우지 못하면 false.</summary>
    public static bool Delete(string _path)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return false;
            }

            File.Delete(_path);
            DebugLogManager.GenerateLogMessage<JsonDataController>($"Json 삭제 완료 : {_path}");
            return true;
        }
        catch (Exception exception)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json 삭제 실패 : {_path} ({exception.Message})");
            return false;
        }
    }

    /// <summary>
    /// 저장된 Json 파일의 전체 경로 목록. 파일 이름이 저장 시각(yyyyMMdd_HHmmss_fff)이라 이름 내림차순이면 최신 파일이 맨 앞이다.
    /// 예전 형식(yyyyMMddHHmmss, yyyyMMdd_HHmmss)의 파일과 섞여도 순서가 맞도록 '_'를 뺀 이름으로 견준다. 저장 폴더가 없거나 읽기에 실패하면 빈 배열.
    /// </summary>
    public static string[] GetSavedFilePaths()
    {
        try
        {
            string[] pathArray = Directory.GetFiles(Application.persistentDataPath, "*.json");
            Array.Sort(pathArray, (a, b) => string.CompareOrdinal(
                Path.GetFileNameWithoutExtension(b).Replace("_", string.Empty),
                Path.GetFileNameWithoutExtension(a).Replace("_", string.Empty)));
            return pathArray;
        }
        catch (Exception exception)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json 파일 목록 읽기 실패 : {exception.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// _path의 Json 파일을 읽어 저장 데이터로 돌려준다. 예전 버전 파일은 현재 버전의 구조로 바꿔서 돌려준다.
    /// 파일이 없거나 형식이 맞지 않거나, 이 프로그램보다 새로운 버전으로 저장된 파일이면 null.
    /// </summary>
    public static SaveJson Load(string _path)
    {
        try
        {
            SaveJson data = JsonUtility.FromJson<SaveJson>(File.ReadAllText(_path));
            if (data == null)
            {
                DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json 내용이 비어 있습니다 : {_path}");
                return null;
            }

            if (!JsonDataMigration.TryMigrate(data))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json을 읽을 수 없는 버전입니다 : {_path}");
                return null;
            }

            return data;
        }
        catch (Exception exception)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json 읽기 실패 : {_path} ({exception.Message})");
            return null;
        }
    }

    /// <summary>
    /// 저장하는 시점의 연도월일_시분초_밀리초(yyyyMMdd_HHmmss_fff)를 파일 이름으로 하는 전체 경로.
    /// 이미 같은 이름의 파일이 있으면 1ms씩 늦춘 이름을 써서 덮어쓰지 않는다(이름 순서가 곧 저장 순서라 최신순 정렬도 유지된다).
    /// </summary>
    private static string BuildSavePath()
    {
        DateTime now = DateTime.Now;
        string path = Path.Combine(Application.persistentDataPath, $"{now:yyyyMMdd_HHmmss_fff}.json");
        while (File.Exists(path))
        {
            now = now.AddMilliseconds(1);
            path = Path.Combine(Application.persistentDataPath, $"{now:yyyyMMdd_HHmmss_fff}.json");
        }

        return path;
    }

    /// <summary>
    /// _path에 _content를 안전하게 쓴다. 임시 파일(.tmp)에 먼저 다 쓴 뒤 이름을 바꿔 완성하므로, 쓰는 도중 앱이 종료돼도 깨진 .json이 남지 않는다.
    /// 실패하면 임시 파일을 지우고 예외를 그대로 던진다.
    /// </summary>
    private static void WriteAllTextAtomic(string _path, string _content)
    {
        string tempPath = _path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, _content);
            File.Move(tempPath, _path);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception)
            {
                // 임시 파일 정리 실패는 원래 예외를 가리지 않도록 무시한다.
            }

            throw;
        }
    }

    /// <summary>
    /// _factory가 가진 Pack / Module / Cell(Pack에 속한 것과 단독 Module 모두)의 현재 상태를 Json 파일로 저장한다. 파일 이름은 저장하는 시점의 yyyyMMdd_HHmmss_fff다.
    /// 저장할 항목이 하나도 없거나 쓰기에 실패하면 false.
    /// </summary>
    public static bool Save(PackFactory _factory, Func<ContainerDataModel, Vector2Int> _gridProvider = null)
    {
        SaveJson data = Build(_factory, _gridProvider);
        if (data.containers.Count == 0)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>("저장할 Pack / Module이 없습니다.");
            return false;
        }

        string savePath = BuildSavePath();
        try
        {
            WriteAllTextAtomic(savePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json 저장 실패 : {exception.Message}");
            return false;
        }

        DebugLogManager.GenerateLogMessage<JsonDataController>($"Json 저장 완료 : {savePath}");
        return true;
    }

    /// <param name="_gridProvider">Container의 Pack 생성 격자(x 개수, z 줄 수). 없으면 격자를 저장하지 않는다.</param>
    public static SaveJson Build(PackFactory _factory, Func<ContainerDataModel, Vector2Int> _gridProvider = null)
    {
        SaveJson data = new SaveJson { version = CurrentVersion };
        if (_factory == null)
        {
            return data;
        }

        IReadOnlyList<ContainerDataModel> containerList = _factory.Containers;
        for (int c = 0; c < containerList.Count; c++)
        {
            ContainerDataModel container = containerList[c];
            ContainerJson containerJson = new ContainerJson { name = container.containerName };

            if (_gridProvider != null)
            {
                Vector2Int grid = _gridProvider(container);
                containerJson.packColumns = grid.x;
                containerJson.packRows = grid.y;
            }

            if (container.containerPosition != null && container.containerPosition.TryGetComponent(out SubDeviceSpacingData spacingData))
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    containerJson.spacingApplied[axis] = spacingData.TryGet(axis, out containerJson.spacing[axis]);
                }
            }

            for (int i = 0; i < container.packsList.Count; i++)
            {
                PackDataModel pack = container.packsList[i];
                PackJson packJson = new PackJson
                {
                    name = pack.packName,
                    hasSlot = pack.slotColumn >= 0 && pack.slotRow >= 0,
                    slotColumn = pack.slotColumn,
                    slotRow = pack.slotRow,
                };

                // PackDataModel에는 Transform이 없으므로 팩토리가 따로 들고 있는 Pack 오브젝트를 꺼낸다.
                Transform packTransform = _factory.GetPackTransform(pack);
                if (packTransform != null)
                {
                    packJson.position = packTransform.position;
                    Bounds packBounds = GetBodyBounds(packTransform);
                    packJson.size = packBounds.size;
                    packJson.hasBodyCenter = true;
                    packJson.bodyCenter = packBounds.center;
                    ReadSpacing(packTransform, packJson.moduleSpacing);
                }

                if (pack.modulesList != null)
                {
                    for (int j = 0; j < pack.modulesList.Count; j++)
                    {
                        packJson.modules.Add(BuildModule(pack.modulesList[j]));
                    }
                }

                containerJson.packs.Add(packJson);
            }

            for (int i = 0; i < container.modulesList.Count; i++)
            {
                containerJson.standaloneModules.Add(BuildModule(container.modulesList[i]));
            }

            data.containers.Add(containerJson);
        }

        return data;
    }

    /// <summary>_target(Pack / Module)에 기억된 자식 간격을 _spacing에 옮겨 담는다. 기억된 간격이 없으면 그대로 둔다.</summary>
    private static void ReadSpacing(Transform _target, SpacingJson _spacing)
    {
        if (!_target.TryGetComponent(out SubDeviceSpacingData data))
        {
            return;
        }

        for (int axis = 0; axis < 3; axis++)
        {
            _spacing.applied[axis] = data.TryGet(axis, out _spacing.values[axis]);
        }
    }

    private static ModuleJson BuildModule(ModuleDataModel _module)
    {
        ModuleJson moduleJson = new ModuleJson { name = _module.moduleName };
        if (_module.modulePosition != null)
        {
            moduleJson.position = _module.modulePosition.position;
            Bounds moduleBounds = GetBodyBounds(_module.modulePosition);
            moduleJson.size = moduleBounds.size;
            moduleJson.hasBodyCenter = true;
            moduleJson.bodyCenter = moduleBounds.center;
            ReadSpacing(_module.modulePosition, moduleJson.cellSpacing);
        }

        if (_module.cellsList != null)
        {
            for (int i = 0; i < _module.cellsList.Count; i++)
            {
                CellDataModel cell = _module.cellsList[i];
                CellJson cellJson = new CellJson { name = cell.cellName };
                if (cell.cellPosition != null)
                {
                    cellJson.position = cell.cellPosition.position;
                    Bounds cellBounds = GetBodyBounds(cell.cellPosition);
                    cellJson.size = cellBounds.size;
                    cellJson.hasBodyCenter = true;
                    cellJson.bodyCenter = cellBounds.center;
                }

                moduleJson.cells.Add(cellJson);
            }
        }

        return moduleJson;
    }

    /// <summary>
    /// 몸체의 월드 bounds. Pack / Module은 자식 Body, Cell은 본인의 Renderer를 쓰고(자식 Module / Cell은 섞지 않는다),
    /// 둘 다 없으면 자식 Renderer 전체를 합친다. Renderer가 없으면 크기 0.
    /// </summary>
    private static Bounds GetBodyBounds(Transform _target)
    {
        Transform body = _target.Find("Body");
        if (body != null && body.TryGetComponent(out Renderer bodyRenderer))
        {
            return bodyRenderer.bounds;
        }

        if (_target.TryGetComponent(out Renderer ownRenderer))
        {
            return ownRenderer.bounds;
        }

        Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
        if (rendererArray.Length == 0)
        {
            return new Bounds(_target.position, Vector3.zero);
        }

        Bounds bounds = rendererArray[0].bounds;
        for (int i = 1; i < rendererArray.Length; i++)
        {
            bounds.Encapsulate(rendererArray[i].bounds);
        }

        return bounds;
    }
    #endregion
}
