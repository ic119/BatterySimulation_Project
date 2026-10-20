using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 항목의 자식 구조(Pack → Module → Cell)를 contentRect 아래에 들여쓴 text 줄로 보여준다.
/// 클릭 같은 상호작용은 없고, 줄 text는 코드로 만들어 재사용한다(UI_TreeNodeItem 프리팹을 쓰지 않는다).
/// </summary>
public class UI_DeviceStructureView : MonoBehaviour
{
    #region Variable
    [Header("DeviceStructure Variable")]
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private float fontSize = 18.0f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField, Tooltip("선택된 항목 본인 줄의 색(굵게 표시됨)")] private Color selfTextColor = new Color(1.0f, 0.85f, 0.3f, 1.0f);
    [SerializeField, Tooltip("깊이 한 단계당 들여쓰기 폭(px)")] private float indentWidth = 18.0f;

    private readonly List<TextMeshProUGUI> lineList = new List<TextMeshProUGUI>();
    #endregion

    #region LifeCycle
    private void Awake()
    {
        EnsureLayout();
    }
    #endregion

    #region Method
    /// <summary>
    /// contentRect에 줄을 위에서 아래로 쌓고 내용 높이에 맞게 늘려 주는 레이아웃이 없으면 만든다.
    /// 이미 프리팹에 있으면 그대로 쓴다.
    /// </summary>
    private void EnsureLayout()
    {
        if (contentRect == null)
        {
            return;
        }

        if (contentRect.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        if (contentRect.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>
    /// 선택된 항목 본인(_selfLabel)을 맨 위에 굵게 selfTextColor로 보여주고, 이어서 자식 구조(_childList의 이름, 깊이)를 한 단계 들여써서 보여준다.
    /// 자식의 깊이 0이 본인의 직접 자식이다.
    /// </summary>
    public void Show(string _selfLabel, IReadOnlyList<(string label, int depth)> _childList)
    {
        if (contentRect == null)
        {
            return;
        }

        SetLine(0, _selfLabel, 0, true);

        int childCount = _childList != null ? _childList.Count : 0;
        for (int i = 0; i < childCount; i++)
        {
            SetLine(i + 1, _childList[i].label, _childList[i].depth + 1, false);
        }

        // 남는 줄은 지우지 않고 숨겨 두었다가 다음에 다시 쓴다.
        for (int i = childCount + 1; i < lineList.Count; i++)
        {
            lineList[i].gameObject.SetActive(false);
        }
    }

    private void SetLine(int _index, string _label, int _depth, bool _isSelf)
    {
        TextMeshProUGUI line = GetLine(_index);
        line.text = $"<indent={Mathf.RoundToInt(_depth * indentWidth)}>{_label}";
        line.fontStyle = _isSelf ? FontStyles.Bold : FontStyles.Normal;
        line.color = _isSelf ? selfTextColor : textColor;
        line.gameObject.SetActive(true);
    }

    /// <summary>보여주던 구조를 모두 숨긴다.</summary>
    /// <summary>보여주던 구조를 모두 숨긴다.</summary>
    public void Clear()
    {
        for (int i = 0; i < lineList.Count; i++)
        {
            lineList[i].gameObject.SetActive(false);
        }
    }

    private TextMeshProUGUI GetLine(int _index)
    {
        while (lineList.Count <= _index)
        {
            GameObject lineObject = new GameObject($"StructureLine_{lineList.Count}", typeof(RectTransform));
            lineObject.transform.SetParent(contentRect, false);

            TextMeshProUGUI line = lineObject.AddComponent<TextMeshProUGUI>();
            line.fontSize = fontSize;
            line.color = textColor;
            line.alignment = TextAlignmentOptions.TopLeft;
            line.raycastTarget = false;
            lineList.Add(line);
        }

        return lineList[_index];
    }
    #endregion
}
