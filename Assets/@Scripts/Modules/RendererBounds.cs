using System;
using UnityEngine;

/// <summary>
/// 오브젝트 아래 Renderer들을 하나로 합친 월드 Bounds 계산. 씬 오브젝트를 만들거나 바꾸지 않고 읽기만 한다.
/// </summary>
public static class RendererBounds
{
    /// <summary>
    /// _target과 그 자식의 Renderer를 모두 감싸는 월드 Bounds. 합칠 Renderer가 하나도 없으면 false.
    /// _includeInactive가 false면 꺼진 오브젝트의 Renderer는 제외한다. _filter가 있으면 true를 돌려주는 Renderer만 합친다.
    /// </summary>
    public static bool TryGetBounds(Transform _target, out Bounds _bounds, bool _includeInactive = true, Predicate<Renderer> _filter = null)
    {
        _bounds = default;
        if (_target == null)
        {
            return false;
        }

        bool found = false;
        Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(_includeInactive);
        for (int i = 0; i < rendererArray.Length; i++)
        {
            if (_filter != null && !_filter(rendererArray[i]))
            {
                continue;
            }

            if (!found)
            {
                _bounds = rendererArray[i].bounds;
                found = true;
            }
            else
            {
                _bounds.Encapsulate(rendererArray[i].bounds);
            }
        }

        return found;
    }
}
