using Incheol.Modules;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Root 바로 아래에 놓이는 Pack의 자리를 정한다. 씬 오브젝트를 만들거나 지우지 않고 이미 만들어진 _item을 옮기기만 한다.
/// Pack은 한 줄(+x 방향)로만 놓인다.
/// </summary>
public static class PackGridPlacer
{
    /// <summary>
    /// _item(Root 바로 아래의 Pack)을 같은 Root에 이미 놓인 항목과 겹치지 않는 첫 자리로 옮긴다(+x 방향, 가로 크기 + _padding 단위).
    /// 첫 항목이거나 Renderer가 없어 크기를 잴 수 없으면 옮기지 않는다.
    /// </summary>
    public static void PlaceInRoot(Transform _item, Transform _root, float _padding)
    {
        if (!RendererBounds.TryGetBounds(_item, out Bounds own))
        {
            return;
        }

        List<Bounds> occupied = PackLayout.GetOccupiedBounds(_root, _item);
        if (occupied.Count > 0 && PackLayout.TryFindFreeOffset(own, _padding, occupied, out Vector3 offset))
        {
            _item.position += offset;
        }
    }
}
