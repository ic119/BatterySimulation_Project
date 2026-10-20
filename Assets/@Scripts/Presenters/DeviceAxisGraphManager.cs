using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// 대상(Body)의 바운딩 박스 모서리를 따라 MATLAB 3D 축 같은 눈금 그래프를 그린다.
/// x / y 축은 카메라에서 가까운 바닥 모서리, z 축은 화면 왼쪽 세로 모서리에 놓이고, 눈금 값은 카메라에서 먼 꼭지점(0)에서 시작한다.
/// 모서리는 처음 그릴 때 카메라 기준으로 한 번만 고르고, 카메라가 돌아도 다시 그리지 않는다.
/// </summary>
public class DeviceAxisGraphManager : MonoBehaviour
{
    #region Variable
    [Header("Target")]
    [Tooltip("그래프를 두를 대상(parentObject). 비워두면 부모(PackPrefab)의 자식 Body를 사용")]
    public Transform centerTarget;
    [Tooltip("처음 모서리를 고를 때와 글자 방향의 기준 카메라. 비워두면 Camera.main")]
    public Camera viewCamera;

    [Header("Style (대상의 가장 긴 변 대비 비율)")]
    public float lineWidthRatio = 0.006f;
    public float tickLengthRatio = 0.03f;
    public float labelSizeRatio = 0.05f;
    [Tooltip("축마다 대략 이 개수의 눈금을 그린다")]
    public int targetTickCount = 5;
    public Color axisColor = Color.black;
    public Color labelColor = Color.black;
    public Material lineMat;

    [Header("Axis Title")]
    public string xTitle = "x: Forward direction";
    public string yTitle = "y: Lateral direction";
    public string zTitle = "z: Vertical direction";

    float _lineWidth;
    float _tickLength;
    float _labelSize;

    float _signX;          // 카메라 쪽(가까운 쪽) 모서리가 대상 중심에서 월드 X 어느 쪽인지(+1 / -1)
    float _signZ;          // 카메라 쪽 모서리가 월드 Z 어느 쪽인지(+1 / -1)
    bool _zOnNearXEdge;    // z 축을 (가까운 X, 먼 Z) 세로 모서리에 두는지, 반대편에 두는지
    Bounds _lastBounds;
    bool _built;

    Renderer _bodyRenderer;                                             // On/Off 기준이 되는 대상의 Renderer
    readonly List<Renderer> _graphRenderers = new List<Renderer>();     // 이 그래프가 만든 선 / 글자 Renderer
    readonly List<Transform> _billboards = new List<Transform>();       // 카메라를 향하게 할 글자
    bool _lastVisible;
    #endregion

    #region LifeCycle
    private void Start()
    {
        SetupCanvas();
        Refresh(true);

        if (centerTarget != null)
            centerTarget.TryGetComponent(out _bodyRenderer);
        ApplyVisible(_bodyRenderer == null || _bodyRenderer.enabled);
    }

    private void LateUpdate()
    {
        // 대상 크기가 바뀐 경우에만 그래프를 다시 만든다.
        Refresh(false);

        // MainSceneManager.SetVisible 등이 대상 Renderer를 켜고 끌 때 그래프도 함께 따라간다.
        if (_bodyRenderer != null && _bodyRenderer.enabled != _lastVisible)
            ApplyVisible(_bodyRenderer.enabled);

        // 글자는 항상 카메라를 향하게 한다.
        Camera cam = GetCamera();
        if (cam == null) return;
        for (int i = 0; i < _billboards.Count; i++)
            _billboards[i].rotation = cam.transform.rotation;
    }
    #endregion

    #region Method
    Camera GetCamera()
    {
        return viewCamera != null ? viewCamera : Camera.main;
    }

    void ApplyVisible(bool visible)
    {
        _lastVisible = visible;
        for (int i = 0; i < _graphRenderers.Count; i++)
            _graphRenderers[i].enabled = visible;
    }

