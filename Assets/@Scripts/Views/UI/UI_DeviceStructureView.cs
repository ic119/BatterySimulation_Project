using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 선택된 항목의 자식 구조(Pack → Module → Cell)를 contentRect 아래에 들여쓴 text 줄로 보여준다.
/// 클릭 같은 상호작용은 없고, 줄 text는 코드로 만든다(UI_TreeNodeItem 프리팹을 쓰지 않는다).
/// 항목마다 줄을 만들지 않고 VirtualScrollList로 화면에 보이는 줄만 만들어 스크롤할 때 돌려 쓴다.
/// </summary>
public class UI_DeviceStructureView : MonoBehaviour
{
    private struct Entry
    {
        public string label;
        public int depth;
        public bool isSelf;
    }

    #region Variable
    [Header("DeviceStructure Variable")]
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private float fontSize = 18.0f;
    [SerializeField, Min(1.0f), Tooltip("한 줄의 높이. fontSize보다 약간 커야 글자가 잘리지 않는다.")] private float lineHeight = 30.0f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField, Tooltip("선택된 항목 본인 줄의 색(굵게 표시됨)")] private Color selfTextColor = new Color(1.0f, 0.85f, 0.3f, 1.0f);
    [SerializeField, Tooltip("깊이 한 단계당 들여쓰기 폭(px)")] private float indentWidth = 18.0f;

    private readonly List<Entry> entryList = new List<Entry>();
    private VirtualScrollList<TextMeshProUGUI> list;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (contentRect != null)
        {
            list = new VirtualScrollList<TextMeshProUGUI>(contentRect, lineHeight, CreateLine, BindLine);
        }
    }

    private void OnDestroy()
    {
        list?.Dispose();
    }
    #endregion

    #region Method
    /// <summary>
    /// 선택된 항목 본인(_selfLabel)을 맨 위에 굵게 selfTextColor로 보여주고, 이어서 자식 구조(_childList의 이름, 깊이)를 한 단계 들여써서 보여준다.
    /// 자식의 깊이 0이 본인의 직접 자식이다.
    /// </summary>
    public void Show(string _selfLabel, IReadOnlyList<(string label, int depth)> _childList)
    {
        if (list == null)
        {
            return;
        }

        entryList.Clear();
        entryList.Add(new Entry { label = _selfLabel, depth = 0, isSelf = true });

        int childCount = _childList != null ? _childList.Count : 0;
        for (int i = 0; i < childCount; i++)
        {
            entryList.Add(new Entry { label = _childList[i].label, depth = _childList[i].depth + 1, isSelf = false });
        }

        list.SetCount(entryList.Count);
    }

    /// <summary>보여주던 구조를 모두 숨긴다.</summary>
    public void Clear()
    {
        entryList.Clear();
        list?.SetCount(0);
    }

    /// <summary>VirtualScrollList가 부르는 줄 오브젝트 생성.</summary>
    private TextMeshProUGUI CreateLine()
    {
        GameObject lineObject = new GameObject("StructureLine", typeof(RectTransform));
        lineObject.transform.SetParent(contentRect, false);

        TextMeshProUGUI line = lineObject.AddComponent<TextMeshProUGUI>();
        line.fontSize = fontSize;
        line.color = textColor;
        line.alignment = TextAlignmentOptions.TopLeft;
        line.overflowMode = TextOverflowModes.Truncate;
        line.raycastTarget = false;
        return line;
    }

    /// <summary>VirtualScrollList가 부르는 줄 채우기. 줄 오브젝트는 다른 항목에 쓰였던 것이므로 글자, 굵기, 색을 모두 새로 정한다.</summary>
    private void BindLine(TextMeshProUGUI _line, int _index)
    {
        Entry entry = entryList[_index];
        _line.text = $"<indent={Mathf.RoundToInt(entry.depth * indentWidth)}>{entry.label}";
        _line.fontStyle = entry.isSelf ? FontStyles.Bold : FontStyles.Normal;
        _line.color = entry.isSelf ? selfTextColor : textColor;
    }
    #endregion
}
