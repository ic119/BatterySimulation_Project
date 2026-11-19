using DeviceType = Incheol.Models.Define.DeviceType;
using StackingAxisType = Incheol.Models.Define.StackingAxisType;
using Incheol.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// Cell 전용 생성·수정 로직.
    /// - StackingXAxis : 기준 Cell의 월드 +x 방향 옆에 Cell을 하나 쌓는다(S 키).
    /// - StackingZAxis : 기준 Cell의 월드 +z 방향 옆에 Cell을 하나 쌓는다(D 키).
    /// 두 함수는 같은 부모(ParallelAssembly) 아래에 같은 크기로 만들고, 방향마다 중심 간격(pitch) 공식만 다르다.
    /// 프리팹 로드, 알람, 이름 번호, 트리 등록은 UI_MainSceneView가 맡고 여기서는 "어디에 어떻게 놓고 만들 것인가"만 다룬다.
    /// </summary>
    public class CellHandler : DeviceNodeHandler
    {
        public override DeviceType Kind => DeviceType.cell;

        /// <summary>같은 방향으로 빈 자리를 찾아 나가는 최대 칸 수.</summary>
        private const int MaxStep = 1000;

        /// <summary>기준 Cell과 그 부모를 살펴 둔 값. 두 방향의 공식이 함께 쓴다.</summary>
        private struct StackContext
        {
            /// <summary>새 Cell의 부모(= 기준 Cell의 부모).</summary>
            public IDeviceNode Parent;

            /// <summary>부모에 몸체(Renderer)가 있어 내부 공간을 알 수 있는지. ParallelAssembly처럼 몸체가 없으면 false.</summary>
            public bool HasInner;

            /// <summary>부모의 내부 공간(HasInner일 때만 의미가 있다).</summary>
            public Bounds Inner;

            /// <summary>기준 Cell의 몸체(Body) 영역.</summary>
            public Bounds Own;

            /// <summary>기준 Cell의 몸체 + PlusTag + MinusTag를 모두 감싼 영역. Tag는 몸체 양 끝(z)에 붙어 있어 몸체보다 길다.</summary>
            public Bounds Full;

            /// <summary>그 축의 Cell 사이 여백. 부모에 사용자가 정한 자식 간격이 있으면 그 값, 없으면 Cell 여백.</summary>
            public float Spacing;
        }

        /// <summary>
        /// JSON stackingAxis에 따라 쌓는다. React의 "Y"는 Unity의 x축(StackingXAxis), "X"는 Unity의 z축(StackingZAxis)으로 처리한다.
        /// </summary>
        public IDeviceNode StackingByAxis(PackFactory _factory, IDeviceNode _reference, GameObject _prefab, string _name, float _cellPadding, StackingAxisType _axis, out string _failMessage)
        {
            return _axis == StackingAxisType.Y
                ? StackingXAxis(_factory, _reference, _prefab, _name, _cellPadding, out _failMessage)
                : StackingZAxis(_factory, _reference, _prefab, _name, _cellPadding, out _failMessage);
        }

        /// <summary>
        /// 기준 Cell의 월드 +x 방향 옆에 Cell을 하나 만든다.
        /// 중심 간격 공식: 몸체가 있는 부모는 Cell x 크기 + 여백, 몸체가 없는 부모는 Body끼리 맞닿도록 Cell x 크기(= Body x 크기)를 그대로 쓴다.
        /// 옆자리가 이미 차 있으면 같은 방향으로 한 칸씩 더 가서 첫 빈 자리에 놓는다.
        /// </summary>
        /// <param name="_cellPadding">Cell 사이 여백 설정값(몸체가 있는 부모에서만 쓴다).</param>
        /// <param name="_failMessage">만들지 못했을 때 사용자에게 보여 줄 안내 문구. 만들었으면 null.</param>
        /// <returns>만들어진 Cell 모델. 만들지 못했으면 null.</returns>
        public IDeviceNode StackingXAxis(PackFactory _factory, IDeviceNode _reference, GameObject _prefab, string _name, float _cellPadding, out string _failMessage)
        {
            if (!TryPrepare(_reference, _cellPadding, 0, out StackContext context, out _failMessage))
            {
                return null;
            }

            float pitch = context.HasInner
                ? context.Own.size.x + context.Spacing
                : context.Own.size.x;

            return Stack(_factory, _reference, _prefab, _name, context, 0, pitch, out _failMessage);
        }

        /// <summary>
        /// 기준 Cell의 월드 +z 방향 옆에 Cell을 하나 만든다.
        /// 중심 간격 공식: 몸체가 있는 부모는 Cell z 크기 + 여백, 몸체가 없는 부모는 한 Cell의 MinusTag와 다음 Cell의 PlusTag가 맞닿도록
        /// Tag까지 포함한 Cell 길이(= Full.size.z)를 중심 간격으로 쓴다.
        /// 옆자리가 이미 차 있으면 같은 방향으로 한 칸씩 더 가서 첫 빈 자리에 놓는다.
        /// </summary>
        /// <param name="_cellPadding">Cell 사이 여백 설정값(몸체가 있는 부모에서만 쓴다).</param>
        /// <param name="_failMessage">만들지 못했을 때 사용자에게 보여 줄 안내 문구. 만들었으면 null.</param>
        /// <returns>만들어진 Cell 모델. 만들지 못했으면 null.</returns>
        public IDeviceNode StackingZAxis(PackFactory _factory, IDeviceNode _reference, GameObject _prefab, string _name, float _cellPadding, out string _failMessage)
        {
            if (!TryPrepare(_reference, _cellPadding, 2, out StackContext context, out _failMessage))
            {
                return null;
            }

            float pitch = context.HasInner
                ? context.Own.size.z + context.Spacing
                : context.Full.size.z;

            return Stack(_factory, _reference, _prefab, _name, context, 2, pitch, out _failMessage);
        }

        /// <summary>
        /// 기준 Cell과 부모가 아직 있는지 확인하고, 두 방향의 공식이 함께 쓰는 값(영역, 여백)을 모은다.
        /// 프리팹을 기다리는 사이 기준 Cell이 지워졌을 수 있으므로 만들기 직전에 다시 확인한다.
        /// </summary>
        private static bool TryPrepare(IDeviceNode _reference, float _cellPadding, int _axis, out StackContext _context, out string _failMessage)
        {
            _context = default;
            _failMessage = null;

            IDeviceNode parent = _reference != null ? _reference.Parent : null;
            if (_reference == null || _reference.Position == null || parent == null || parent.Position == null
                || !DeviceBody.TryGetRenderer(_reference.Position, out Renderer referenceRenderer))
            {
                DebugLogManager.GenerateWarningMessage<CellHandler>("기준 Cell 또는 부모가 없어져 만들지 못했습니다.");
                _failMessage = "기준 Cell이 없어져 만들지 못했습니다.";
                return false;
            }

            // 부모에 몸체(Renderer)가 없으면(예: ParallelAssembly) 내부 공간을 알 수 없다. 이때는 "안에 들어가는지" 검사를 건너뛴다.
            bool hasInner = PackLayout.TryGetInnerBounds(parent.Position, out Bounds inner);

            // 부모에 사용자가 정한 자식 간격이 있으면 그 축의 간격을, 없으면 Cell 여백을 쓴다(기본 칸 배치와 같은 규칙).
            float spacing = _cellPadding;
            if (parent.Position.TryGetComponent(out SubDeviceSpacingData spacingData) && spacingData.TryGet(_axis, out float customSpacing))
            {
                spacing = customSpacing;
            }

            Bounds own = referenceRenderer.bounds;
            Bounds full = RendererBounds.TryGetBounds(_reference.Position, out Bounds all) ? all : own;

            _context = new StackContext
            {
                Parent = parent,
                HasInner = hasInner,
                Inner = inner,
                Own = own,
                Full = full,
                Spacing = spacing,
            };
            return true;
        }

        /// <summary>
        /// 기준 Cell에서 _axis(0 = x, 2 = z)의 + 방향으로 _pitch씩 나아가며 처음 만나는 빈 자리에 Cell을 만든다.
        /// 부모 몸체가 있으면 그 안에서만, 없으면 겹치지만 않으면 된다. 겹침은 Tag까지 포함한 영역으로 검사한다(맞닿는 면은 겹침이 아니다).
        /// </summary>
        private static IDeviceNode Stack(PackFactory _factory, IDeviceNode _reference, GameObject _prefab, string _name, StackContext _context, int _axis, float _pitch, out string _failMessage)
        {
            _failMessage = null;
            string axisName = _axis == 0 ? "+x" : "+z";
            IDeviceNode parent = _context.Parent;

            if (_pitch <= 0.0f)
            {
                DebugLogManager.GenerateErrorMessage<CellHandler>($"Cell 간격을 계산할 수 없습니다 : {axisName} 간격 {_pitch}");
                _failMessage = "Cell 간격을 계산할 수 없어 만들지 못했습니다.";
                return null;
            }

            Vector3 step = _axis == 0 ? Vector3.right : Vector3.forward;
            List<Bounds> occupied = PackLayout.GetOccupiedBounds(parent.Position, null);

            bool found = false;
            Bounds placed = default;
            for (int i = 1; i <= MaxStep; i++)
            {
                Bounds candidate = new Bounds(_context.Own.center + step * (_pitch * i), _context.Own.size);

                if (_context.HasInner && !IsInside(candidate, _context.Inner))
                {
                    // 같은 방향으로 더 가면 더 벗어나므로 여기서 끝낸다.
                    break;
                }

                Bounds footprint = new Bounds(candidate.center + (_context.Full.center - _context.Own.center), _context.Full.size);
                bool overlapped = false;
                for (int o = 0; o < occupied.Count && !overlapped; o++)
                {
                    overlapped = PackLayout.Overlaps(footprint, occupied[o]);
                }

                if (!overlapped)
                {
                    placed = candidate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                DebugLogManager.GenerateWarningMessage<CellHandler>($"{parent.Kind} {parent.Name}에 {axisName} 방향으로 놓을 자리가 없습니다.");
                _failMessage = $"{parent.Kind} {parent.Name}에 {axisName} 방향으로 더 이상 Cell을 추가할 수 없습니다.";
                return null;
            }

            // 크기는 넘기지 않는다. DeviceBody.Resize는 Body가 단위 크기 메시라는 전제라 모델 메시를 쓰는 Cell에는 쓸 수 없다. 대신 아래에서 기준 Cell의 스케일을 복사한다.
            IDeviceNode node = _factory.Create(DeviceType.cell, parent, _name, _prefab, default, placed.center);
            if (node == null)
            {
                _failMessage = string.IsNullOrEmpty(_factory.LastFullMessage) ? "Cell을 만들지 못했습니다." : _factory.LastFullMessage;
                return null;
            }

            // 기준 Cell이 JSON 사양(종류, 쌓는 축, 크기)을 가지고 있으면 새 Cell 모델에도 복사한다.
            if (node is CellDataModel createdModel && _reference is CellDataModel referenceModel)
            {
                createdModel.CopySpecFrom(referenceModel);
            }

            // 기준 Cell과 같은 크기로 맞춘다(사용자가 크기를 바꿨을 수 있다).
            Transform source = _reference.Position;
            Transform created = node.Position;
            created.localScale = source.localScale;
            Transform sourceBody = source.Find(DeviceBody.BodyChildName);
            Transform createdBody = created.Find(DeviceBody.BodyChildName);
            if (sourceBody != null && createdBody != null)
            {
                createdBody.localScale = sourceBody.localScale;
                DeviceBody.AlignCellTags(created);
            }

            // 스케일을 바꾸면 몸체 중심이 달라지므로 놓을 자리로 다시 맞추고, 모델 값(크기, 몸체 중심)을 현재 상태로 갱신한다.
            DeviceBody.AlignCenter(created, placed.center);
            node.Values.Capture(created);

            return node;
        }

        /// <summary>_candidate가 _inner(부모 내부 공간) 안에 완전히 들어가는지.</summary>
        private static bool IsInside(Bounds _candidate, Bounds _inner)
        {
            return _candidate.min.x >= _inner.min.x - PackLayout.Tolerance && _candidate.max.x <= _inner.max.x + PackLayout.Tolerance &&
                   _candidate.min.y >= _inner.min.y - PackLayout.Tolerance && _candidate.max.y <= _inner.max.y + PackLayout.Tolerance &&
                   _candidate.min.z >= _inner.min.z - PackLayout.Tolerance && _candidate.max.z <= _inner.max.z + PackLayout.Tolerance;
        }
    }
}
