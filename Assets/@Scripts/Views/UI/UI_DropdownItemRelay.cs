using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// TMP_Dropdown은 이미 선택된 항목을 다시 눌러도 onValueChanged가 발생하지 않는다.
/// 템플릿 Item에 붙여 두면 Show() 때 복제되는 모든 옵션 항목이 클릭을 직접 UI_MainSceneView에 전달한다.
/// </summary>
public class UI_DropdownItemRelay : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData _eventData)
    {
        TMP_Text label = GetComponentInChildren<TMP_Text>();
        if (label == null)
        {
            return;
        }

        GetComponentInParent<UI_MainSceneView>()?.OnDropdownItemClicked(label.text);
    }
}
