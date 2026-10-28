using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// CinemachineCamera(OrbitalFollow)로 생성된 Pack / Module / Cell을 추적한다.
/// - 추적: 새 오브젝트가 생기면 pivot을 그쪽으로 부드럽게 옮긴다(Latest), 또는 전체가 한 화면에 들어오도록 pivot과 거리를 맞춘다(All).
/// - 줌: 마우스 휠 (UI 위에서는 무시)
/// - 회전: 마우스 우클릭 드래그 (UI 위에서 시작한 드래그는 무시)
/// </summary>
public class OrbitCameraView : MonoBehaviour
{
    public enum TrackMode
    {
        /// <summary>가장 최근에 생성된 오브젝트를 따라간다. 거리(줌)는 사용자가 정한 값을 유지한다.</summary>
        Latest,
        /// <summary>생성된 모든 오브젝트가 화면에 들어오도록 중심과 거리를 자동으로 맞춘다.</summary>
        All,
    }

    #region Variable
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
    [SerializeField] private Transform pivot;

    [Header("Tracking")]
    [SerializeField] private TrackMode trackMode = TrackMode.Latest;
    [SerializeField] private float followSmoothTime = 0.25f;
    [SerializeField, Tooltip("All 모드에서 오브젝트 주변에 남길 여유 배율")] private float framePadding = 1.2f;

    [Header("Zoom")]
    [SerializeField, Tooltip("휠 한 번당 거리 변화 비율")] private float zoomStep = 0.1f;
    [SerializeField] private float zoomSmoothTime = 0.15f;
    [SerializeField] private float minRadius = 3.0f;
    [SerializeField] private float maxRadius = 300.0f;

    [Header("Rotate")]
    [SerializeField, Tooltip("마우스 1픽셀당 회전 각도")] private float rotateSpeed = 0.2f;
    [SerializeField, Tooltip("체크하면 마우스를 위로 끌 때 아래에서 올려다보는 방향(Y축 반전)으로 회전한다. 해제하면 위에서 내려다보는 각도가 커진다.")] private bool invertY = true;
    [SerializeField] private float minPitch = -10.0f;
    [SerializeField] private float maxPitch = 85.0f;

    private Vector3 targetPosition;
    private float targetRadius;
    private Vector3 positionVelocity;
    private float radiusVelocity;

    private Transform latestTarget;
    private Transform framedRoot;
    private bool isRotating;

    // 선택이 없을 때 돌아갈 처음 시점(Awake 때의 pivot 위치 / 거리 / 회전).
    private Vector3 homePosition;
    private float homeRadius;
    private float homeYaw;
    private float homePitch;

    private bool isResettingRotation;
    private float yawVelocity;
    private float pitchVelocity;
    private const float RotationResetTolerance = 0.05f;
    #endregion

    #region Property
    public TrackMode CurrentTrackMode => trackMode;
    public float TargetRadius => targetRadius;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;

        InputAxis pitch = orbitalFollow.VerticalAxis;
        pitch.Range = new Vector2(minPitch, maxPitch);
        pitch.Wrap = false;
        orbitalFollow.VerticalAxis = pitch;

        targetPosition = pivot.position;
        targetRadius = Mathf.Clamp(orbitalFollow.Radius, minRadius, maxRadius);
        orbitalFollow.Radius = targetRadius;

