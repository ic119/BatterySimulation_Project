using UnityEngine;

/// <summary>
/// Pack / Module / Cell 오브젝트의 "몸체" 규칙과 만들 때 쓰는 크기·자리 계산.
/// 몸체는 Pack / Module에서는 자식 "Body"의 Renderer, Cell에서는 본인의 Renderer다(자식 Module / Cell의 Renderer는 몸체가 아니다).
/// 씬 오브젝트를 만들거나 지우지 않고, 이미 있는 Transform의 크기와 위치만 바꾼다.
/// </summary>
public static class DeviceBody
{
    /// <summary>Pack / Module 프리팹에서 몸체 Renderer를 가진 자식의 이름.</summary>
    public const string BodyChildName = "Body";

    /// <summary>길이 / 위치 비교에서 같다고 보는 오차. 맞닿은 면을 겹침으로 보지 않거나 경계를 넘었는지 볼 때 쓴다.</summary>
    public const float Tolerance = 0.001f;

    /// <summary>
    /// _target의 몸체 Renderer. 자식 Body에 Renderer가 있으면 그것, 없으면 _target 본인의 Renderer. 둘 다 없으면 false.
    /// </summary>
    public static bool TryGetRenderer(Transform _target, out Renderer _renderer)
    {
        Transform body = _target.Find(BodyChildName);
        if (body != null && body.TryGetComponent(out _renderer))
        {
            return true;
        }

        return _target.TryGetComponent(out _renderer);
    }

    /// <summary>
    /// _child를 _parent 아래에 붙이되, 부모의 크기(스케일)가 곱해지지 않게 한다.
    /// 붙이기 전 _child의 스케일(= 프리팹 본래 크기)이 월드 스케일이 되도록 로컬 스케일을 부모 스케일로 나눠 보정한다.
    /// </summary>
    public static void AttachKeepingOwnScale(Transform _child, Transform _parent)
    {
        Vector3 ownScale = _child.localScale;
        _child.SetParent(_parent, false);

        Vector3 parentScale = _parent.lossyScale;
        _child.localScale = new Vector3(
            DivideSafe(ownScale.x, parentScale.x),
            DivideSafe(ownScale.y, parentScale.y),
            DivideSafe(ownScale.z, parentScale.z));
    }

    /// <summary>
    /// _target의 크기를 월드 크기 _size로 바꾼다. 크기의 어느 축이든 0 이하이면(= 지정하지 않음) 아무것도 하지 않는다.
    /// Pack / Module처럼 자식 Body가 크기를 가지면 Body의 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 바꾸고(Scale 패널과 같은 규칙),
    /// Cell처럼 본인이 몸체면 중심을 유지한 채 스케일을 바꾼다. 몸체는 단위 크기 메시(Cube 등)라는 전제다.
    /// </summary>
    public static void Resize(Transform _target, Vector3 _size)
    {
        if (_target == null || _size.x <= 0.0f || _size.y <= 0.0f || _size.z <= 0.0f)
        {
            return;
        }

        Transform body = _target.Find(BodyChildName);
        if (body != null && body.TryGetComponent(out Renderer bodyRenderer))
        {
            Bounds current = bodyRenderer.bounds;
            Vector3 parentScale = _target.lossyScale;
            body.localScale = new Vector3(
                DivideSafe(_size.x, parentScale.x),
                DivideSafe(_size.y, parentScale.y),
                DivideSafe(_size.z, parentScale.z));
            body.position = new Vector3(
                current.min.x + _size.x * 0.5f,
                current.min.y + _size.y * 0.5f,
                current.max.z - _size.z * 0.5f);
            return;
        }

        if (_target.TryGetComponent(out Renderer ownRenderer))
        {
            Bounds current = ownRenderer.bounds;
            Vector3 scale = _target.localScale;
            _target.localScale = new Vector3(
                current.size.x > 0.0f ? scale.x * _size.x / current.size.x : scale.x,
                current.size.y > 0.0f ? scale.y * _size.y / current.size.y : scale.y,
                current.size.z > 0.0f ? scale.z * _size.z / current.size.z : scale.z);
            _target.position += current.center - ownRenderer.bounds.center;
        }
    }

    /// <summary>
    /// _target의 몸체 중심이 월드 좌표 _center에 오도록 _target 전체를 옮긴다.
    /// 몸체 Renderer를 찾지 못하면 아무것도 하지 않는다.
    /// </summary>
    public static void AlignCenter(Transform _target, Vector3 _center)
    {
        if (!TryGetRenderer(_target, out Renderer bodyRenderer))
        {
            return;
        }

        _target.position += _center - bodyRenderer.bounds.center;
    }

    /// <summary>_divisor가 0이면 나눌 수 없으므로 _value를 그대로 돌려준다.</summary>
    public static float DivideSafe(float _value, float _divisor)
    {
        return Mathf.Approximately(_divisor, 0.0f) ? _value : _value / _divisor;
    }
}
