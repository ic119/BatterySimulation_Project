using UnityEngine;

namespace BatterySimulation.Modules
{
    /// <summary>
    /// EMS 배터리 시뮬레이션용 전류 흐름 및 충전 상태 제어 컴포넌트
    /// ElectricChargeFlow 셰이더 프로퍼티를 인스펙터 및 C# 스크립트로 제어합니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class ElectricChargeController : MonoBehaviour
    {
        [Header("Charge State (0 to 1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _chargeProgress = 0.5f;

        [Header("Flow Settings")]
        [Tooltip("+X축 방향: (1, 0, 0), 역방향: (-1, 0, 0)")]
        [SerializeField] private Vector3 _flowDirection = new Vector3(1f, 0f, 0f);
        [SerializeField] private float _flowSpeed = 2.5f;
        [SerializeField] private float _flowTiling = 6.0f;

        [Header("Colors (HDR)")]
        [ColorUsage(true, true)]
        [SerializeField] private Color _flowColor = new Color(0f, 0.85f, 1f, 1f);

        [ColorUsage(true, true)]
        [SerializeField] private Color _chargedColor = new Color(0f, 0.9f, 0.45f, 1f);

        [ColorUsage(false, false)]
        [SerializeField] private Color _unchargedColor = new Color(0.02f, 0.03f, 0.05f, 1f);

        [ColorUsage(true, true)]
        [SerializeField] private Color _edgeSparkColor = new Color(1f, 0.95f, 0.3f, 1f);

        [Header("Optimization")]
        [Tooltip("MaterialPropertyBlock을 사용하여 여러 배터리 객체의 개별 제어 및 드로우콜 최적화를 지원합니다.")]
        [SerializeField] private bool _usePropertyBlock = true;

        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;

        // Shader Property IDs
        private static readonly int ChargeProgressId = Shader.PropertyToID("_ChargeProgress");
        private static readonly int FlowDirectionId = Shader.PropertyToID("_FlowDirection");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int FlowTilingId = Shader.PropertyToID("_FlowTiling");
        private static readonly int FlowColorId = Shader.PropertyToID("_FlowColor");
        private static readonly int ChargedColorId = Shader.PropertyToID("_ChargedColor");
        private static readonly int UnchargedColorId = Shader.PropertyToID("_UnchargedColor");
        private static readonly int ChargeEdgeColorId = Shader.PropertyToID("_ChargeEdgeColor");

        public float ChargeProgress
        {
            get => _chargeProgress;
            set
            {
                _chargeProgress = Mathf.Clamp01(value);
                ApplyProperties();
            }
        }

        public Vector3 FlowDirection
        {
            get => _flowDirection;
            set
            {
                _flowDirection = value;
                ApplyProperties();
            }
        }

        public float FlowSpeed
        {
            get => _flowSpeed;
            set
            {
                _flowSpeed = value;
                ApplyProperties();
            }
        }

        public Color FlowColor
        {
            get => _flowColor;
            set
            {
                _flowColor = value;
                ApplyProperties();
            }
        }

        public Color ChargedColor
        {
            get => _chargedColor;
            set
            {
                _chargedColor = value;
                ApplyProperties();
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }
            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }
            ApplyProperties();
        }

        private void Update()
        {
            // 에디터 변경 감지 및 동기화
            if (!Application.isPlaying)
            {
                ApplyProperties();
            }
        }

        private void OnValidate()
        {
            ApplyProperties();
        }

        /// <summary>
        /// EMS 시스템에서 충전율을 설정합니다. (0.0f ~ 1.0f)
        /// </summary>
        public void SetChargeProgress(float progress)
        {
            _chargeProgress = Mathf.Clamp01(progress);
            ApplyProperties();
        }

        /// <summary>
        /// 전류 흐름 방향을 설정합니다. (예: Vector3.right = +X축)
        /// </summary>
        public void SetFlowDirection(Vector3 direction)
        {
            _flowDirection = direction;
            ApplyProperties();
        }

        /// <summary>
        /// 흐름 속도를 설정합니다. (0이면 정지, 음수면 반대 방향)
        /// </summary>
        public void SetFlowSpeed(float speed)
        {
            _flowSpeed = speed;
            ApplyProperties();
        }

        /// <summary>
        /// 전류/스파크 색상을 변경합니다.
        /// </summary>
        public void SetFlowColor(Color color)
        {
            _flowColor = color;
            ApplyProperties();
        }

        /// <summary>
        /// 충전 완료 영역 색상을 변경합니다.
        /// </summary>
        public void SetChargedColor(Color color)
        {
            _chargedColor = color;
            ApplyProperties();
        }

        /// <summary>
        /// 셰이더 프로퍼티를 렌더러에 적용합니다.
        /// </summary>
        public void ApplyProperties()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
                if (_renderer == null) return;
            }

            Vector3 normDir = _flowDirection.sqrMagnitude > 0.0001f ? _flowDirection.normalized : Vector3.right;

            if (_usePropertyBlock)
            {
                if (_propBlock == null)
                {
                    _propBlock = new MaterialPropertyBlock();
                }

                _renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(ChargeProgressId, _chargeProgress);
                _propBlock.SetVector(FlowDirectionId, new Vector4(normDir.x, normDir.y, normDir.z, 0f));
                _propBlock.SetFloat(FlowSpeedId, _flowSpeed);
                _propBlock.SetFloat(FlowTilingId, _flowTiling);
                _propBlock.SetColor(FlowColorId, _flowColor);
                _propBlock.SetColor(ChargedColorId, _chargedColor);
                _propBlock.SetColor(UnchargedColorId, _unchargedColor);
                _propBlock.SetColor(ChargeEdgeColorId, _edgeSparkColor);
                _renderer.SetPropertyBlock(_propBlock);
            }
            else
            {
                Material mat = _renderer.sharedMaterial;
                if (mat != null)
                {
                    mat.SetFloat(ChargeProgressId, _chargeProgress);
                    mat.SetVector(FlowDirectionId, new Vector4(normDir.x, normDir.y, normDir.z, 0f));
                    mat.SetFloat(FlowSpeedId, _flowSpeed);
                    mat.SetFloat(FlowTilingId, _flowTiling);
                    mat.SetColor(FlowColorId, _flowColor);
                    mat.SetColor(ChargedColorId, _chargedColor);
                    mat.SetColor(UnchargedColorId, _unchargedColor);
                    mat.SetColor(ChargeEdgeColorId, _edgeSparkColor);
                }
            }
        }
    }
}
