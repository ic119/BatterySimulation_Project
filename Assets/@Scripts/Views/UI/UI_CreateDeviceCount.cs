using Incheol.Modules;
using UnityEngine;

/// <summary>
/// 예전 Container의 Pack 생성 수량(x * z) 패널. Container가 없어지면서 기능이 사라졌고, Pack은 Root 아래 한 줄로 놓인다.
/// 프리팹에 이 컴포넌트가 붙어 있어도 Missing Script가 되지 않도록 빈 껍데기로 남겨 두며, 패널(container)은 항상 끈다.
/// UI_MainScene 프리팹에서 이 컴포넌트와 패널 오브젝트를 지운 뒤 이 파일도 지워도 된다.
/// </summary>
public class UI_CreateDeviceCount : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private GameObject container;

    /// <summary>MainSceneManager가 프리팹을 인스턴스화한 직후 부른다. 패널을 끈다.</summary>
    public void Init(DeviceSelection _selection)
    {
        Refresh();
    }

    /// <summary>선택이 바뀔 때마다 부른다. 패널은 항상 꺼 둔다.</summary>
    public void Refresh()
    {
        if (container != null)
        {
            container.SetActive(false);
        }
    }
}
