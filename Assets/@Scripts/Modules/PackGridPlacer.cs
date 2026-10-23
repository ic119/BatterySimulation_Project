using Incheol.Modules;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container 바로 아래에 놓이는 항목(Pack, 단독 Module)의 자리를 정한다. 
/// 씬 오브젝트를 만들거나 지우지 않고 이미 만들어진 _target을 옮기기만 한다.
/// - PlaceInSlot: Container의 x * z 격자 칸에 놓고 칸(열, 줄)을 Pack에 기록한다.
/// - PlaceInContainer: 격자가 없을 때, 이미 놓인 항목과 겹치지 않는 첫 자리로 옮긴다.
/// </summary>
public class PackGridPlacer
{
    private readonly PackRegistry registry;

    public PackGridPlacer(PackRegistry _registry)
    {
        registry = _registry;
    }

    #region PlaceInContainer
    /// <summary>
    /// _item(Container 바로 아래의 Pack / 단독 Module)을 같은 Container에 이미 놓인 항목과 겹치지 않는 첫 자리로 옮긴다(Module / Cell을 놓을 때와 같은 겹침 검사).
    /// 첫 항목이거나 Renderer가 없어 크기를 잴 수 없으면 옮기지 않는다.
    /// </summary>
    public static void PlaceInContainer(Transform _item, Transform _container, float _padding)
    {
        if (!TryGetBounds(_item, out Bounds own))
        {
            return;
        }

        List<Bounds> occupied = PackLayout.GetOccupiedBounds(_container, _item);
        if (occupied.Count > 0 && PackLayout.TryFindFreeOffset(own, _padding, occupied, out Vector3 offset))
        {
            _item.position += offset;
        }
    }
    #endregion

    #region PlaceInSlot
    /// <summary>
    /// _packTransform(방금 만든 Pack)을 Container의 x(_columns) * z(_rows) 격자 칸에 놓고 칸(열, 줄)을 _pack에 기록한다.
    /// 칸 크기는 Pack 크기 + 축별 간격이고, Container에 사용자가 조절한 간격(SubDeviceSpacingData)이 있으면 그 축은 _padding 대신 그 간격을 쓴다.
    /// 칸을 기록한 적 없는 Pack(격자를 정하기 전에 만든 Pack, 예전 파일의 Pack)은 현재 위치를 칸 크기로 나눠 어느 칸인지 구해 그 칸을 차지한 것으로 본다.
    /// (_requestedColumn, _requestedRow)가 비어 있으면 그 칸에(저장된 파일을 불러올 때), 아니면 첫 줄부터 x 방향으로 훑어 가장 먼저 나오는 빈 칸에 놓는다.
    /// 이미 놓인 Pack이 있으면 그 Pack의 면을 기준으로 위치를 정한다(자세한 규칙은 ResolveMinX / ResolveMaxZ).
    /// 빈 칸이 없거나 Renderer가 없어 크기를 잴 수 없으면 false이고 칸은 기록하지 않는다(= -1).
    /// </summary>
    public bool PlaceInSlot(PackDataModel _pack, Transform _packTransform, ContainerDataModel _container, int _columns, int _rows, int _requestedColumn, int _requestedRow, float _padding)
    {
        _pack.slotColumn = -1;
        _pack.slotRow = -1;

        if (_columns <= 0 || _rows <= 0 || !TryGetBounds(_packTransform, out Bounds own))
        {
            return false;
        }

        Vector3 size = own.size;
        GetPadding(_container, _padding, out float paddingX, out float paddingZ);

        float pitchX = size.x + paddingX;
        float pitchZ = size.z + paddingZ;
        if (pitchX <= 0.0f || pitchZ <= 0.0f)
        {
            return false;
        }

        bool[,] used = MarkUsedSlots(_pack, _container, _columns, _rows, pitchX, pitchZ);
        if (!TryPickSlot(used, _columns, _rows, _requestedColumn, _requestedRow, out int column, out int row))
        {
            return false;
        }

        // 이미 놓인 Pack(크기를 바꿨거나 간격이 조절됐을 수 있다)을 기준으로 위치를 정한다.
        // 기준이 될 Pack이 하나도 없을 때만 원점 기준 칸 크기(pitch)로 계산한다.
        List<KeyValuePair<PackDataModel, Bounds>> placedList = CollectPlaced(_pack, _container);

        float targetMinX = own.min.x + pitchX * column;
        float targetMaxZ = own.max.z - pitchZ * row;
        if (placedList.Count > 0)
        {
            targetMinX = ResolveMinX(placedList, column, targetMinX, paddingX, pitchX, size.x);
            targetMaxZ = ResolveMaxZ(placedList, row, targetMaxZ, paddingZ, pitchZ, size.z);
        }

        _packTransform.position += new Vector3(targetMinX - own.min.x, 0.0f, targetMaxZ - own.max.z);
        _pack.slotColumn = column;
        _pack.slotRow = row;
        return true;
    }

