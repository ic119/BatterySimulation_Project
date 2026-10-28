using Incheol.Modules;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pack / Module / Cell 배치에 쓰는 공간 계산 모음. 부모 내부 공간(Bounds), 이미 차지된 영역, 놓을 수 있는 칸을 구한다.
/// 씬 오브젝트를 만들거나 지우지 않고 읽기만 한다. 칸은 부모 내부를 항목 크기 + 여백(x, z만) 단위로 나눠 뒤쪽(+z) 끝에서 -z 방향, 다음 줄은 +x 방향, 바닥이 차면 위 층 순서로 채운다.
/// </summary>
public static class PackLayout
{
    /// <summary>
    /// Module 내부 공간(월드 AABB). 현재 ModulePrefab은 속이 찬 상자라 벽 두께 없이 Module 자신의 Renderer 영역 전체를 내부로 본다.
    /// 자식(Cell)의 Renderer는 포함하지 않는다. Renderer가 없으면 false.
    /// </summary>
    public static bool TryGetModuleInnerBounds(ModuleDataModel _module, out Bounds _bounds)
    {
        _bounds = default;
        if (_module == null || _module.modulePosition == null)
        {
            return false;
        }

        // ModulePrefab은 루트(pivot)에 Renderer가 없고 자식 Body에 있다. Cell도 자식이므로 Cell 아래의 Renderer는 제외한다.
        return RendererBounds.TryGetBounds(_module.modulePosition, out _bounds, false, _renderer => !IsUnderAnyCell(_renderer.transform, _module));
    }

