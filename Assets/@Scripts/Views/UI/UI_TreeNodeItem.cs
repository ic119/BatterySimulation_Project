using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 트리 한 줄(들여쓰기 / 화살표 버튼 / 이름)을 이루는 프리팹(UI_TreeNodeItem)에 붙는 컴포넌트.
/// 화살표 아이콘은 아래(▼) 모양 스프라이트이며, 접힘 상태에서는 90도 돌려 오른쪽(▶)을 가리키게 한다.
/// 줄 전체를 누르면 선택되고, 선택된 줄은 ClickedImage로 색이 바뀐다.
/// </summary>
public class UI_TreeNodeItem : MonoBehaviour
{
    #region Variable
    [Header("Tree Node Item Variable")]
    [SerializeField] private LayoutElement indent;
    [SerializeField] private Button arrowButton;
    [SerializeField] private Image arrowIcon;
    [SerializeField] private TMP_Text label;

    [Header("Click Variable")]
    [SerializeField] private Button itemButton;
    [SerializeField, Tooltip("선택된 줄에만 켜지는 강조 이미지")] private Image clickedImage;

    [Header("Delete Variable")]
    [SerializeField, Tooltip("이 줄의 Pack / Module / Cell을 삭제하는 버튼. 줄 클릭(itemButton)과 따로 눌린다.")] private Button deleteButton;

    [SerializeField] private float indentWidth = 18.0f;
    #endregion

    #region Property
    /// <summary>트리 줄에 표시되는 이름(예: "Pack 1").</summary>
    public string Label => label != null ? label.text : string.Empty;
    #endregion

    #region Method
    /// <summary>이름과 깊이(들여쓰기)를 정하고 화살표 / 줄 클릭 / 삭제 버튼 콜백을 연결한다.</summary>
    public void Setup(string _label, int _depth, UnityAction _onClickArrow, UnityAction _onClickItem, UnityAction _onClickDelete = null)
    {
        label.text = _label;
        indent.preferredWidth = _depth * indentWidth;

        arrowButton.onClick.RemoveAllListeners();
        if (_onClickArrow != null)
        {
            arrowButton.onClick.AddListener(_onClickArrow);
        }

        itemButton.onClick.RemoveAllListeners();
        if (_onClickItem != null)
        {
            itemButton.onClick.AddListener(_onClickItem);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            if (_onClickDelete != null)
            {
                deleteButton.onClick.AddListener(_onClickDelete);
            }
        }

        SetClicked(false);
    }

    /// <summary>자식이 없으면 화살표를 숨기고, 있으면 펼침(아래) / 접힘(오른쪽) 모양으로 보여준다.</summary>
    public void SetArrow(bool _hasChild, bool _expanded)
    {
        arrowButton.interactable = _hasChild;
        arrowIcon.gameObject.SetActive(_hasChild);
        arrowIcon.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, _expanded ? 0.0f : 90.0f);
    }

    /// <summary>선택 상태에 따라 ClickedImage를 켜고 끈다.</summary>
    public void SetClicked(bool _clicked)
    {
        clickedImage.gameObject.SetActive(_clicked);
    }
    #endregion
}
