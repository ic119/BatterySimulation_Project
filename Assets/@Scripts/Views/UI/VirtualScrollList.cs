using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ScrollRect의 Content 아래 줄이 아주 많아도 화면에 보이는 만큼만 줄 오브젝트를 만들어 돌려 쓰는 목록.
/// 줄 높이는 모두 같다고 보고, 줄 수 × 줄 높이로 Content 높이를 정한 뒤 스크롤 위치에 맞는 줄만 보이는 자리에 놓고 내용을 다시 채운다.
/// 줄 오브젝트 수는 보이는 줄 수 + 여분(buffer)으로 일정하다. 그래서 항목이 수천 개가 되어도 오브젝트와 메모리는 늘지 않는다.
/// Content에 있던 LayoutGroup / ContentSizeFitter는 줄 위치와 높이를 이 목록이 정하므로 꺼 둔다.
/// </summary>
public class VirtualScrollList<TRow> where TRow : Component
{
    private readonly RectTransform content;
    private readonly ScrollRect scrollRect;
    private readonly float rowHeight;
    private readonly int buffer;
    private readonly Func<TRow> createRow;
    private readonly Action<TRow, int> bindRow;

    private readonly List<TRow> rowList = new List<TRow>();
    private readonly List<int> boundIndexList = new List<int>();
    private UI_ViewportWatcher watcher;
    private int count;
    private bool dirty = true;

    /// <param name="_content">ScrollRect의 Content. 줄이 이 아래에 놓인다.</param>
    /// <param name="_rowHeight">줄 하나의 높이(모든 줄 같음).</param>
    /// <param name="_createRow">줄 오브젝트를 만든다. 아직 만들 수 없으면(프리팹을 불러오는 중 등) null을 돌려주면 나중에 Refresh로 다시 시도한다.</param>
    /// <param name="_bindRow">줄을 _index번째 항목의 내용으로 채운다. 같은 줄 오브젝트가 다른 항목에 다시 쓰이므로 모든 표시를 새로 정해야 한다.</param>
    /// <param name="_buffer">화면 위아래로 미리 채워 두는 줄 수.</param>
    public VirtualScrollList(RectTransform _content, float _rowHeight, Func<TRow> _createRow, Action<TRow, int> _bindRow, int _buffer = 2)
    {
        content = _content;
        rowHeight = Mathf.Max(1.0f, _rowHeight);
        createRow = _createRow;
        bindRow = _bindRow;
        buffer = Mathf.Max(0, _buffer);
        scrollRect = content.GetComponentInParent<ScrollRect>();

        ConfigureContent();

        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScroll);

            // 처음 그릴 때는 보이는 영역 크기가 아직 정해지지 않았을 수 있어, 크기가 바뀌면 다시 채운다.
            RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
            watcher = viewport.GetComponent<UI_ViewportWatcher>();
            if (watcher == null)
            {
                watcher = viewport.gameObject.AddComponent<UI_ViewportWatcher>();
            }

            watcher.Resized += Refresh;
        }
    }

    /// <summary>항목 수를 정하고 Content 높이를 맞춘 뒤 보이는 줄을 다시 채운다.</summary>
    public void SetCount(int _count)
    {
        count = Mathf.Max(0, _count);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, count * rowHeight);
        Refresh();
    }

    /// <summary>항목 수는 그대로 두고 보이는 줄의 내용을 모두 다시 채운다. 항목의 표시가 바뀌었을 때 부른다.</summary>
    public void Refresh()
    {
        dirty = true;
        UpdateVisible();
    }

    /// <summary>쓰던 이벤트 연결과 줄 오브젝트를 정리한다.</summary>
    public void Dispose()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        if (watcher != null)
        {
            watcher.Resized -= Refresh;
        }

        for (int i = 0; i < rowList.Count; i++)
        {
            if (rowList[i] != null)
            {
                UnityEngine.Object.Destroy(rowList[i].gameObject);
            }
        }

        rowList.Clear();
        boundIndexList.Clear();
    }

    private void ConfigureContent()
    {
        LayoutGroup[] layoutGroupArray = content.GetComponents<LayoutGroup>();
        for (int i = 0; i < layoutGroupArray.Length; i++)
        {
            layoutGroupArray[i].enabled = false;
        }

        ContentSizeFitter[] fitterArray = content.GetComponents<ContentSizeFitter>();
        for (int i = 0; i < fitterArray.Length; i++)
        {
            fitterArray[i].enabled = false;
        }

        // 위쪽 기준으로 가로는 가득 채우고, 세로 길이는 SetCount가 정한다.
        content.anchorMin = new Vector2(0.0f, 1.0f);
        content.anchorMax = new Vector2(1.0f, 1.0f);
        content.pivot = new Vector2(0.5f, 1.0f);
        content.offsetMin = new Vector2(0.0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0.0f, content.offsetMax.y);
    }

    private void OnScroll(Vector2 _position)
    {
        UpdateVisible();
    }

    private float GetViewportHeight()
    {
        if (scrollRect != null && scrollRect.viewport != null)
        {
            return scrollRect.viewport.rect.height;
        }

        RectTransform parent = content.parent as RectTransform;
        return parent != null ? parent.rect.height : 0.0f;
    }

    /// <summary>스크롤 위치에 보이는 줄 범위를 구해 돌려 쓰는 줄 오브젝트에 채우고, 범위 밖의 줄은 끈다.</summary>
    private void UpdateVisible()
    {
        float offset = Mathf.Max(0.0f, content.anchoredPosition.y);
        int first = Mathf.Max(0, Mathf.FloorToInt(offset / rowHeight) - buffer);
        int last = Mathf.Min(count - 1, Mathf.CeilToInt((offset + GetViewportHeight()) / rowHeight) + buffer);
        int needed = count == 0 ? 0 : last - first + 1;

        // 보이는 줄이 늘어나면 줄 오브젝트를 더 만든다. 자리(인덱스 % 줄 수)가 바뀌므로 전부 다시 채운다.
        while (rowList.Count < needed)
        {
            TRow row = createRow();
            if (row == null)
            {
                return;
            }

            ConfigureRow(row);
            rowList.Add(row);
            boundIndexList.Add(-1);
            dirty = true;
        }

        for (int index = first; index <= last && needed > 0; index++)
        {
            int slot = index % rowList.Count;
            TRow row = rowList[slot];
            if (dirty || boundIndexList[slot] != index)
            {
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0.0f, -index * rowHeight);
                bindRow(row, index);
                boundIndexList[slot] = index;
            }

            row.gameObject.SetActive(true);
        }

        for (int slot = 0; slot < rowList.Count; slot++)
        {
            int bound = boundIndexList[slot];
            if (bound != -1 && (needed == 0 || bound < first || bound > last))
            {
                rowList[slot].gameObject.SetActive(false);
                boundIndexList[slot] = -1;
            }
        }

        dirty = false;
    }

    private void ConfigureRow(TRow _row)
    {
        RectTransform rect = (RectTransform)_row.transform;
        rect.anchorMin = new Vector2(0.0f, 1.0f);
        rect.anchorMax = new Vector2(1.0f, 1.0f);
        rect.pivot = new Vector2(0.5f, 1.0f);
        rect.sizeDelta = new Vector2(0.0f, rowHeight);
    }
}
