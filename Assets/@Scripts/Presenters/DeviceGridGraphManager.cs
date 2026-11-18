using System.Collections.Generic;
using Incheol.Modules;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeviceType = Incheol.Models.Define.DeviceType;

/// <summary>
/// 바닥과 두 벽면으로 구성된 독립적인 3차원 좌표 그래프.
/// 그래프 x = 로컬 X, 그래프 y = 로컬 Z(횡방향), 그래프 z = 로컬 Y(높이).
/// 기본은 이 오브젝트의 위치가 원점이며 회전은 반영하되 부모 스케일은 반영하지 않는다.
/// SetTarget을 한 번 부르면 Pack 추적 모드가 되어, 그래프 박스의 중심이 Pack 몸체 중심에 오고
/// 축 길이가 Pack 몸체 크기(x, 깊이z = 그래프 y, 높이y = 그래프 z)를 따라간다. 대상이 없으면 숨긴다.
/// </summary>
public class DeviceGridGraphManager : MonoBehaviour
{
    #region Variable
    [Header("Axis Length (World Units)")]
    [SerializeField, Min(0.001f)] private float _xLength = 1f;
    [SerializeField, Min(0.001f)] private float _yLength = 1f;
    [SerializeField, Min(0.001f)] private float _zLength = 1f;

    [Header("Ticks")]
    [Tooltip("각 축을 나눌 구간 수. 10이면 0부터 끝까지 11개의 눈금을 표시한다.")]
    [SerializeField, Range(1, 100)] private int _xDivisions = 10;
    [SerializeField, Range(1, 100)] private int _yDivisions = 10;
    [SerializeField, Range(1, 100)] private int _zDivisions = 10;
    [Tooltip("켜면 길이와 무관하게 0~1, 끄면 0~각 축 길이로 표시한다.")]
    [SerializeField] private bool _normalizedLabels;

    [Header("Axis Title")]
    [SerializeField] private string _xTitle = "x: 정방향";
    [SerializeField] private string _yTitle = "y: 횡방향";
    [SerializeField] private string _zTitle = "z: 종방향";

    [Header("Style (Longest Axis Ratio)")]
    [SerializeField, Min(0.0001f)] private float _lineWidthRatio = 0.0015f;
    [SerializeField, Min(0.0001f)] private float _tickLengthRatio = 0.018f;
    [SerializeField, Min(0.0001f)] private float _labelSizeRatio = 0.024f;
    [SerializeField] private Color _axisColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color _gridColor = new Color(0.88f, 0.88f, 0.88f, 1f);
    [SerializeField] private Color _labelColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color _surfaceColor = Color.white;
    [SerializeField] private bool _showSurfaces = true;
    [Tooltip("이미지처럼 벽면은 세로 격자만 표시한다. 켜면 가로 격자도 추가한다.")]
    [SerializeField] private bool _showHorizontalWallGrid;

    [Header("References")]
    [SerializeField] private Camera _viewCamera;
    [Tooltip("한글 글리프가 포함된 TMP 폰트를 지정한다.")]
    [SerializeField] private TMPro.TMP_FontAsset _labelFont;
    [Tooltip("색상을 지원하는 Unlit 재질. WebGL 빌드에는 직접 지정하는 것을 권장한다.")]
    [SerializeField] private Material _lineMaterial;
    [Tooltip("불투명 Unlit 재질. 인스턴스를 만들어 Surface Color를 적용한다.")]
    [SerializeField] private Material _surfaceMaterial;

    private Transform _graphRoot;
    private Camera _cachedCamera;
    private MeshFilter _surfaceFilter;
    private MeshRenderer _surfaceRenderer;
    private Mesh _surfaceMesh;
    private Material _fallbackLineMaterial;
    private Material _surfaceInstance;
    private Material _lastSurfaceSource;
    private readonly List<LineRenderer> _linePool = new List<LineRenderer>();
    private readonly List<TMPro.TextMeshPro> _labelPool = new List<TMPro.TextMeshPro>();
    private int _lineUsed;
    private int _labelUsed;
    private float _lineWidth;
    private float _tickLength;
    private float _labelSize;
    private bool _dirty = true;
    private Transform _target;
    private bool _followTarget;

    public float XLength { get { return _xLength; } set { SetAxisLengths(value, _yLength, _zLength); } }
    public float YLength { get { return _yLength; } set { SetAxisLengths(_xLength, value, _zLength); } }
    public float ZLength { get { return _zLength; } set { SetAxisLengths(_xLength, _yLength, value); } }
    #endregion

    #region LifeCycle
    private void Awake()
    {
        _cachedCamera = _viewCamera != null ? _viewCamera : Camera.main;
    }

