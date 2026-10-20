using System;
using UnityEngine;

/// <summary>
/// ScrollRect의 Viewport 크기가 바뀔 때 알려 준다. VirtualScrollList가 보이는 줄 수를 다시 계산하는 데 쓰며, 코드가 필요할 때 붙인다.
/// </summary>
public class UI_ViewportWatcher : MonoBehaviour
{
    /// <summary>Viewport의 크기가 바뀌었을 때 발생한다.</summary>
    public event Action Resized;

    private void OnRectTransformDimensionsChange()
    {
        Resized?.Invoke();
    }
}
