using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
using Incheol.Modules;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 저장 데이터(SaveJson) 형식과 Json 파일 입출력. 멤버가 모두 static이라 씬에 두거나 컴포넌트로 붙일 필요가 없다.
/// (static class는 DebugLogManager.GenerateLogMessage&lt;T&gt;의 T로 쓸 수 없어 일반 class로 둔다.)
/// 저장 구조는 Pack > ModuleAssembly > Module > ParallelAssembly > Cell 이다.
/// </summary>
public class JsonDataController
{
    #region Json Data
    /// <summary>항목에 적용한 자식 간격(축 x, y, z 순서). applied가 true인 축만 유효하다.</summary>
    [Serializable]
    public class SpacingJson
    {
        public float[] values = new float[3];
        public bool[] applied = new bool[3];
    }

    /// <summary>모든 종류가 공통으로 저장하는 값. 종류는 어느 목록에 담겼는지로 정해진다(JsonUtility는 다형성을 지원하지 않는다).</summary>
    [Serializable]
    public class NodeJson
    {
        /// <summary>React와 주고받는 장치 식별자. 없는 파일(예전 저장본)은 비어 있다.</summary>
        public string deviceId;
        public string name;
        public Vector3 position;
        public Vector3 size;

        /// <summary>몸체의 월드 중심. 불러올 때 이 자리에 그대로 놓는다.</summary>
        public bool hasBodyCenter;
        public Vector3 bodyCenter;

        /// <summary>이 항목 안의 자식 사이 간격.</summary>
        public SpacingJson childSpacing = new SpacingJson();
    }

    [Serializable]
    public class CellJson : NodeJson
    {
    }

    [Serializable]
    public class ParallelAssemblyJson : NodeJson
    {
        public List<CellJson> cells = new List<CellJson>();
    }

    [Serializable]
    public class ModuleJson : NodeJson
    {
        public List<ParallelAssemblyJson> parallelAssemblies = new List<ParallelAssemblyJson>();
    }

    [Serializable]
    public class ModuleAssemblyJson : NodeJson
    {
        public List<ModuleJson> modules = new List<ModuleJson>();
    }

    [Serializable]
    public class PackJson : NodeJson
    {
        public List<ModuleAssemblyJson> moduleAssemblies = new List<ModuleAssemblyJson>();
    }

    [Serializable]
    public class SaveJson
    {
        /// <summary>저장 형식의 버전. 이 값이 CurrentVersion과 다른 파일은 읽지 않는다.</summary>
        public int version;

        public List<PackJson> packs = new List<PackJson>();
    }

    /// <summary>
    /// 저장 형식의 현재 버전.
    /// 2: Pack > ModuleAssembly > Module > ParallelAssembly > Cell. 이전 형식(Container, Pack > Module > Cell)은 호환하지 않는다.
    /// </summary>
    public const int CurrentVersion = 2;
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
    /// _path의 Json 파일을 읽어 저장 데이터로 돌려준다.
    /// 파일이 없거나 형식이 맞지 않거나, 현재 저장 형식(CurrentVersion)이 아닌 파일이면 null.
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

            if (data.version != CurrentVersion)
            {
                DebugLogManager.GenerateErrorMessage<JsonDataController>($"Json을 읽을 수 없는 버전입니다 : {_path} (파일 {data.version}, 현재 {CurrentVersion})");
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
    /// _factory가 가진 모든 장치의 현재 상태를 Json 파일로 저장한다. 파일 이름은 저장하는 시점의 yyyyMMdd_HHmmss_fff다.
    /// 저장할 항목이 하나도 없거나 쓰기에 실패하면 false.
    /// </summary>
    public static bool Save(PackFactory _factory)
    {
        SaveJson data = Build(_factory);
        if (data.packs.Count == 0)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>("저장할 Pack이 없습니다.");
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

    public static SaveJson Build(PackFactory _factory)
    {
        SaveJson data = new SaveJson { version = CurrentVersion };
        if (_factory == null)
        {
            return data;
        }

        // 크기와 간격은 생성 뒤에도 바뀌므로, 저장하기 전에 모델 값을 씬의 현재 상태로 맞춘다.
        _factory.RefreshValues();

        IReadOnlyList<IDeviceNode> packList = _factory.GetNodes(DeviceType.Pack);
        for (int i = 0; i < packList.Count; i++)
        {
            PackJson packJson = new PackJson();
            Fill(packJson, packList[i]);
            foreach (IDeviceNode moduleAssembly in packList[i].Children)
            {
                ModuleAssemblyJson moduleAssemblyJson = new ModuleAssemblyJson();
                Fill(moduleAssemblyJson, moduleAssembly);
                foreach (IDeviceNode module in moduleAssembly.Children)
                {
                    ModuleJson moduleJson = new ModuleJson();
                    Fill(moduleJson, module);
                    foreach (IDeviceNode parallelAssembly in module.Children)
                    {
                        ParallelAssemblyJson parallelAssemblyJson = new ParallelAssemblyJson();
                        Fill(parallelAssemblyJson, parallelAssembly);
                        foreach (IDeviceNode cell in parallelAssembly.Children)
                        {
                            CellJson cellJson = new CellJson();
                            Fill(cellJson, cell);
                            parallelAssemblyJson.cells.Add(cellJson);
                        }

                        moduleJson.parallelAssemblies.Add(parallelAssemblyJson);
                    }

                    moduleAssemblyJson.modules.Add(moduleJson);
                }

                packJson.moduleAssemblies.Add(moduleAssemblyJson);
            }

            data.packs.Add(packJson);
        }

        return data;
    }

    /// <summary>_node의 이름, 위치, 몸체 크기와 중심, 자식 간격을 _json에 옮겨 담는다.</summary>
    private static void Fill(NodeJson _json, IDeviceNode _node)
    {
        _json.name = _node.Name;
        _json.deviceId = _node.Values.deviceId;
        if (_node.Position == null)
        {
            return;
        }

        DeviceNodeValues values = _node.Values;
        _json.position = values.position;
        _json.size = values.size;
        _json.hasBodyCenter = true;
        _json.bodyCenter = values.bodyCenter;
        for (int axis = 0; axis < 3; axis++)
        {
            _json.childSpacing.values[axis] = values.spacing[axis];
            _json.childSpacing.applied[axis] = values.spacingApplied[axis];
        }
    }
    #endregion
}