    private static bool IsUnderAnyCell(Transform _transform, ModuleDataModel _module)
    {
        for (int i = 0; i < _module.cellsList.Count; i++)
        {
            Transform cellTransform = _module.cellsList[i].cellPosition;
            if (cellTransform != null && _transform.IsChildOf(cellTransform))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pack 몸체 Renderer(ModulePrefab과 같은 구조면 자식 Body). 자식 Module(과 그 아래 Cell)의 Renderer는 제외한다. 없으면 false.
    /// </summary>
    public static bool TryGetPackBody(PackDataModel _pack, Transform _packTransform, out Renderer _body)
    {
        _body = null;
        if (_pack == null || _packTransform == null)
        {
            return false;
        }

        Renderer[] rendererArray = _packTransform.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rendererArray.Length; i++)
        {
            if (!IsUnderAnyModule(rendererArray[i].transform, _pack))
            {
                _body = rendererArray[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>Pack에 이미 놓인 Module들이 차지한 영역(자식 Cell 포함 월드 Bounds) 목록.</summary>
    public static List<Bounds> GetOccupiedBounds(PackDataModel _pack)
    {
        List<Bounds> occupiedList = new List<Bounds>();
        for (int i = 0; i < _pack.modulesList.Count; i++)
        {
            Transform moduleTransform = _pack.modulesList[i].modulePosition;
            if (moduleTransform == null)
            {
                continue;
            }

            if (RendererBounds.TryGetBounds(moduleTransform, out Bounds bounds))
            {
                occupiedList.Add(bounds);
            }
        }

        return occupiedList;
    }

    /// <summary>
    /// Container 아래에 이미 놓인 항목(Pack, 단독 Module)이 차지한 영역(자식 Module / Cell 포함 월드 Bounds) 목록.
    /// 새로 만드는 중인 _exclude는 건너뛴다.
    /// </summary>
    public static List<Bounds> GetOccupiedBounds(Transform _container, Transform _exclude)
    {
        List<Bounds> occupiedList = new List<Bounds>();
        for (int i = 0; i < _container.childCount; i++)
        {
            Transform child = _container.GetChild(i);
            if (child == _exclude)
            {
                continue;
            }

            if (RendererBounds.TryGetBounds(child, out Bounds bounds))
            {
                occupiedList.Add(bounds);
            }
        }

        return occupiedList;
    }

    /// <summary>
    /// _own(현재 놓인 자리의 Bounds)을 +x 방향으로 (가로 크기 + _padding)씩 옮겨 가며 _occupied와 겹치지 않는 첫 자리까지의 이동량을 구한다.
    /// 첫 자리(이동량 0)가 비어 있으면 0이다. 맞닿는 면은 겹침으로 보지 않는다. 자리를 찾지 못하면 false.
    /// </summary>
    public static bool TryFindFreeOffset(Bounds _own, float _padding, List<Bounds> _occupied, out Vector3 _offset)
    {
        _offset = Vector3.zero;
        float pitch = _own.size.x + _padding;
        if (pitch <= 0.0f)
        {
            return false;
        }

        const int MaxStep = 1000;
        for (int step = 0; step < MaxStep; step++)
        {
            Vector3 offset = new Vector3(pitch * step, 0.0f, 0.0f);
            Bounds candidate = new Bounds(_own.center + offset, _own.size);

            bool overlapped = false;
            for (int i = 0; i < _occupied.Count && !overlapped; i++)
            {
                overlapped = Overlaps(candidate, _occupied[i]);
            }

            if (!overlapped)
            {
                _offset = offset;
                return true;
            }
        }

        return false;
    }

    /// <summary>두 Bounds가 겹치는지. 맞닿는 면은 겹침으로 보지 않는다.</summary>
    public static bool Overlaps(Bounds _a, Bounds _b)
    {
        const float Tolerance = 0.001f;
        return _a.min.x < _b.max.x - Tolerance && _a.max.x > _b.min.x + Tolerance &&
               _a.min.y < _b.max.y - Tolerance && _a.max.y > _b.min.y + Tolerance &&
               _a.min.z < _b.max.z - Tolerance && _a.max.z > _b.min.z + Tolerance;
    }

    /// <summary>Module에 이미 놓인 Cell들이 차지한 영역(월드 Bounds) 목록.</summary>
    public static List<Bounds> GetOccupiedBounds(ModuleDataModel _module)
    {
        List<Bounds> occupiedList = new List<Bounds>();
        for (int i = 0; i < _module.cellsList.Count; i++)
        {
            Transform cellTransform = _module.cellsList[i].cellPosition;
            if (cellTransform == null)
            {
                continue;
            }

            if (RendererBounds.TryGetBounds(cellTransform, out Bounds bounds))
            {
                occupiedList.Add(bounds);
            }
        }

        return occupiedList;
    }

    /// <summary>
    /// TryGetSlotCenter와 같은 채우기 순서로 칸을 훑으며 _occupied와 겹치지 않는 첫 칸의 중심을 구한다. 맞닿는 면은 겹침으로 보지 않는다.
    /// 빈 칸이 없으면 false이고 _capacity에는 총 수용 개수가 담긴다.
    /// </summary>
    public static bool TryFindFreeSlot(Bounds _inner, Vector3 _itemSize, float _padding, List<Bounds> _occupied, out Vector3 _center, out int _capacity)
    {
        _center = default;
        _capacity = 0;

        const float Tolerance = 0.001f;
        int index = 0;
        while (TryGetSlotCenter(_inner, _itemSize, _padding, index, out Vector3 candidateCenter, out _capacity))
        {
            Bounds candidate = new Bounds(candidateCenter, _itemSize);
            bool overlapped = false;
            for (int i = 0; i < _occupied.Count && !overlapped; i++)
            {
                Bounds other = _occupied[i];
                overlapped =
                    candidate.min.x < other.max.x - Tolerance && candidate.max.x > other.min.x + Tolerance &&
                    candidate.min.y < other.max.y - Tolerance && candidate.max.y > other.min.y + Tolerance &&
                    candidate.min.z < other.max.z - Tolerance && candidate.max.z > other.min.z + Tolerance;
            }

            if (!overlapped)
            {
                _center = candidateCenter;
                return true;
            }

            index++;
        }

        return false;
    }

    /// <summary>
    /// _parent에 사용자가 조절한 자식 간격(SubDeviceSpacingData)이 있으면 그 간격을 지키는 칸에서 첫 빈 칸을, 없으면 기존 칸 배치(TryFindFreeSlot)에서 구한다.
    /// 간격 칸은 이미 놓인 항목(_occupied)의 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)에서 시작해 항목 크기 + 간격 단위로 z(-방향), x, y 순서로 채우며,
    /// 간격을 지정하지 않은 축은 x, z는 _padding, y는 0을 간격으로 쓴다. 부모 내부(_inner)를 벗어나는 칸은 건너뛴다.
    /// </summary>
    public static bool TryFindFreeSlot(Bounds _inner, Vector3 _itemSize, float _padding, List<Bounds> _occupied, Transform _parent, out Vector3 _center, out int _capacity)
    {
        if (_parent == null || _occupied.Count == 0 || !_parent.TryGetComponent(out SubDeviceSpacingData data))
        {
            return TryFindFreeSlot(_inner, _itemSize, _padding, _occupied, out _center, out _capacity);
        }

        _center = default;
        _capacity = 0;

        Vector3 pitch = new Vector3(
            _itemSize.x + (data.TryGet(0, out float sx) ? sx : _padding),
            _itemSize.y + (data.TryGet(1, out float sy) ? sy : 0.0f),
            _itemSize.z + (data.TryGet(2, out float sz) ? sz : _padding));
        if (pitch.x <= 0.0f || pitch.y <= 0.0f || pitch.z <= 0.0f)
        {
            return TryFindFreeSlot(_inner, _itemSize, _padding, _occupied, out _center, out _capacity);
        }

        const float Tolerance = 0.001f;
        Vector3 start = new Vector3(float.MaxValue, float.MaxValue, float.MinValue);
        for (int i = 0; i < _occupied.Count; i++)
        {
            start.x = Mathf.Min(start.x, _occupied[i].min.x);
            start.y = Mathf.Min(start.y, _occupied[i].min.y);
            start.z = Mathf.Max(start.z, _occupied[i].max.z);
        }

        int countX = Mathf.Max(1, Mathf.FloorToInt((_inner.max.x - start.x) / pitch.x + Tolerance) + 1);
        int countY = Mathf.Max(1, Mathf.FloorToInt((_inner.max.y - start.y) / pitch.y + Tolerance) + 1);
        int countZ = Mathf.Max(1, Mathf.FloorToInt((start.z - _inner.min.z) / pitch.z + Tolerance) + 1);

        for (int y = 0; y < countY; y++)
        {
            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    Vector3 center = new Vector3(
                        start.x + pitch.x * x + _itemSize.x * 0.5f,
                        start.y + pitch.y * y + _itemSize.y * 0.5f,
                        start.z - pitch.z * z - _itemSize.z * 0.5f);
                    Bounds candidate = new Bounds(center, _itemSize);

                    bool inside =
                        candidate.min.x >= _inner.min.x - Tolerance && candidate.max.x <= _inner.max.x + Tolerance &&
                        candidate.min.y >= _inner.min.y - Tolerance && candidate.max.y <= _inner.max.y + Tolerance &&
                        candidate.min.z >= _inner.min.z - Tolerance && candidate.max.z <= _inner.max.z + Tolerance;
                    if (!inside)
                    {
                        continue;
                    }

                    _capacity++;
                    bool overlapped = false;
                    for (int i = 0; i < _occupied.Count && !overlapped; i++)
                    {
                        Bounds other = _occupied[i];
                        overlapped =
                            candidate.min.x < other.max.x - Tolerance && candidate.max.x > other.min.x + Tolerance &&
                            candidate.min.y < other.max.y - Tolerance && candidate.max.y > other.min.y + Tolerance &&
                            candidate.min.z < other.max.z - Tolerance && candidate.max.z > other.min.z + Tolerance;
                    }

                    if (!overlapped)
                    {
                        _center = center;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsUnderAnyModule(Transform _transform, PackDataModel _pack)
    {
        for (int i = 0; i < _pack.modulesList.Count; i++)
        {
            Transform moduleTransform = _pack.modulesList[i].modulePosition;
            if (moduleTransform != null && _transform.IsChildOf(moduleTransform))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Cell의 Renderer 영역 전체가 부모 Module 내부 공간에 들어가는지 검사한다. 검사할 수 없으면(Renderer 없음) true로 본다.
    /// _overflow에는 내부 공간을 벗어난 정도(월드 단위, 축별)가 담긴다.
    /// </summary>
    public static bool IsCellInsideModule(CellDataModel _cell, out Vector3 _overflow)
    {
        _overflow = Vector3.zero;
        Renderer cellRenderer = _cell != null && _cell.cellPosition != null ? _cell.cellPosition.GetComponent<Renderer>() : null;
        if (cellRenderer == null || !TryGetModuleInnerBounds(_cell.parentModule, out Bounds inner))
        {
            return true;
        }

        Bounds cell = cellRenderer.bounds;
        _overflow = new Vector3(
            Mathf.Max(0.0f, inner.min.x - cell.min.x, cell.max.x - inner.max.x),
            Mathf.Max(0.0f, inner.min.y - cell.min.y, cell.max.y - inner.max.y),
            Mathf.Max(0.0f, inner.min.z - cell.min.z, cell.max.z - inner.max.z));

        const float Tolerance = 0.001f;
        return _overflow.x <= Tolerance && _overflow.y <= Tolerance && _overflow.z <= Tolerance;
    }

    /// <summary>
    /// _index번째 Cell이 놓일 칸의 중심(월드)을 구한다. 부모(Module) 내부 공간을 항목 크기 + 여백(x, z만) 단위 칸으로 나눠 채운다.
    /// 채우는 순서: 뒤쪽(+z) 끝에서 -z 방향으로 한 줄을 채운 뒤 +x 방향 다음 줄, 바닥이 다 찬 뒤 위 층.
    /// 높이(y)는 항목이 부모와 비슷한 높이라 여백을 두지 않는다. 칸이 부족하면 false이고 _capacity에는 총 수용 개수가 담긴다.
    /// </summary>
    private static bool TryGetSlotCenter(Bounds _inner, Vector3 _itemSize, float _padding, int _index, out Vector3 _center, out int _capacity)
    {
        _center = default;
        _capacity = 0;

        float slotX = _itemSize.x + _padding;
        float slotY = _itemSize.y;
        float slotZ = _itemSize.z + _padding;
        if (slotX <= 0.0f || slotY <= 0.0f || slotZ <= 0.0f)
        {
            return false;
        }

        const float Tolerance = 0.0001f;
        int countX = Mathf.FloorToInt(_inner.size.x / slotX + Tolerance);
        int countY = Mathf.FloorToInt(_inner.size.y / slotY + Tolerance);
        int countZ = Mathf.FloorToInt(_inner.size.z / slotZ + Tolerance);

        _capacity = Mathf.Max(0, countX) * Mathf.Max(0, countY) * Mathf.Max(0, countZ);
        if (_index >= _capacity)
        {
            return false;
        }

        int indexZ = _index % countZ;
        int indexX = (_index / countZ) % countX;
        int indexY = _index / (countZ * countX);

        _center = new Vector3(
            _inner.min.x + slotX * (indexX + 0.5f),
            _inner.min.y + slotY * (indexY + 0.5f),
            _inner.max.z - slotZ * (indexZ + 0.5f));
        return true;
    }
}
