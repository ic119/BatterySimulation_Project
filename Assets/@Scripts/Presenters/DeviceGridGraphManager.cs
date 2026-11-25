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
/// 축 길이가 Pack 몸체 크기(x, 깊이z = 그래프 y, 높이y = 그래프 z)를 따라간다.
/// 대상이 없거나(데이터가 아직 안 들어옴) 대상이 사라지면 숨기지 않고 기본 상태(Inspector의 축 길이, 이 오브젝트의 위치)로 그린다.
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

    [Header("Screen Constant Size")]
    [Tooltip("켜면 선 굵기와 글자 크기를 카메라 줌과 상관없이 화면에서 항상 같은 픽셀 크기로 유지한다. 끄면 위 Ratio 값(월드 크기)을 그대로 쓴다.")]
    [SerializeField] private bool _screenConstantSize = true;
    [Tooltip("축 선의 화면 굵기(픽셀). 격자 선은 이 값의 0.65배다.")]
    [SerializeField, Min(0.1f)] private float _lineWidthPixels = 2f;
    [Tooltip("눈금 숫자와 제목의 화면 글자 높이(픽셀).")]
    [SerializeField, Min(1f)] private float _labelPixels = 14f;
    [Tooltip("픽셀당 월드 크기가 이 비율 이상 바뀔 때만 글자 크기를 다시 적용한다. 선 굵기는 셰이더가 매 프레임 픽셀 기준으로 계산하므로 이 값과 무관하다.")]
    [SerializeField, Range(0.001f, 0.2f)] private float _scaleUpdateThreshold = 0.01f;
    [Tooltip("벽·바닥 면을 격자보다 바깥에 두는 거리(가장 긴 축 비율). 줌과 무관한 고정값이라 z-fighting이 줌에 따라 달라지지 않는다.")]
    [SerializeField, Min(0.0001f)] private float _surfaceOffsetRatio = 0.003f;

    [Header("References")]
    [SerializeField] private Camera _viewCamera;
    [Tooltip("한글 글리프가 포함된 TMP 폰트를 지정한다.")]
    [SerializeField] private TMPro.TMP_FontAsset _labelFont;
    [Tooltip("Incheol/GridGraphLine 셰이더를 쓰는 재질. 비어 있거나 다른 셰이더면 셰이더를 직접 찾아 쓰므로, WebGL 빌드에는 직접 지정해 셰이더가 빌드에 포함되게 한다.")]
    [SerializeField] private Material _lineMaterial;
    [Tooltip("불투명 Unlit 재질. 인스턴스를 만들어 Surface Color를 적용한다.")]
    [SerializeField] private Material _surfaceMaterial;

    private Transform _graphRoot;
    private Camera _cachedCamera;
    private MeshFilter _surfaceFilter;
    private MeshRenderer _surfaceRenderer;
    private Mesh _surfaceMesh;
    private Material _fallbackLineMaterial;
    private Material _fallbackSurfaceMaterial;
    private Material _surfaceInstance;
    private Material _lastSurfaceSource;
    private readonly List<TMPro.TextMeshPro> _labelPool = new List<TMPro.TextMeshPro>();

    // 모든 선(축, 눈금, 격자)을 하나의 메시로 합쳐 그린다. 선 하나는 정점 4개 / 삼각형 2개이며, 굵기는 셰이더가 화면 픽셀 기준으로 펼친다.
    private const string LineShaderName = "Incheol/GridGraphLine";
    private static readonly int WidthScaleId = Shader.PropertyToID("_WidthScale");
    private MeshFilter _lineFilter;
    private MeshRenderer _lineRenderer;
    private Mesh _lineMesh;
    private MaterialPropertyBlock _lineBlock;
    private readonly List<Vector3> _lineVertices = new List<Vector3>();
    private readonly List<Vector4> _lineOthers = new List<Vector4>();
    private readonly List<Vector2> _lineWidthInfos = new List<Vector2>();
    private readonly List<Color> _lineColors = new List<Color>();
    private readonly List<int> _lineTriangles = new List<int>();
    private float _appliedLineWidthScale = -1f;

    private float _worldPerPixel;
    private float _appliedWorldPerPixel;
    private int _labelUsed;
    // 정점에 기록하는 기본 선 굵기. 화면 고정 모드면 픽셀, 아니면 월드 단위다(셰이더의 _WidthScale이 픽셀로 바꾼다).
    private float _lineWidth;
    private float _tickLength;
    private float _labelSize;
    private bool _dirty = true;
    private Transform _target;
    private bool _followTarget;

    // 추적할 대상이 없을 때 돌아갈 기본 축 길이(Awake 시점의 Inspector 값).
    private float _defaultXLength;
    private float _defaultYLength;
    private float _defaultZLength;

    public float XLength { get { return _xLength; } set { SetAxisLengths(value, _yLength, _zLength); } }
    public float YLength { get { return _yLength; } set { SetAxisLengths(_xLength, value, _zLength); } }
    public float ZLength { get { return _zLength; } set { SetAxisLengths(_xLength, _yLength, value); } }
    #endregion

    #region LifeCycle
    private void Awake()
    {
        _cachedCamera = _viewCamera != null ? _viewCamera : Camera.main;
        _defaultXLength = _xLength;
        _defaultYLength = _yLength;
        _defaultZLength = _zLength;
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
        // 추적 대상이 있으면 그 몸체 크기와 중심을 따라가고, 없으면(아직 데이터가 안 들어왔거나 대상이 사라짐) 기본 상태로 그린다.
        bool tracking = false;
        Bounds packBounds = default;
        if (_followTarget)
        {
            tracking = TryGetTargetBounds(out packBounds);

            float x;
            float y;
            float z;
            if (tracking)
            {
                x = Mathf.Max(packBounds.size.x, 0.001f);
                y = Mathf.Max(packBounds.size.z, 0.001f);
                z = Mathf.Max(packBounds.size.y, 0.001f);
            }
            else
            {
                x = _defaultXLength;
                y = _defaultYLength;
                z = _defaultZLength;
            }

            if (!Mathf.Approximately(x, _xLength) || !Mathf.Approximately(y, _yLength) || !Mathf.Approximately(z, _zLength))
                SetAxisLengths(x, y, z);
        }

        if (_dirty)
            RefreshGraph();

        if (_graphRoot == null) return;
        _graphRoot.gameObject.SetActive(isActiveAndEnabled);
        if (tracking)
            _graphRoot.SetPositionAndRotation(GetTargetOrigin(packBounds.center), transform.rotation);
        else
            _graphRoot.SetPositionAndRotation(transform.position, transform.rotation);
        Camera cam = _viewCamera != null ? _viewCamera : _cachedCamera;
        if (cam == null)
        {
            _cachedCamera = Camera.main;
            cam = _cachedCamera;
        }
        if (cam == null) return;
        for (int i = 0; i < _labelUsed; i++)
            _labelPool[i].transform.rotation = cam.transform.rotation;
        ApplyScreenScale();
    }

    /// <summary>
    /// 카메라 줌(거리)이 바뀌면 글자 크기를 화면 픽셀 기준으로 다시 맞춘다. 글자는 Transform 스케일만 바꾸고,
    /// 픽셀당 월드 크기가 임계값 이상 바뀔 때만 갱신한다.
    /// 선 굵기는 셰이더가 매 프레임 화면 픽셀 기준으로 계산하므로 여기서 다루지 않는다(ApplyLineWidthScale 참고).
    /// </summary>
    private void ApplyScreenScale()
    {
        ApplyLineWidthScale();
        if (!_screenConstantSize) return;
        float worldPerPixel = GetWorldPerPixel();
        if (worldPerPixel <= 0f) return;
        if (_appliedWorldPerPixel > 0f && Mathf.Abs(worldPerPixel - _appliedWorldPerPixel) <= _appliedWorldPerPixel * _scaleUpdateThreshold)
            return;

        _appliedWorldPerPixel = worldPerPixel;
        _worldPerPixel = worldPerPixel;

        Vector3 labelScale = Vector3.one * GetLabelScale();
        for (int i = 0; i < _labelUsed; i++)
            _labelPool[i].transform.localScale = labelScale;
    }

    /// <summary>
    /// 정점의 굵기 값을 셰이더가 픽셀로 바꾸는 배율(_WidthScale)을 갱신한다.
    /// 화면 고정 모드는 정점 값이 이미 픽셀이라 배율 1이고, 월드 굵기 모드는 1 / (픽셀당 월드 크기)다. 메시는 다시 만들지 않는다.
    /// </summary>
    private void ApplyLineWidthScale()
    {
        if (_lineRenderer == null) return;
        float scale = 1f;
        if (!_screenConstantSize)
        {
            float worldPerPixel = GetWorldPerPixel();
            if (worldPerPixel > 0f)
                scale = 1f / worldPerPixel;
        }
        if (Mathf.Approximately(scale, _appliedLineWidthScale)) return;

        _appliedLineWidthScale = scale;
        if (_lineBlock == null) _lineBlock = new MaterialPropertyBlock();
        _lineRenderer.GetPropertyBlock(_lineBlock);
        _lineBlock.SetFloat(WidthScaleId, scale);
        _lineRenderer.SetPropertyBlock(_lineBlock);
    }

    /// <summary>
    /// 화면 1픽셀이 그래프 중심 깊이에서 차지하는 월드 길이. 카메라가 없거나 화면 크기를 알 수 없으면 0(월드 비율 방식으로 대신한다).
    /// 원근 카메라는 그래프 중심까지의 카메라 앞 방향 거리, 직교 카메라는 orthographicSize로 계산한다.
    /// </summary>
    private float GetWorldPerPixel()
    {
        if (_graphRoot == null) return 0f;
        Camera cam = _viewCamera != null ? _viewCamera : _cachedCamera;
        if (cam == null)
        {
            _cachedCamera = Camera.main;
            cam = _cachedCamera;
        }
        if (cam == null || cam.pixelHeight <= 0) return 0f;
        if (cam.orthographic)
            return 2f * cam.orthographicSize / cam.pixelHeight;

        Vector3 center = _graphRoot.position + _graphRoot.rotation * new Vector3(_xLength * 0.5f, _zLength * 0.5f, _yLength * 0.5f);
        float depth = Mathf.Max(cam.nearClipPlane, Vector3.Dot(center - cam.transform.position, cam.transform.forward));
        return 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
    }

    /// <summary>글자 localScale. TMP는 fontSize 1, 스케일 s일 때 글자 높이가 대략 s/10 월드 단위다.</summary>
    private float GetLabelScale()
    {
        return _worldPerPixel > 0f ? _worldPerPixel * _labelPixels * 10f : _labelSize * 10f;
    }

    private void OnDestroy()
    {
        ReleaseObject(_graphRoot != null ? _graphRoot.gameObject : null);
        ReleaseObject(_surfaceMesh);
        ReleaseObject(_lineMesh);
        ReleaseObject(_surfaceInstance);
        ReleaseObject(_fallbackLineMaterial);
        ReleaseObject(_fallbackSurfaceMaterial);
    }
    #endregion

    #region Method
    /// <summary>
    /// 그래프가 따라갈 Pack을 지정한다. 호출하면 Pack 추적 모드가 되며, null이면(또는 대상이 파괴되면) 기본 상태 그래프로 돌아간다.
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
            EncapsulateCellTags(_target, ref bounds);
            return true;
        }
        return RendererBounds.TryGetBounds(_target, out bounds);
    }

    // Cell을 단독으로 만들면 Body만 몸체로 잡혀 PlusTag / MinusTag가 박스 밖으로 나온다. Pack / Module 등은 자식 Renderer를 모두 감싸는 경로라 Tag가 이미 포함된다.
    // Cell만 대상으로 하도록 _target의 직접 자식 PlusTag와 MinusTag가 둘 다 Renderer를 가질 때만 합친다(자식 Cell의 Tag는 보지 않는다).
    private static void EncapsulateCellTags(Transform target, ref Bounds bounds)
    {
        Transform plusTag = target.Find(DeviceBody.PlusTagChildName);
        Transform minusTag = target.Find(DeviceBody.MinusTagChildName);
        if (plusTag == null || minusTag == null
            || !plusTag.TryGetComponent(out Renderer plusRenderer)
            || !minusTag.TryGetComponent(out Renderer minusRenderer))
            return;
        bounds.Encapsulate(plusRenderer.bounds);
        bounds.Encapsulate(minusRenderer.bounds);
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
        // 화면 고정 크기 모드면 지금 카메라 기준 픽셀 굵기로, 아니면(또는 카메라가 없으면) 가장 긴 축 비율로 굵기를 정한다.
        _worldPerPixel = _screenConstantSize ? GetWorldPerPixel() : 0f;
        _appliedWorldPerPixel = _worldPerPixel;
        _lineWidth = _screenConstantSize ? _lineWidthPixels : longest * _lineWidthRatio;
        _tickLength = longest * _tickLengthRatio;
        _labelSize = longest * _labelSizeRatio;
        _labelUsed = 0;
        ClearLines();
        BuildSurfaces();
        BuildGrid();

        // 기본 상태와 Pack을 따라갈 때 모두 같은 배치다. 카메라(+x, -z 쪽에서 원점을 내려다봄) 기준으로 벽은 뒤쪽 두 면(x = 0 평면, 깊이 끝 평면),
        // 축은 앞쪽 두 바닥 모서리(Unity x 방향 = 깊이 0 모서리, Unity z 방향 = x 끝 모서리)와 왼쪽 수직 모서리(Z = 원점)에 둔다.
        // 제목은 참고 이미지처럼 x는 오른쪽 앞 모서리(Unity z 방향), y는 왼쪽 앞 모서리(Unity x 방향)에 표시한다. 선 길이와 눈금 수는 각 모서리의 실제 방향 값을 그대로 쓴다.
        BuildAxis(Vector3.zero, Vector3.right, _xLength, Vector3.back, _xDivisions, _yTitle);
        BuildAxis(Vector3.right * _xLength, Vector3.forward, _yLength, Vector3.right, _yDivisions, _xTitle);
        BuildAxis(Vector3.zero, Vector3.up, _zLength, new Vector3(-1f, 0f, -1f).normalized, _zDivisions, _zTitle);

        UploadLines();
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

    /// <summary>
    /// 격자(기본 상태와 Pack을 따라갈 때 같다). 바닥 격자 + 뒤쪽 두 벽(옆벽 x = 0 평면, 뒤벽 깊이 끝 평면)의 세로선을 만든다(_showHorizontalWallGrid를 켜면 가로선도).
    /// 축 선(BuildAxis)이 이미 그리는 선(X축 = Floor Y 0, Y축 = Floor X 끝, Z축 = Wall Y 0)과 두 벽이 만나는 모서리(Wall X 0 = Wall Y 끝)는 한 번만 그린다.
    /// </summary>
    private void BuildGrid()
    {
        const float width = 0.65f; // 축 선 굵기 대비 격자 선 굵기 배율
        for (int i = 0; i <= _xDivisions; i++)
        {
            float x = _xLength * i / _xDivisions;
            if (i != _xDivisions)
                CreateLine(new Vector3(x, 0f, 0f), new Vector3(x, 0f, _yLength), _gridColor, width);
            if (i != 0)
                CreateLine(new Vector3(x, 0f, _yLength), new Vector3(x, _zLength, _yLength), _gridColor, width);
        }
        for (int i = 1; i <= _yDivisions; i++)
        {
            float y = _yLength * i / _yDivisions;
            CreateLine(new Vector3(0f, 0f, y), new Vector3(_xLength, 0f, y), _gridColor, width);
            CreateLine(new Vector3(0f, 0f, y), new Vector3(0f, _zLength, y), _gridColor, width);
        }
        if (!_showHorizontalWallGrid) return;
        for (int i = 1; i <= _zDivisions; i++)
        {
            float z = _zLength * i / _zDivisions;
            CreateLine(new Vector3(0f, z, _yLength), new Vector3(_xLength, z, _yLength), _gridColor, width);
            CreateLine(new Vector3(0f, z, 0f), new Vector3(0f, z, _yLength), _gridColor, width);
        }
    }

    private void BuildAxis(Vector3 origin, Vector3 direction, float length, Vector3 outward, int divisions, string title)
    {
        CreateLine(origin, origin + direction * length, _axisColor, 1f);
        for (int i = 0; i <= divisions; i++)
        {
            float fraction = (float)i / divisions;
            Vector3 point = origin + direction * (length * fraction);
            CreateLine(point, point + outward * _tickLength, _axisColor, 1f);
            float value = _normalizedLabels ? fraction : length * fraction;
            CreateLabel(value.ToString("0.###", CultureInfo.InvariantCulture), point + outward * (_tickLength + _labelSize), false);
        }
        CreateLabel(title, origin + direction * (length * 0.5f) + outward * (_tickLength + _labelSize * 6f), true);
    }

    private void ClearLines()
    {
        _lineVertices.Clear();
        _lineOthers.Clear();
        _lineWidthInfos.Clear();
        _lineColors.Clear();
        _lineTriangles.Clear();
    }

    /// <param name="widthFactor">현재 선 굵기(_lineWidth)에 곱하는 배율. 축 1, 격자 0.65.</param>
    private void CreateLine(Vector3 a, Vector3 b, Color color, float widthFactor)
    {
        float width = _lineWidth * widthFactor;
        // 메시 정점 색은 Linear 색 공간에서 자동 변환되지 않으므로 직접 바꾼다(LineRenderer와 같은 색으로 보이게).
        Color vertexColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        int v = _lineVertices.Count;
        // 정점 순서: 시작점 +쪽, 시작점 -쪽, 끝점 +쪽, 끝점 -쪽. 끝점 쪽은 방향이 뒤집히므로 dirSign -1로 셰이더에서 보정한다.
        AddLineVertex(a, b, 1f, 1f, width, vertexColor);
        AddLineVertex(a, b, -1f, 1f, width, vertexColor);
        AddLineVertex(b, a, 1f, -1f, width, vertexColor);
        AddLineVertex(b, a, -1f, -1f, width, vertexColor);
        _lineTriangles.Add(v); _lineTriangles.Add(v + 2); _lineTriangles.Add(v + 1);
        _lineTriangles.Add(v + 1); _lineTriangles.Add(v + 2); _lineTriangles.Add(v + 3);
    }

    private void AddLineVertex(Vector3 self, Vector3 other, float side, float dirSign, float width, Color color)
    {
        _lineVertices.Add(self);
        _lineOthers.Add(new Vector4(other.x, other.y, other.z, side));
        _lineWidthInfos.Add(new Vector2(width, dirSign));
        _lineColors.Add(color);
    }

    /// <summary>모아 둔 선 정점으로 단일 선 메시를 갱신한다.</summary>
    private void UploadLines()
    {
        if (_lineRenderer == null)
        {
            var go = new GameObject("Lines");
            go.layer = gameObject.layer;
            go.transform.SetParent(_graphRoot, false);
            _lineFilter = go.AddComponent<MeshFilter>();
            _lineRenderer = go.AddComponent<MeshRenderer>();
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _lineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _lineMesh = new Mesh { name = "GridGraph Lines" };
            _lineFilter.sharedMesh = _lineMesh;
        }
        _lineMesh.Clear();
        _lineMesh.SetVertices(_lineVertices);
        _lineMesh.SetUVs(0, _lineOthers);
        _lineMesh.SetUVs(1, _lineWidthInfos);
        _lineMesh.SetColors(_lineColors);
        _lineMesh.SetTriangles(_lineTriangles, 0, true);
        _lineRenderer.sharedMaterial = GetLineMaterial();
        _lineRenderer.gameObject.SetActive(true);
        _appliedLineWidthScale = -1f;
        ApplyLineWidthScale();
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
        label.transform.localScale = Vector3.one * GetLabelScale();
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
        // 오프셋은 줌과 무관한 고정값(가장 긴 축 비율)이다. 선 굵기에 묶으면 줌 아웃 때 오프셋이 깊이 정밀도보다 작아져 선이 면에 가려진다.
        float offset = Mathf.Max(_xLength, Mathf.Max(_yLength, _zLength)) * _surfaceOffsetRatio;
        _surfaceMesh.Clear();
        // 뒤벽 면은 격자 바깥쪽(깊이 끝 + offset, 카메라 반대편)에 둔다.
        float backSurfaceZ = _yLength + offset;
        _surfaceMesh.vertices = new[]
        {
            new Vector3(0f, -offset, 0f), new Vector3(_xLength, -offset, 0f),
            new Vector3(_xLength, -offset, _yLength), new Vector3(0f, -offset, _yLength),
            new Vector3(0f, 0f, backSurfaceZ), new Vector3(_xLength, 0f, backSurfaceZ),
            new Vector3(_xLength, _zLength, backSurfaceZ), new Vector3(0f, _zLength, backSurfaceZ),
            new Vector3(-offset, 0f, 0f), new Vector3(-offset, 0f, _yLength),
            new Vector3(-offset, _zLength, _yLength), new Vector3(-offset, _zLength, 0f)
        };
        // 양면 메시이므로 재질의 Cull 설정과 관계없이 양쪽에서 벽면이 보인다.
        // 면 0 = 바닥, 면 1 = 뒤벽, 면 2 = 옆벽.
        var triangles = new List<int>(36);
        for (int face = 0; face < 3; face++)
        {
            int v = face * 4;
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
            triangles.Add(v + 2); triangles.Add(v + 1); triangles.Add(v);
            triangles.Add(v + 3); triangles.Add(v + 2); triangles.Add(v);
        }
        _surfaceMesh.SetTriangles(triangles, 0);
        _surfaceMesh.RecalculateNormals();
        _surfaceMesh.RecalculateBounds();
        if (_surfaceInstance == null || _lastSurfaceSource != _surfaceMaterial)
        {
            ReleaseObject(_surfaceInstance);
            if (_surfaceMaterial != null)
                _surfaceInstance = new Material(_surfaceMaterial);
            else
            {
                _surfaceInstance = new Material(GetSurfaceFallbackMaterial()) { name = "GridGraph Surface" };
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

    /// <summary>Incheol/GridGraphLine 셰이더 재질. 지정한 재질이 없거나 다른 셰이더(_WidthScale 없음)면 셰이더를 찾아 대신 만든다.</summary>
    private Material GetLineMaterial()
    {
        if (_lineMaterial != null && _lineMaterial.HasProperty(WidthScaleId)) return _lineMaterial;
        if (_fallbackLineMaterial == null)
        {
            Shader shader = Shader.Find(LineShaderName);
            if (shader == null)
                throw new System.InvalidOperationException("그래프 Line Material(" + LineShaderName + " 셰이더)을 지정해야 합니다.");
            _fallbackLineMaterial = new Material(shader) { name = "GridGraph Line Fallback" };
        }
        return _fallbackLineMaterial;
    }

    private Material GetSurfaceFallbackMaterial()
    {
        if (_fallbackSurfaceMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new System.InvalidOperationException("그래프 Surface Material을 지정해야 합니다.");
            _fallbackSurfaceMaterial = new Material(shader) { name = "GridGraph Surface Fallback" };
        }
        return _fallbackSurfaceMaterial;
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
