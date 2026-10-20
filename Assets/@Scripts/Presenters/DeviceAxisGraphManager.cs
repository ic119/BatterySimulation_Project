using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// </summary>
public class DeviceAxisGraphManager : MonoBehaviour
{
    #region Variable
    /// <summary>
    /// 그래프를 보여줄 항목(Pack / Module / Cell 루트) 
    /// null일 경우 모든 그래프 비활성화(MainSceneManager 설정 담당)
    /// </summary>
    public static Transform SelectedOwner { get; private set; }

    [Header("Target")]
    [Tooltip("그래프를 두를 몸체. 비워두면 부모의 자식 Body, 없으면 부모 본인의 Renderer(Cell)를 사용")]
    public Transform centerTarget;
    [Tooltip("모서리를 고를 때와 글자 방향의 기준 카메라. 비워두면 Camera.main")]
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
    public string yTitle = "y: Vertical direction";
    public string zTitle = "z: Lateral direction";

    private float _lineWidth;
    private float _tickLength;
    private float _labelSize;

    private float _signX;          // 카메라 쪽(가까운 쪽) 모서리가 대상 중심에서 월드 X 어느 쪽인지(+1 / -1)
    private float _signZ;          // 카메라 쪽 모서리가 월드 Z 어느 쪽인지(+1 / -1)
    private bool _zOnNearXEdge;    // z 축을 (가까운 X, 먼 Z) 세로 모서리에 두는지, 반대편에 두는지
    private Bounds _lastBounds;
    private bool _built;           // 현재 선택 구간에서 모서리를 골라 그래프를 만들었는지

    private Renderer _bodyRenderer;       // 크기 기준이 되는 몸체 Renderer
    private Transform _graphRoot;         // 선 / 글자를 담는 별도 루트(이 오브젝트의 자식이 아님)
    private readonly List<Transform> _billboards = new List<Transform>();       // 카메라를 향하게 할 글자
    private bool _shown;                  // 그래프가 지금 보이는 상태인지
    #endregion

    #region Static
    /// <summary>_owner 항목의 그래프만 보이게 한다. null이면 모두 숨긴다.</summary>
    public static void SetSelectedOwner(Transform _owner)
    {
        SelectedOwner = _owner;
    }
    #endregion

    #region LifeCycle
    private void Start()
    {
        SetupCanvas();
    }

    private void OnDisable()
    {
        SetShown(false);
    }

    private void OnDestroy()
    {
        if (_graphRoot != null)
            Destroy(_graphRoot.gameObject);
    }

    private void LateUpdate()
    {
        // 선택된 항목의 그래프만 보인다. 선택되지 않았으면 만들지도 않는다.
        bool selected = IsSelected();
        if (!selected)
        {
            SetShown(false);
            return;
        }

        // 선택되는 순간 그 시점의 카메라 기준으로 모서리를 새로 고르고 그린다.
        if (!_shown)
            _built = false;

        Refresh();
        SetShown(true);

        // 글자는 항상 카메라를 향하게 한다.
        Camera cam = GetCamera();
        if (cam == null) return;
        for (int i = 0; i < _billboards.Count; i++)
            _billboards[i].rotation = cam.transform.rotation;
    }
    #endregion

    #region Method
    // 이 그래프가 달린 항목(부모 = Pack / Module / Cell 루트)이 선택되어 있는지.
    private bool IsSelected()
    {
        return SelectedOwner != null && transform.parent != null && SelectedOwner == transform.parent;
    }

    private Camera GetCamera()
    {
        return viewCamera != null ? viewCamera : Camera.main;
    }

    private void SetShown(bool shown)
    {
        _shown = shown;
        if (_graphRoot != null)
            _graphRoot.gameObject.SetActive(shown);
    }

    private void SetupCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            canvas.renderMode = RenderMode.WorldSpace;
    }

    // 몸체 Renderer 탐색 (centerTarget > 부모의 자식 Body > 부모 본인(Cell))
    private void ResolveBody()
    {
        if (_bodyRenderer != null) return;

        Transform target = centerTarget;
        if (target == null && transform.parent != null)
        {
            target = transform.parent.Find("Body");
            if (target == null)
                target = transform.parent;
        }

        if (target != null && target.TryGetComponent(out Renderer r))
            _bodyRenderer = r;
    }

    // 선택 직후(_built == false) 또는 몸체 크기·위치가 바뀐 경우에만 그래프 생성
    private void Refresh()
    {
        ResolveBody();
        if (_bodyRenderer == null)
            return;

        Bounds bounds = _bodyRenderer.bounds;

        // 모서리는 선택될 때 카메라 기준으로 한 번만 고르고, 선택된 동안 카메라가 돌아도 바꾸지 않는다.
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

        bool changed = !_built || (bounds.size - _lastBounds.size).sqrMagnitude > 1e-6f || (bounds.center - _lastBounds.center).sqrMagnitude > 1e-6f;
        if (!changed)
            return;

        _lastBounds = bounds;
        _built = true;

        Rebuild(bounds);
    }

    // 그래프 루트를 (없으면) 만든다. 이 오브젝트 계층 밖, 같은 씬의 최상위에 둔다.
    void EnsureRoot()
    {
        if (_graphRoot != null) return;

        string ownerName = transform.parent != null ? transform.parent.name : name;
        var go = new GameObject("AxisGraph_" + ownerName);
        if (gameObject.scene.IsValid())
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        _graphRoot = go.transform;
    }

    void Rebuild(Bounds bounds)
    {
        EnsureRoot();

        // 이전에 만든 선 / 글자 제거
        for (int i = _graphRoot.childCount - 1; i >= 0; i--)
            Destroy(_graphRoot.GetChild(i).gameObject);
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

        // 장치 x 축: Unity X 방향, 가까운 Z 바닥 모서리 위. 카메라 위치와 상관없이 왼쪽(min.x)이 0이고 오른쪽으로 커진다.
        BuildAxis(new Vector3(bounds.min.x, y0, nearZ), Vector3.right, bounds.size.x, xOut, xTitle);
        // 장치 z 축: Unity Z 방향, 가까운 X 바닥 모서리 위. 카메라 위치와 상관없이 max.z가 0이고 -z 방향으로 커진다.
        BuildAxis(new Vector3(nearX, y0, bounds.max.z), Vector3.back, bounds.size.z, yOut, zTitle);

        // 장치 y 축: 세로 모서리 위 (바닥에서 위로)
        Vector3 zCorner = _zOnNearXEdge
            ? new Vector3(nearX, y0, farZ)
            : new Vector3(farX, y0, nearZ);
        Vector3 zOut = new Vector3(zCorner.x - c.x, 0f, zCorner.z - c.z).normalized;
        BuildAxis(zCorner, Vector3.up, bounds.size.y, zOut, yTitle);
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

        // 마지막 눈금이 끝에서 너무 멀면 끝(= 몸체의 실제 길이)에도 눈금을 찍어 길이를 읽을 수 있게 한다.
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
        go.transform.SetParent(_graphRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startWidth = lr.endWidth = _lineWidth;
        lr.material = lineMat != null ? lineMat : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = axisColor;
    }

    // 월드 좌표에 놓는 글자. 크기는 월드 기준(_labelSize)이고 항상 카메라를 향한다. 그래프 루트는 스케일 1이다.
    void CreateLabel(string text, Vector3 worldPos)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(_graphRoot, false);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * (_labelSize * 10f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 1;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = labelColor;
        _billboards.Add(go.transform);
    }
    #endregion
}