    private void OnEnable()
    {
        _dirty = true;
        if (_graphRoot != null)
            _graphRoot.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (_graphRoot != null)
            _graphRoot.gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        _xLength = ValidLength(_xLength);
        _yLength = ValidLength(_yLength);
        _zLength = ValidLength(_zLength);
        _xDivisions = Mathf.Clamp(_xDivisions, 1, 100);
        _yDivisions = Mathf.Clamp(_yDivisions, 1, 100);
        _zDivisions = Mathf.Clamp(_zDivisions, 1, 100);
        _lineWidthRatio = ValidLength(_lineWidthRatio);
        _tickLengthRatio = ValidLength(_tickLengthRatio);
        _labelSizeRatio = ValidLength(_labelSizeRatio);
        _dirty = true;
    }

    private void LateUpdate()
    {
        bool hasBounds = true;
        Bounds packBounds = default;
        if (_followTarget)
        {
            hasBounds = TryGetTargetBounds(out packBounds);
            if (hasBounds)
            {
                float x = Mathf.Max(packBounds.size.x, 0.001f);
                float y = Mathf.Max(packBounds.size.z, 0.001f);
                float z = Mathf.Max(packBounds.size.y, 0.001f);
                if (!Mathf.Approximately(x, _xLength) || !Mathf.Approximately(y, _yLength) || !Mathf.Approximately(z, _zLength))
                    SetAxisLengths(x, y, z);
            }
        }

        if (_dirty)
            RefreshGraph();

        if (_graphRoot == null) return;
        if (_followTarget)
        {
            _graphRoot.gameObject.SetActive(isActiveAndEnabled && hasBounds);
            if (!hasBounds) return;
            _graphRoot.SetPositionAndRotation(GetTargetOrigin(packBounds.center), transform.rotation);
        }
        else
        {
            _graphRoot.SetPositionAndRotation(transform.position, transform.rotation);
        }
        Camera cam = _viewCamera != null ? _viewCamera : _cachedCamera;
        if (cam == null)
        {
            _cachedCamera = Camera.main;
            cam = _cachedCamera;
        }
        if (cam == null) return;
        for (int i = 0; i < _labelUsed; i++)
            _labelPool[i].transform.rotation = cam.transform.rotation;
    }

    private void OnDestroy()
    {
        ReleaseObject(_graphRoot != null ? _graphRoot.gameObject : null);
        ReleaseObject(_surfaceMesh);
        ReleaseObject(_surfaceInstance);
        ReleaseObject(_fallbackLineMaterial);
    }
    #endregion

    #region Method
    /// <summary>
    /// 그래프가 따라갈 Pack을 지정한다. 호출하면 Pack 추적 모드가 되며, null이면(또는 대상이 파괴되면) 그래프를 숨긴다.
    /// 그래프 박스 중심이 Pack 몸체(자식 Body) 중심에 오고 축 길이는 몸체 크기를 따라 바뀐다.
    /// </summary>
    public void SetTarget(Transform pack)
    {
        _target = pack;
        _followTarget = true;
        _dirty = true;
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = default;
        if (_target == null)
            return false;
        // 몸체(Body) Renderer가 있으면 그 크기를 쓰고, 프리팹에 몸체가 없으면 자식 Renderer를 모두 감싼 크기로 대신한다.
        if (DeviceSizeCalculator.TryGetSizeRenderer(_target, DeviceType.pack, out Renderer body) && body != null)
        {
            bounds = body.bounds;
            return true;
        }
        return RendererBounds.TryGetBounds(_target, out bounds);
    }

    // 그래프 박스(로컬 x, 높이 z, 깊이 y 순서의 Vector3(x, z, y)) 중심을 worldCenter에 맞추는 원점.
    private Vector3 GetTargetOrigin(Vector3 worldCenter)
    {
        Vector3 halfLocal = new Vector3(_xLength * 0.5f, _zLength * 0.5f, _yLength * 0.5f);
        return worldCenter - transform.rotation * halfLocal;
    }

    /// <summary>軸ごとの長さを設定する。値は有限の正数が必要。</summary>
    public void SetAxisLengths(float xLength, float yLength, float zLength)
    {
        if (!IsValidLength(xLength) || !IsValidLength(yLength) || !IsValidLength(zLength))
            throw new System.ArgumentOutOfRangeException("Axis lengths", "축 길이는 0보다 큰 유한한 값이어야 합니다.");
        _xLength = Mathf.Max(0.001f, xLength);
        _yLength = Mathf.Max(0.001f, yLength);
        _zLength = Mathf.Max(0.001f, zLength);
        _dirty = true;
    }

