using Incheol.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 키 입력.
/// - A : Pack을 만든다(그 아래 ModuleAssembly > Module > ParallelAssembly > Cell도 하나씩 함께). 생성 버튼과 같은 경로를 쓰고, 만든 Pack을 선택해 보이게 한다.
/// - S : 기준 Cell(선택된 Cell, 없으면 가장 최근에 만든 Cell)의 월드 +x 방향 옆에 Cell을 하나 만든다.
/// - D : 같은 기준 Cell의 월드 +z 방향 옆에 Cell을 하나 만든다.
/// 생성 규칙(부모 몸체 안, 겹침 없음, 간격)은 UI_MainSceneView.CreateCellBeside가 맡고 여기서는 키만 받는다.
/// MainScene의 아무 오브젝트에 붙이면 된다.
/// </summary>
public class TestController : MonoBehaviour
{
    // UI_MainScene 프리팹은 런타임에 만들어지므로 처음 필요할 때 찾는다.
    private UI_MainSceneView mainSceneView;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || IsTyping())
        {
            return;
        }

        if (keyboard.aKey.wasPressedThisFrame)
        {
            CreatePack();
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            CreateCellBeside(Vector3.right);
        }

        if (keyboard.dKey.wasPressedThisFrame)
        {
            CreateCellBeside(Vector3.forward);
        }
    }

    private void CreatePack()
    {
        UI_MainSceneView view = GetMainSceneView();
        if (view != null)
        {
            view.CreatePack(true);
        }
    }

    private void CreateCellBeside(Vector3 _direction)
    {
        UI_MainSceneView view = GetMainSceneView();
        if (view != null)
        {
            view.CreateCellBeside(_direction);
        }
    }

    /// <summary>UI_MainScene 프리팹은 런타임에 만들어지므로 처음 필요할 때 찾는다. 아직 없으면 경고하고 null.</summary>
    private UI_MainSceneView GetMainSceneView()
    {
        if (mainSceneView == null)
        {
            mainSceneView = FindAnyObjectByType<UI_MainSceneView>();
        }

        if (mainSceneView == null)
        {
            DebugLogManager.GenerateWarningMessage<TestController>("UI_MainSceneView를 찾지 못했습니다. UI_MainScene이 만들어진 뒤에 눌러 주세요.");
        }

        return mainSceneView;
    }

    /// <summary>이름 변경 같은 입력 필드에 글자를 치는 중이면 단축키로 보지 않는다.</summary>
    private static bool IsTyping()
    {
        GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return focused != null && focused.GetComponent<TMP_InputField>() != null;
    }
}
