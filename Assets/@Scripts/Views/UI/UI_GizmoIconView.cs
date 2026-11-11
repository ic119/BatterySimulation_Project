using UnityEngine;

namespace Incheol.Presenter
{
    [AddComponentMenu("UI/Gizmo Icon View")]
    [DefaultExecutionOrder(10000)]
    public class UI_GizmoIconView : UnityEngine.UI.MaskableGraphic
    {
        [Header("Gizmo")]
        [SerializeField] private Camera viewCamera;
        [SerializeField, Tooltip("켜면 카메라 회전에 맞춰 축이 함께 회전한다. 끄면 아래 Fixed Euler 각도로 고정된다.")] private bool rotateWithCamera = false;
        [SerializeField, Tooltip("Rotate With Camera가 꺼졌을 때 쓰는 고정 시점(카메라 오일러 각도). X / Y / Z 축이 모두 보이도록 살짝 기울여 둔다.")] private Vector3 fixedEuler = new Vector3(20.0f, -45.0f, 0.0f);
        [SerializeField] private TMPro.TMP_Text[] axisLabels = new TMPro.TMP_Text[3];
        [SerializeField] private Color xColor = new Color(0.94f, 0.28f, 0.30f);
        [SerializeField] private Color yColor = new Color(0.25f, 0.55f, 1.0f);
        [SerializeField] private Color zColor = new Color(0.35f, 0.84f, 0.38f);
        [SerializeField, Range(0.1f, 1.0f)] private float axisLengthRatio = 0.32f;
        [SerializeField] private float lineWidth = 3.0f;
        [SerializeField] private float endpointRadius = 10.0f;
        [SerializeField] private float backgroundOpacity = 0.65f;

        private readonly Vector3[] directions = new Vector3[3];
        private readonly int[] drawOrder = { 0, 1, 2 };
        private Quaternion lastRotation;
        private bool hasRotation;

        /// <summary>true면 카메라와 함께 회전, false면 고정된 정방향(Fixed Euler)으로 축을 표시한다.</summary>
        public bool RotateWithCamera
        {
            get => rotateWithCamera;
            set
            {
                if (rotateWithCamera == value) return;
                rotateWithCamera = value;
                hasRotation = false;
                Refresh();
            }
        }

        public void ToggleRotateWithCamera()
        {
            RotateWithCamera = !rotateWithCamera;
        }

        // 현재 모드에 따라 축 계산에 쓸 시점 회전. 카메라 모드인데 카메라가 없으면 null 대신 identity.
        private Quaternion GetViewRotation()
        {
            if (!rotateWithCamera) return Quaternion.Euler(fixedEuler);
            return viewCamera != null ? viewCamera.transform.rotation : Quaternion.identity;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            hasRotation = false;
        }
#endif

        public void Init(Camera _camera)
        {
            viewCamera = _camera;
            hasRotation = false;
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            hasRotation = false;
        }

        protected override void Start()
        {
            base.Start();
            MatchMainCamera();
        }

        // MainCamera 태그 카메라(Cinemachine Brain이 붙은 카메라)와 viewCamera를 맞춘다.
        private bool MatchMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return false;
            if (viewCamera == mainCamera) return true;
            viewCamera = mainCamera;
            Refresh();
            return true;
        }


