using Incheol.Utils;

/// <summary>
/// 예전 버전으로 저장된 JsonDataController.SaveJson을 현재 버전(JsonDataController.CurrentVersion)의 구조로 바꾼다.
/// 불러오는 쪽(PackJsonBuilder)은 현재 버전의 구조만 알면 되도록, 버전마다 달랐던 구조 차이는 여기서 모두 흡수한다.
/// 저장 형식을 바꿀 때는 CurrentVersion을 올리고, 이전 버전에서 올리는 단계(MigrateToVersionN)를 아래에 추가한다.
/// </summary>
public static class JsonDataMigration
{
    /// <summary>
    /// _data를 현재 버전으로 바꾼다. 이미 현재 버전이면 아무것도 바꾸지 않는다(여러 번 불러도 같다).
    /// _data가 null이거나 현재 버전보다 높은(= 더 새로운 프로그램이 저장한) 파일이면 읽을 수 없으므로 false.
    /// </summary>
    public static bool TryMigrate(JsonDataController.SaveJson _data)
    {
        if (_data == null)
        {
            return false;
        }

        if (_data.version > JsonDataController.CurrentVersion)
        {
            DebugLogManager.GenerateErrorMessage<JsonDataController>($"이 프로그램(저장 형식 {JsonDataController.CurrentVersion})보다 새로운 형식({_data.version})으로 저장된 파일입니다.");
            return false;
        }

        if (_data.version < 1)
        {
            MigrateToVersion1(_data);
        }

        _data.version = JsonDataController.CurrentVersion;
        return true;
    }

    /// <summary>
    /// 버전 0 → 1: Container가 없던 예전 파일의 최상위 Pack / 단독 Module을 항목마다 새 Container에 옮겨 담고 최상위 목록을 비운다.
    /// 새 Container의 이름은 이미 있는 Container 이름 중 가장 큰 번호 다음부터 번호를 이어 붙인다.
    /// 몸체 중심(hasBodyCenter)이나 칸(hasSlot)처럼 파일마다 있을 수도 없을 수도 있는 값은 그 값이 있는지를 항목이 직접 표시하므로 건드리지 않는다.
    /// </summary>
    private static void MigrateToVersion1(JsonDataController.SaveJson _data)
    {
        int lastNumber = 0;
        for (int i = 0; i < _data.containers.Count; i++)
        {
            if (int.TryParse(_data.containers[i].name, out int number) && number > lastNumber)
            {
                lastNumber = number;
            }
        }

        for (int i = 0; i < _data.packs.Count; i++)
        {
            JsonDataController.ContainerJson container = new JsonDataController.ContainerJson { name = (++lastNumber).ToString() };
            container.packs.Add(_data.packs[i]);
            _data.containers.Add(container);
        }

        for (int i = 0; i < _data.standaloneModules.Count; i++)
        {
            JsonDataController.ContainerJson container = new JsonDataController.ContainerJson { name = (++lastNumber).ToString() };
            container.standaloneModules.Add(_data.standaloneModules[i]);
            _data.containers.Add(container);
        }

        _data.packs.Clear();
        _data.standaloneModules.Clear();
    }
}