        homePosition = targetPosition;
        homeRadius = targetRadius;
        homeYaw = orbitalFollow.HorizontalAxis.Value;
        homePitch = orbitalFollow.VerticalAxis.Value;
    }

    private void Update()
    {
        HandleInput();
        UpdateRotationReset();

        pivot.position = Vector3.SmoothDamp(pivot.position, targetPosition, ref positionVelocity, followSmoothTime);
        orbitalFollow.Radius = Mathf.SmoothDamp(orbitalFollow.Radius, targetRadius, ref radiusVelocity, zoomSmoothTime);
    }
    #endregion

    #region Method
    /// <summary>_target(Pack/Module/Cell)을 추적 대상으로 삼는다. _root는 생성된 모든 오브젝트의 부모(PackRoot)다.</summary>
    public void Track(Transform _target, Transform _root)
    {
        latestTarget = _target;
        framedRoot = _root;
        Refocus();
    }

    /// <summary>선택이 없을 때 처음 시점(위치 / 거리 / 회전)으로 부드럽게 되돌린다.</summary>
    public void ResetView()
    {
        latestTarget = null;
        framedRoot = null;

        targetPosition = homePosition;
        targetRadius = homeRadius;
        isResettingRotation = true;
    }

    private void UpdateRotationReset()
    {
        if (!isResettingRotation)
        {
            return;
        }

        float yaw = Mathf.SmoothDampAngle(orbitalFollow.HorizontalAxis.Value, homeYaw, ref yawVelocity, followSmoothTime);
        float pitch = Mathf.SmoothDamp(orbitalFollow.VerticalAxis.Value, homePitch, ref pitchVelocity, followSmoothTime);
        orbitalFollow.HorizontalAxis.Value = yaw;
        orbitalFollow.VerticalAxis.Value = pitch;

        if (Mathf.Abs(Mathf.DeltaAngle(yaw, homeYaw)) < RotationResetTolerance && Mathf.Abs(pitch - homePitch) < RotationResetTolerance)
        {
            orbitalFollow.HorizontalAxis.Value = homeYaw;
            orbitalFollow.VerticalAxis.Value = homePitch;
            isResettingRotation = false;
        }
    }

    public void SetTrackMode(TrackMode _mode)
    {
        trackMode = _mode;
        Refocus();
    }

    /// <summary>현재 추적 모드에 맞춰 목표 위치/거리를 다시 계산한다.</summary>
    public void Refocus()
    {
        if (trackMode == TrackMode.Latest)
        {
            if (TryGetBounds(latestTarget, out Bounds latestBounds))
            {
                targetPosition = latestBounds.center;
            }

            return;
        }

        if (TryGetBounds(framedRoot, out Bounds allBounds))
        {
            targetPosition = allBounds.center;
            targetRadius = Mathf.Clamp(CalculateFitRadius(allBounds), minRadius, maxRadius);
        }
    }

    /// <summary>_bounds 전체가 화면에 들어오기 위한 pivot ~ 카메라 거리. 가로/세로 시야각 중 좁은 쪽을 기준으로 한다.</summary>
    public float CalculateFitRadius(Bounds _bounds)
    {
        Camera cam = Camera.main;
        float aspect = cam != null ? cam.aspect : 16.0f / 9.0f;
        float verticalHalf = cinemachineCamera.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontalHalf = Mathf.Atan(Mathf.Tan(verticalHalf) * aspect);
        float narrowHalf = Mathf.Min(verticalHalf, horizontalHalf);

        // 바운딩 구가 시야 원뿔에 들어오는 거리. 구 기준이라 어느 각도에서 봐도 잘리지 않는다.
        float sphereRadius = _bounds.extents.magnitude;
        return sphereRadius / Mathf.Sin(narrowHalf) * framePadding;
    }

    private void HandleInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        float scroll = mouse.scroll.ReadValue().y;
        if (!overUi)
        {
            Zoom(scroll);
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            isRotating = !overUi;
        }
        else if (mouse.rightButton.wasReleasedThisFrame)
        {
            isRotating = false;
        }

        if (isRotating && mouse.rightButton.isPressed)
        {
            Rotate(mouse.delta.ReadValue());
        }
    }

    /// <summary>휠 입력으로 줌한다. 양수(휠 위로) = 확대(거리 감소). 비율로 변화시켜 가까울 때는 미세하게, 멀 때는 크게 움직인다.</summary>
    public void Zoom(float _scroll)
    {
        if (Mathf.Approximately(_scroll, 0.0f))
        {
            return;
        }

        targetRadius = Mathf.Clamp(targetRadius * (1.0f - Mathf.Sign(_scroll) * zoomStep), minRadius, maxRadius);
    }

    /// <summary>마우스 이동량(픽셀)만큼 회전한다. 오른쪽으로 끌면 장면이 오른쪽으로 돌아가고, 위로 끌면 위에서 내려다보는 각도가 커진다.</summary>
    public void Rotate(Vector2 _delta)
    {
        // 사용자가 직접 돌리기 시작하면 진행 중이던 회전 복귀는 멈춘다.
        isResettingRotation = false;

        orbitalFollow.HorizontalAxis.Value += _delta.x * rotateSpeed;
        float pitchDelta = invertY ? -_delta.y : _delta.y;
        orbitalFollow.VerticalAxis.Value = Mathf.Clamp(orbitalFollow.VerticalAxis.Value + pitchDelta * rotateSpeed, minPitch, maxPitch);
    }

    /// <summary>
    /// _target 아래에서 "보이는"(켜져 있는) Renderer를 감싸는 월드 Bounds. 숨겨진 오브젝트는 화면에 없으므로 제외한다.
    /// 보이는 Renderer가 없으면 위치 한 점으로 대신한다.
    /// </summary>
    private static bool TryGetBounds(Transform _target, out Bounds _bounds)
    {
        _bounds = default;
        if (_target == null)
        {
            return false;
        }

        if (!RendererBounds.TryGetBounds(_target, out _bounds, false, _renderer => _renderer.enabled))
        {
            _bounds = new Bounds(_target.position, Vector3.one);
        }

        return true;
    }
    #endregion
}