    void SetupCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            canvas.renderMode = RenderMode.WorldSpace;
    }

    // 처음(force) 또는 대상 크기·위치가 바뀐 경우에만 그래프를 만든다.
    void Refresh(bool force)
    {
        if (centerTarget == null && transform.parent != null)
            centerTarget = transform.parent.Find("Body");

        if (!TryGetTargetBounds(out Bounds bounds))
            return;

        // 모서리는 처음 그릴 때 카메라 기준으로 한 번만 고르고, 이후 카메라가 돌아도 바꾸지 않는다.
        if (!_built)
        {
            Camera cam = GetCamera();
            Vector3 camPos = cam != null ? cam.transform.position : bounds.center + Vector3.back * 10f;
            _signX = camPos.x >= bounds.center.x ? 1f : -1f;
            _signZ = camPos.z >= bounds.center.z ? 1f : -1f;

            // z 축은 화면에서 더 왼쪽에 보이는 바닥 꼭지점 위 세로 모서리에 둔다.
            Vector3 cornerA = new Vector3(bounds.center.x + _signX * bounds.extents.x, bounds.min.y, bounds.center.z - _signZ * bounds.extents.z);
            Vector3 cornerB = new Vector3(bounds.center.x - _signX * bounds.extents.x, bounds.min.y, bounds.center.z + _signZ * bounds.extents.z);
            _zOnNearXEdge = cam == null || cam.WorldToViewportPoint(cornerA).x <= cam.WorldToViewportPoint(cornerB).x;
        }

        // 대상 크기 / 위치가 바뀐 경우에만 다시 만든다.
        bool changed = !_built
            || (bounds.size - _lastBounds.size).sqrMagnitude > 1e-6f
            || (bounds.center - _lastBounds.center).sqrMagnitude > 1e-6f;
        if (!force && !changed)
            return;

        _lastBounds = bounds;
        _built = true;

        Rebuild(bounds);
        ApplyVisible(_bodyRenderer == null || _bodyRenderer.enabled);
    }

    bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = default;
        if (centerTarget != null)
        {
            bounds = new Bounds(centerTarget.position, Vector3.zero);
            foreach (var r in centerTarget.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            return true;
        }

        if (transform.parent == null) return false;

        bool found = false;
        foreach (var r in transform.parent.GetComponentsInChildren<Renderer>())
        {
            // 이 그래프 자신이 만든 선/글자는 제외
            if (r.transform.IsChildOf(transform)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    void Rebuild(Bounds bounds)
    {
        // 이전에 만든 선 / 글자 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        _graphRenderers.Clear();
        _billboards.Clear();

        float max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        _lineWidth = max * lineWidthRatio;
        _tickLength = max * tickLengthRatio;
        _labelSize = max * labelSizeRatio;

        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        float y0 = bounds.min.y;

        // 눈금 값은 카메라에서 먼 꼭지점(0)에서 가까운 꼭지점 방향으로 늘어난다.
        float farX = c.x - _signX * e.x;
        float farZ = c.z - _signZ * e.z;
        float nearX = c.x + _signX * e.x;
        float nearZ = c.z + _signZ * e.z;

        Vector3 xOut = Vector3.forward * _signZ; // 가까운 Z 모서리의 바깥쪽
        Vector3 yOut = Vector3.right * _signX;   // 가까운 X 모서리의 바깥쪽

        // 장치 x 축: Unity X 방향, 가까운 Z 바닥 모서리 위
        BuildAxis(new Vector3(farX, y0, nearZ), Vector3.right * _signX, bounds.size.x, xOut, xTitle);
        // 장치 y 축: Unity Z 방향, 가까운 X 바닥 모서리 위
        BuildAxis(new Vector3(nearX, y0, farZ), Vector3.forward * _signZ, bounds.size.z, yOut, yTitle);

        // 장치 z 축: 세로 모서리 위 (바닥에서 위로)
        Vector3 zCorner = _zOnNearXEdge
            ? new Vector3(nearX, y0, farZ)
            : new Vector3(farX, y0, nearZ);
        Vector3 zOut = new Vector3(zCorner.x - c.x, 0f, zCorner.z - c.z).normalized;
        BuildAxis(zCorner, Vector3.up, bounds.size.y, zOut, zTitle);
    }

    // origin에서 dir 방향으로 length만큼 축 선 + 눈금 + 숫자 + 축 이름(모서리 바깥쪽)
    void BuildAxis(Vector3 origin, Vector3 dir, float length, Vector3 outward, string title)
    {
        if (length <= 0f) return;

        CreateLine("Axis " + title, origin, origin + dir * length);

        float step = NiceStep(length / Mathf.Max(1, targetTickCount));
        float lastValue = 0f;
        for (float v = 0f; v <= length + step * 1e-3f; v += step)
        {
            CreateTick(origin, dir, v, outward);
            lastValue = v;
        }

        // 마지막 눈금이 끝에서 너무 멀면 끝(= 대상의 실제 길이)에도 눈금을 찍어 길이를 읽을 수 있게 한다.
        if (length - lastValue > step * 0.3f)
            CreateTick(origin, dir, length, outward);

        Vector3 mid = origin + dir * length * 0.5f;
        CreateLabel(title, mid + outward * (_tickLength + _labelSize * 3f));
    }

    void CreateTick(Vector3 origin, Vector3 dir, float value, Vector3 outward)
    {
        Vector3 p = origin + dir * value;
        CreateLine("Tick " + value.ToString("0.##", CultureInfo.InvariantCulture), p, p + outward * _tickLength);
        CreateLabel(value.ToString("0.##", CultureInfo.InvariantCulture), p + outward * (_tickLength + _labelSize * 0.8f));
    }

    // 1 / 2 / 5 × 10^n 중 가장 가까운 눈금 간격
    static float NiceStep(float raw)
    {
        if (raw <= 0f) return 1f;
        float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
        float f = raw / pow;
        float nice = f < 1.5f ? 1f : f < 3f ? 2f : f < 7f ? 5f : 10f;
        return nice * pow;
    }

    // 월드 좌표 a, b를 잇는 선
    void CreateLine(string name, Vector3 a, Vector3 b)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = lr.endWidth = _lineWidth;
        lr.material = lineMat != null ? lineMat : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = axisColor;
        _graphRenderers.Add(lr);
    }

    // 월드 좌표에 놓는 글자. 크기는 월드 기준(_labelSize)이고 항상 카메라를 향한다.
    void CreateLabel(string text, Vector3 worldPos)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;
        float scale = Mathf.Max(1e-6f, transform.lossyScale.x);
        go.transform.localScale = Vector3.one * (_labelSize * 10f / scale);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 1;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = labelColor;
        _graphRenderers.Add(tmp.GetComponent<Renderer>());
        _billboards.Add(go.transform);
    }
    #endregion
}