    /// <summary>_target 아래 모든 Renderer(꺼진 것 포함)를 감싸는 월드 Bounds. Renderer가 없으면 false.</summary>
    private static bool TryGetBounds(Transform _target, out Bounds _bounds)
    {
        Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
        if (rendererArray.Length == 0)
        {
            _bounds = default;
            return false;
        }

        _bounds = rendererArray[0].bounds;
        for (int i = 1; i < rendererArray.Length; i++)
        {
            _bounds.Encapsulate(rendererArray[i].bounds);
        }

        return true;
    }

    /// <summary>x(0), z(2) 방향 칸 사이 간격. Container에 조절한 간격이 있으면 그 축은 그 값, 없으면 _padding.</summary>
    private static void GetPadding(ContainerDataModel _container, float _padding, out float _paddingX, out float _paddingZ)
    {
        _paddingX = _padding;
        _paddingZ = _padding;
        if (_container.containerPosition.TryGetComponent(out SubDeviceSpacingData data))
        {
            _paddingX = data.TryGet(0, out float spacingX) ? spacingX : _paddingX;
            _paddingZ = data.TryGet(2, out float spacingZ) ? spacingZ : _paddingZ;
        }
    }

    /// <summary>
    /// _pack을 뺀 Container의 Pack들이 차지한 칸을 표시한다. 칸을 기록한 적 없는 Pack은 지금 위치에서 칸을 구해 기록해 둔다(첫 Pack은 원점이라 0열 0줄).
    /// 격자 밖의 칸은 표시하지 않는다.
    /// </summary>
    private bool[,] MarkUsedSlots(PackDataModel _pack, ContainerDataModel _container, int _columns, int _rows, float _pitchX, float _pitchZ)
    {
        bool[,] used = new bool[_columns, _rows];
        for (int i = 0; i < _container.packsList.Count; i++)
        {
            PackDataModel other = _container.packsList[i];
            if (other == _pack)
            {
                continue;
            }

            if (other.slotColumn < 0 || other.slotRow < 0)
            {
                Transform otherTransform = registry.GetPackTransform(other);
                if (otherTransform != null)
                {
                    other.slotColumn = Mathf.Max(0, Mathf.RoundToInt(otherTransform.position.x / _pitchX));
                    other.slotRow = Mathf.Max(0, Mathf.RoundToInt(-otherTransform.position.z / _pitchZ));
                }
            }

            if (other.slotColumn >= 0 && other.slotColumn < _columns && other.slotRow >= 0 && other.slotRow < _rows)
            {
                used[other.slotColumn, other.slotRow] = true;
            }
        }

        return used;
    }

    /// <summary>요청한 칸이 격자 안이고 비어 있으면 그 칸, 아니면 첫 줄부터 x 방향으로 훑어 가장 먼저 나오는 빈 칸. 빈 칸이 없으면 false.</summary>
    private static bool TryPickSlot(bool[,] _used, int _columns, int _rows, int _requestedColumn, int _requestedRow, out int _column, out int _row)
    {
        _column = -1;
        _row = -1;
        if (_requestedColumn >= 0 && _requestedColumn < _columns && _requestedRow >= 0 && _requestedRow < _rows && !_used[_requestedColumn, _requestedRow])
        {
            _column = _requestedColumn;
            _row = _requestedRow;
        }

        for (int r = 0; _column < 0 && r < _rows; r++)
        {
            for (int c = 0; _column < 0 && c < _columns; c++)
            {
                if (!_used[c, r])
                {
                    _column = c;
                    _row = r;
                }
            }
        }

        return _column >= 0;
    }

    /// <summary>칸이 기록된 다른 Pack들의 몸체 Bounds. 몸체(자식 Module 제외)를 찾지 못하면 첫 Renderer의 Bounds를 쓴다. Renderer가 없는 Pack은 뺀다.</summary>
    private List<KeyValuePair<PackDataModel, Bounds>> CollectPlaced(PackDataModel _pack, ContainerDataModel _container)
    {
        List<KeyValuePair<PackDataModel, Bounds>> placedList = new List<KeyValuePair<PackDataModel, Bounds>>();
        for (int i = 0; i < _container.packsList.Count; i++)
        {
            PackDataModel other = _container.packsList[i];
            Transform otherTransform = other == _pack ? null : registry.GetPackTransform(other);
            if (otherTransform == null || other.slotColumn < 0 || other.slotRow < 0)
            {
                continue;
            }

            Renderer[] otherRendererArray = otherTransform.GetComponentsInChildren<Renderer>(true);
            if (otherRendererArray.Length == 0)
            {
                continue;
            }

            Bounds otherBounds = PackLayout.TryGetPackBody(other, otherTransform, out Renderer otherBody) ? otherBody.bounds : otherRendererArray[0].bounds;
            placedList.Add(new KeyValuePair<PackDataModel, Bounds>(other, otherBounds));
        }

        return placedList;
    }