    /// <summary>현재 설정으로 즉시 다시 그린다. 기존 선과 글자는 풀에서 재사용한다.</summary>
    [ContextMenu("Refresh Graph")]
    public void RefreshGraph()
    {
        OnValidate();
        EnsureRoot();
        _graphRoot.SetPositionAndRotation(transform.position, transform.rotation);
        float longest = Mathf.Max(_xLength, Mathf.Max(_yLength, _zLength));
        _lineWidth = longest * _lineWidthRatio;
        _tickLength = longest * _tickLengthRatio;
        _labelSize = longest * _labelSizeRatio;
        _lineUsed = 0;
        _labelUsed = 0;
        BuildSurfaces();
        BuildGrid();

        // +X,+Z 방향에서 볼 때 바닥 오른쪽 X / 바닥 왼쪽 Y / 왼쪽 수직 Z 모서리.
        BuildAxis(Vector3.forward * _yLength, Vector3.right, _xLength, Vector3.forward, _xDivisions, _xTitle);
        BuildAxis(Vector3.right * _xLength, Vector3.forward, _yLength, Vector3.right, _yDivisions, _yTitle);
        BuildAxis(Vector3.right * _xLength, Vector3.up, _zLength, Vector3.back, _zDivisions, _zTitle);

        for (int i = _lineUsed; i < _linePool.Count; i++)
            _linePool[i].gameObject.SetActive(false);
        for (int i = _labelUsed; i < _labelPool.Count; i++)
            _labelPool[i].gameObject.SetActive(false);
        _graphRoot.gameObject.SetActive(isActiveAndEnabled);
        _dirty = false;
    }

    private void EnsureRoot()
    {
        if (_graphRoot != null) return;
        var root = new GameObject("GridGraph_" + name);
        root.layer = gameObject.layer;
        if (gameObject.scene.IsValid())
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        _graphRoot = root.transform;
    }

    private void BuildGrid()
    {
        for (int i = 0; i <= _xDivisions; i++)
        {
            float x = _xLength * i / _xDivisions;
            CreateLine("Floor X " + i, new Vector3(x, 0f, 0f), new Vector3(x, 0f, _yLength), _gridColor, _lineWidth * 0.65f);
            CreateLine("Wall X " + i, new Vector3(x, 0f, 0f), new Vector3(x, _zLength, 0f), _gridColor, _lineWidth * 0.65f);
        }
        for (int i = 0; i <= _yDivisions; i++)
        {
            float y = _yLength * i / _yDivisions;
            CreateLine("Floor Y " + i, new Vector3(0f, 0f, y), new Vector3(_xLength, 0f, y), _gridColor, _lineWidth * 0.65f);
            CreateLine("Wall Y " + i, new Vector3(0f, 0f, y), new Vector3(0f, _zLength, y), _gridColor, _lineWidth * 0.65f);
        }
        if (!_showHorizontalWallGrid) return;
        for (int i = 1; i <= _zDivisions; i++)
        {
            float z = _zLength * i / _zDivisions;
            CreateLine("Wall Height X " + i, new Vector3(0f, z, 0f), new Vector3(_xLength, z, 0f), _gridColor, _lineWidth * 0.65f);
            CreateLine("Wall Height Y " + i, new Vector3(0f, z, 0f), new Vector3(0f, z, _yLength), _gridColor, _lineWidth * 0.65f);
        }
    }

    private void BuildAxis(Vector3 origin, Vector3 direction, float length, Vector3 outward, int divisions, string title)
    {
        CreateLine("Axis " + title, origin, origin + direction * length, _axisColor, _lineWidth);
        for (int i = 0; i <= divisions; i++)
        {
            float fraction = (float)i / divisions;
            Vector3 point = origin + direction * (length * fraction);
            CreateLine("Tick " + title + " " + i, point, point + outward * _tickLength, _axisColor, _lineWidth);
            float value = _normalizedLabels ? fraction : length * fraction;
            CreateLabel(value.ToString("0.###", CultureInfo.InvariantCulture), point + outward * (_tickLength + _labelSize), false);
        }
        CreateLabel(title, origin + direction * (length * 0.5f) + outward * (_tickLength + _labelSize * 6f), true);
    }

