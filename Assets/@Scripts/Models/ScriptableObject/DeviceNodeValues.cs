using System;
using Incheol.Modules;
using UnityEngine;

/// <summary>
/// 장치 모델(Pack / ModuleAssembly / Module / ParallelAssembly / Cell)이 각자 들고 있는 씬 값.
/// 월드 위치, 몸체 크기와 중심, 자식 사이에 적용한 간격을 담는다. 씬 오브젝트(Transform)는 저장하지 않으므로 이 값이 저장의 기준이다.
/// </summary>
[Serializable]
public class DeviceNodeValues
{
    /// <summary>React와 주고받는 장치 식별자. 씬 상태가 아니라 Capture가 건드리지 않는다. 비어 있으면 React가 모르는(Unity에서 만든) 장치다.</summary>
    public string deviceId;

    public Vector3 position;
    public Vector3 size;
    public Vector3 bodyCenter;

    /// <summary>자식 사이 간격(축 x, y, z 순서). spacingApplied가 true인 축만 유효하다.</summary>
    public float[] spacing = new float[3];
    public bool[] spacingApplied = new bool[3];

    /// <summary>_target의 현재 상태를 읽어 이 값에 덮어쓴다. _target이 null이면 그대로 둔다.</summary>
    public void Capture(Transform _target)
    {
        if (_target == null)
        {
            return;
        }

        position = _target.position;

        Bounds bounds;
        if (DeviceBody.TryGetRenderer(_target, out Renderer bodyRenderer))
        {
            bounds = bodyRenderer.bounds;
        }
        else if (!RendererBounds.TryGetBounds(_target, out bounds))
        {
            bounds = new Bounds(_target.position, Vector3.zero);
        }

        size = bounds.size;
        bodyCenter = bounds.center;

        _target.TryGetComponent(out SubDeviceSpacingData data);
        for (int axis = 0; axis < 3; axis++)
        {
            float value = 0.0f;
            spacingApplied[axis] = data != null && data.TryGet(axis, out value);
            spacing[axis] = spacingApplied[axis] ? value : 0.0f;
        }
    }
}