    /// <summary>
    /// 새 Pack의 왼쪽 면(min.x)이 놓일 x. 같은 열에 Pack이 있으면 그 왼쪽 면에, 같은 열이 없으면 가까운 앞쪽 열 Pack의 오른쪽 면에서 간격만큼(사이의 빈 열은 칸 크기만큼 더) 떨어뜨린다.
    /// 앞쪽 열도 없으면 가까운 뒤쪽 열 Pack의 왼쪽 면에서 간격과 Pack 폭만큼 거슬러 올라간다. _fallback은 기준이 될 Pack이 없을 때의 값이다.
    /// </summary>
    private static float ResolveMinX(List<KeyValuePair<PackDataModel, Bounds>> _placedList, int _column, float _fallback, float _paddingX, float _pitchX, float _sizeX)
    {
        float target = _fallback;
        bool found = false;
        for (int i = 0; i < _placedList.Count; i++)
        {
            if (_placedList[i].Key.slotColumn == _column)
            {
                target = found ? Mathf.Min(target, _placedList[i].Value.min.x) : _placedList[i].Value.min.x;
                found = true;
            }
        }

        if (found)
        {
            return target;
        }

        int prevColumn = -1;
        int nextColumn = int.MaxValue;
        for (int i = 0; i < _placedList.Count; i++)
        {
            int other = _placedList[i].Key.slotColumn;
            prevColumn = other < _column ? Mathf.Max(prevColumn, other) : prevColumn;
            nextColumn = other > _column ? Mathf.Min(nextColumn, other) : nextColumn;
        }

        if (prevColumn >= 0)
        {
            float edge = float.MinValue;
            for (int i = 0; i < _placedList.Count; i++)
            {
                edge = _placedList[i].Key.slotColumn == prevColumn ? Mathf.Max(edge, _placedList[i].Value.max.x) : edge;
            }

            return edge + _paddingX + (_column - prevColumn - 1) * _pitchX;
        }

        if (nextColumn != int.MaxValue)
        {
            float edge = float.MaxValue;
            for (int i = 0; i < _placedList.Count; i++)
            {
                edge = _placedList[i].Key.slotColumn == nextColumn ? Mathf.Min(edge, _placedList[i].Value.min.x) : edge;
            }

            return edge - _paddingX - _sizeX - (nextColumn - _column - 1) * _pitchX;
        }

        return target;
    }

    /// <summary>
    /// 새 Pack의 뒤쪽 면(max.z)이 놓일 z. 줄은 늘수록 -z 쪽이다. 같은 줄에 Pack이 있으면 그 뒤쪽 면에, 같은 줄이 없으면 가까운 앞쪽 줄 Pack의 앞면(min.z)에서 간격만큼 떨어뜨린다.
    /// 앞쪽 줄도 없으면 가까운 뒤쪽 줄 Pack의 뒤쪽 면에서 간격과 Pack 깊이만큼 거슬러 올라간다. _fallback은 기준이 될 Pack이 없을 때의 값이다.
    /// </summary>
    private static float ResolveMaxZ(List<KeyValuePair<PackDataModel, Bounds>> _placedList, int _row, float _fallback, float _paddingZ, float _pitchZ, float _sizeZ)
    {
        float target = _fallback;
        bool found = false;
        for (int i = 0; i < _placedList.Count; i++)
        {
            if (_placedList[i].Key.slotRow == _row)
            {
                target = found ? Mathf.Max(target, _placedList[i].Value.max.z) : _placedList[i].Value.max.z;
                found = true;
            }
        }

        if (found)
        {
            return target;
        }

        int prevRow = -1;
        int nextRow = int.MaxValue;
        for (int i = 0; i < _placedList.Count; i++)
        {
            int other = _placedList[i].Key.slotRow;
            prevRow = other < _row ? Mathf.Max(prevRow, other) : prevRow;
            nextRow = other > _row ? Mathf.Min(nextRow, other) : nextRow;
        }

        if (prevRow >= 0)
        {
            float edge = float.MaxValue;
            for (int i = 0; i < _placedList.Count; i++)
            {
                edge = _placedList[i].Key.slotRow == prevRow ? Mathf.Min(edge, _placedList[i].Value.min.z) : edge;
            }

            return edge - _paddingZ - (_row - prevRow - 1) * _pitchZ;
        }

        if (nextRow != int.MaxValue)
        {
            float edge = float.MinValue;
            for (int i = 0; i < _placedList.Count; i++)
            {
                edge = _placedList[i].Key.slotRow == nextRow ? Mathf.Max(edge, _placedList[i].Value.max.z) : edge;
            }

            return edge + _paddingZ + _sizeZ + (nextRow - _row - 1) * _pitchZ;
        }

        return target;
    }
    #endregion
}