    private void CreateLine(string lineName, Vector3 a, Vector3 b, Color color, float width)
    {
        LineRenderer line;
        if (_lineUsed < _linePool.Count)
            line = _linePool[_lineUsed];
        else
        {
            var go = new GameObject(lineName);
            go.layer = gameObject.layer;
            go.transform.SetParent(_graphRoot, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            _linePool.Add(line);
        }
        _lineUsed++;
        line.gameObject.name = lineName;
        line.gameObject.SetActive(true);
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.sharedMaterial = GetLineMaterial();
    }

    private void CreateLabel(string text, Vector3 position, bool title)
    {
        TMPro.TextMeshPro label;
        if (_labelUsed < _labelPool.Count)
            label = _labelPool[_labelUsed];
        else
        {
            var go = new GameObject("Label");
            go.layer = gameObject.layer;
            go.transform.SetParent(_graphRoot, false);
            label = go.AddComponent<TMPro.TextMeshPro>();
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.fontSize = 1f;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            _labelPool.Add(label);
        }
        _labelUsed++;
        label.gameObject.SetActive(true);
        label.gameObject.name = "Label_" + text;
        label.transform.localPosition = position;
        label.transform.localScale = Vector3.one * (_labelSize * 10f);
        label.rectTransform.sizeDelta = new Vector2(title ? 30f : 8f, 3f);
        label.text = text;
        label.color = _labelColor;
        if (_labelFont != null) label.font = _labelFont;
        Camera cam = _viewCamera != null ? _viewCamera : _cachedCamera;
        if (cam != null) label.transform.rotation = cam.transform.rotation;
    }

    private void BuildSurfaces()
    {
        if (!_showSurfaces)
        {
            if (_surfaceRenderer != null) _surfaceRenderer.gameObject.SetActive(false);
            return;
        }
        if (_surfaceFilter == null)
        {
            var go = new GameObject("Floor And Walls");
            go.layer = gameObject.layer;
            go.transform.SetParent(_graphRoot, false);
            _surfaceFilter = go.AddComponent<MeshFilter>();
            _surfaceRenderer = go.AddComponent<MeshRenderer>();
            _surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _surfaceRenderer.receiveShadows = false;
            _surfaceMesh = new Mesh { name = "GridGraph Surfaces" };
            _surfaceFilter.sharedMesh = _surfaceMesh;
        }
        // 面をグリッドよりわずかに外側へ置き、Z-fightingを防ぐ。
        float offset = _lineWidth * 2f;
        _surfaceMesh.Clear();
        _surfaceMesh.vertices = new[]
        {
            new Vector3(0f, -offset, 0f), new Vector3(_xLength, -offset, 0f),
            new Vector3(_xLength, -offset, _yLength), new Vector3(0f, -offset, _yLength),
            new Vector3(0f, 0f, -offset), new Vector3(_xLength, 0f, -offset),
            new Vector3(_xLength, _zLength, -offset), new Vector3(0f, _zLength, -offset),
            new Vector3(-offset, 0f, 0f), new Vector3(-offset, 0f, _yLength),
            new Vector3(-offset, _zLength, _yLength), new Vector3(-offset, _zLength, 0f)
        };
        // 양면 메시이므로 재질의 Cull 설정과 관계없이 양쪽에서 벽면이 보인다.
        var triangles = new int[36];
        for (int face = 0; face < 3; face++)
        {
            int v = face * 4;
            int t = face * 12;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            triangles[t + 6] = v + 2; triangles[t + 7] = v + 1; triangles[t + 8] = v;
            triangles[t + 9] = v + 3; triangles[t + 10] = v + 2; triangles[t + 11] = v;
        }
        _surfaceMesh.triangles = triangles;
        _surfaceMesh.RecalculateNormals();
        _surfaceMesh.RecalculateBounds();
        if (_surfaceInstance == null || _lastSurfaceSource != _surfaceMaterial)
        {
            ReleaseObject(_surfaceInstance);
            if (_surfaceMaterial != null)
                _surfaceInstance = new Material(_surfaceMaterial);
            else
            {
                _surfaceInstance = new Material(GetLineMaterial()) { name = "GridGraph Surface" };
                // 기본 Sprite 재질은 투명 큐이므로 선보다 먼저 그려 벽이 격자를 덮지 않게 한다.
                _surfaceInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            _lastSurfaceSource = _surfaceMaterial;
        }
        if (_surfaceInstance.HasProperty("_BaseColor")) _surfaceInstance.SetColor("_BaseColor", _surfaceColor);
        if (_surfaceInstance.HasProperty("_Color")) _surfaceInstance.SetColor("_Color", _surfaceColor);
        _surfaceRenderer.sharedMaterial = _surfaceInstance;
        _surfaceRenderer.gameObject.SetActive(true);
    }

    private Material GetLineMaterial()
    {
        if (_lineMaterial != null) return _lineMaterial;
        if (_fallbackLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new System.InvalidOperationException("그래프 Line Material을 지정해야 합니다.");
            _fallbackLineMaterial = new Material(shader) { name = "GridGraph Fallback" };
        }
        return _fallbackLineMaterial;
    }

    private static bool IsValidLength(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float ValidLength(float value)
    {
        return IsValidLength(value) ? Mathf.Max(0.0001f, value) : 0.001f;
    }

    private static void ReleaseObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
    #endregion
}