        private void LateUpdate()
        {
            // 씬 로드 순서 때문에 Start 시점에 카메라가 없었거나 파괴된 경우 다시 찾는다.
            if (viewCamera == null) MatchMainCamera();

            bool canRefresh = !rotateWithCamera || viewCamera != null;
            if (canRefresh && (!hasRotation || Quaternion.Angle(lastRotation, GetViewRotation()) > 0.001f))
            {
                Refresh();
            }

            Vector2 center = rectTransform.rect.center;
            float length = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * axisLengthRatio;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axisLabels != null && axis < axisLabels.Length && axisLabels[axis] != null)
                {
                    Vector2 projected = new Vector2(directions[axis].x, directions[axis].y) * length;
                    Vector2 outward = projected.sqrMagnitude > 0.001f ? projected.normalized : Vector2.up;
                    axisLabels[axis].rectTransform.anchoredPosition = projected + outward * endpointRadius * 1.2f;
}
            }
        }

        private void Refresh(bool _markDirty = true)
        {
            lastRotation = GetViewRotation();
            hasRotation = true;
            Quaternion inverse = Quaternion.Inverse(lastRotation);
            directions[0] = inverse * Vector3.right;
            directions[1] = inverse * Vector3.forward;
            directions[2] = inverse * Vector3.up;
            if (_markDirty) SetVerticesDirty();
        }

        private Color AxisColor(int _axis)
        {
            return _axis == 0 ? xColor : _axis == 1 ? yColor : zColor;
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper _mesh)
        {
            _mesh.Clear();
            if (!hasRotation)
            {
                Refresh(false);
            }

            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float size = Mathf.Min(rect.width, rect.height);
            float length = size * axisLengthRatio;
            AddDisc(_mesh, center, size * 0.49f, new Color(0.07f, 0.09f, 0.12f, backgroundOpacity));

            // 카메라에서 먼 축부터 그려 겹치는 경우 가까운 축을 앞에 표시한다.
            for (int i = 0; i < 3; i++) drawOrder[i] = i;
            for (int i = 0; i < 2; i++)
                for (int j = i + 1; j < 3; j++)
                    if (directions[drawOrder[i]].z < directions[drawOrder[j]].z)
                    {
                        int temporary = drawOrder[i];
                        drawOrder[i] = drawOrder[j];
                        drawOrder[j] = temporary;
                    }

            for (int i = 0; i < 3; i++)
            {
                int axis = drawOrder[i];
                Vector2 endpoint = center + new Vector2(directions[axis].x, directions[axis].y) * length;
                Color axisColor = AxisColor(axis);
                AddArrow(_mesh, center, endpoint, axisColor);
            }
        }

        private void AddArrow(UnityEngine.UI.VertexHelper _mesh, Vector2 _start, Vector2 _tip, Color _color)
        {
            Vector2 projected = _tip - _start;
            float projectedLength = projected.magnitude;
            if (projectedLength < 0.001f) return;

            Vector2 direction = projected / projectedLength;
            float headLength = Mathf.Min(endpointRadius * 1.6f, projectedLength * 0.45f);
            float halfWidth = headLength * 0.45f;
            Vector2 baseCenter = _tip - direction * headLength;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * halfWidth;
            AddLine(_mesh, _start, baseCenter, _color);
            int first = _mesh.currentVertCount;
            _mesh.AddVert(_tip, _color, Vector2.zero);
            _mesh.AddVert(baseCenter + perpendicular, _color, Vector2.zero);
            _mesh.AddVert(baseCenter - perpendicular, _color, Vector2.zero);
            _mesh.AddTriangle(first, first + 1, first + 2);
        }

        private void AddLine(UnityEngine.UI.VertexHelper _mesh, Vector2 _start, Vector2 _end, Color _color)
        {
            Vector2 direction = _end - _start;
            if (direction.sqrMagnitude < 0.001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * lineWidth * 0.5f;
            int first = _mesh.currentVertCount;
            _mesh.AddVert(_start - normal, _color, Vector2.zero);
            _mesh.AddVert(_start + normal, _color, Vector2.zero);
            _mesh.AddVert(_end + normal, _color, Vector2.zero);
            _mesh.AddVert(_end - normal, _color, Vector2.zero);
            _mesh.AddTriangle(first, first + 1, first + 2);
            _mesh.AddTriangle(first, first + 2, first + 3);
        }

        private static void AddDisc(UnityEngine.UI.VertexHelper _mesh, Vector2 _center, float _radius, Color _color)
        {
            const int segments = 24;
            int first = _mesh.currentVertCount;
            _mesh.AddVert(_center, _color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2.0f / segments;
                _mesh.AddVert(_center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _radius, _color, Vector2.zero);
                if (i > 0) _mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
