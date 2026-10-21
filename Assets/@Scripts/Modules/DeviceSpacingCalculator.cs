using Incheol.Presenter;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// 선택 항목의 한 단계 아래 자식(Pack이면 Module, Module이면 Cell) 사이의 간격을 읽고 바꾸는 계산 모음. UI에는 의존하지 않는다.
    /// 축마다 자식을 겹치는 구간끼리 한 층으로 묶고, 층과 층 사이의 빈 거리를 간격으로 본다.
    /// 첫 층은 제자리에 두고 x, y는 +방향, z는 -방향(좌측 바닥 뒤쪽 모서리 기준)으로 나머지 층을 밀어 낸다.
    /// </summary>
    public static class DeviceSpacingCalculator
    {
        private const float Tolerance = 0.001f;
        private const float UnboundedSize = 2.0e7f;

        private struct Layer
        {
            public float Min;
            public float Max;
            public List<Transform> Members;
        }

        /// <summary>_target의 한 단계 아래 자식 중 간격을 줄 수 있는 것(Body, 축 그래프, Renderer가 없는 것은 제외).</summary>
        public static List<Transform> GetChildren(Transform _target)
        {
            List<Transform> result = new List<Transform>();
            if (_target == null)
            {
                return result;
            }

            for (int i = 0; i < _target.childCount; i++)
            {
                Transform child = _target.GetChild(i);
                if (child.name == "Body" || child.GetComponent<DeviceAxisGraphManager>() != null)
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    result.Add(child);
                }
            }

            return result;
        }

        /// <summary>
        /// _axis(0 = x, 1 = y, 2 = z) 방향의 현재 평균 간격과, 몸체를 벗어나지 않는 최대 간격을 구한다.
        /// 층이 2개 미만이거나 몸체가 없으면 false.
        /// </summary>
        public static bool TryGetSpacing(Transform _target, MainSceneManager.TargetKind _kind, int _axis, out float _current, out float _max)
        {
            _current = 0.0f;
            _max = 0.0f;
            if (!TryBuildLayers(_target, _kind, _axis, out List<Layer> layerList, out Bounds body))
            {
                return false;
            }

            bool reverse = _axis == 2;
            float total = 0.0f;
            for (int i = 0; i < layerList.Count; i++)
            {
                total += layerList[i].Max - layerList[i].Min;
            }

            Layer first = layerList[0];
            Layer last = layerList[layerList.Count - 1];
            float span = reverse ? first.Max - last.Min : last.Max - first.Min;
            _current = Mathf.Max(0.0f, (span - total) / (layerList.Count - 1));

            float available = reverse ? first.Max - body.min[_axis] : body.max[_axis] - first.Min;
            _max = Mathf.Max(0.0f, (available - total) / (layerList.Count - 1));
            return true;
        }

        /// <summary>
        /// _parent에 기억된 축별 간격이 있으면 자식(새로 생긴 것 포함)에 다시 적용한다.
        /// 몸체 밖으로 나가는 축은 건드리지 않고 그 축 이름을 _failedAxis에 담는다(없으면 -1).
        /// 적용한 축이 하나라도 있으면 true.
        /// </summary>
        public static bool ApplyStored(Transform _parent, out int _failedAxis)
        {
            _failedAxis = -1;
            if (_parent == null || !_parent.TryGetComponent(out SubDeviceSpacingData data))
            {
                return false;
            }

            MainSceneManager.TargetKind kind = GetParentKind(_parent);

            bool applied = false;
            for (int axis = 0; axis < 3; axis++)
            {
                if (!data.TryGet(axis, out float spacing))
                {
                    continue;
                }

                if (TrySetSpacing(_parent, kind, axis, spacing, out _))
                {
                    applied = true;
                }
                else if (_failedAxis < 0)
                {
                    _failedAxis = axis;
                }
            }

            return applied;
        }

        /// <summary>부모는 Cell일 수 없다. 몸체(Body)가 없는 빈 오브젝트면 Container, 있으면 Pack / Module(탐색 방식은 같다)이다.</summary>
        private static MainSceneManager.TargetKind GetParentKind(Transform _parent)
        {
            return DeviceSizeCalculator.TryGetSizeRenderer(_parent, MainSceneManager.TargetKind.Pack, out _)
                ? MainSceneManager.TargetKind.Pack
                : MainSceneManager.TargetKind.Container;
        }

        /// <summary>
        /// _parent에 기억된 축별 간격을 자식의 현재 실제 간격으로 갱신한다. 자식 크기를 바꾼 뒤 부르면 기억된 값이 실제와 어긋나지 않는다
        /// (어긋나면 새 자식이 생길 때 ApplyStored가 예전 간격으로 되돌려 버린다).
        /// 기억된 간격이 없는 축이나 층이 2개 미만이라 간격을 잴 수 없는 축은 그대로 둔다.
        /// </summary>
        public static void SyncStored(Transform _parent)
        {
            if (_parent == null || !_parent.TryGetComponent(out SubDeviceSpacingData data))
            {
                return;
            }

            MainSceneManager.TargetKind kind = GetParentKind(_parent);
            for (int axis = 0; axis < 3; axis++)
            {
                if (data.TryGet(axis, out _) && TryGetSpacing(_parent, kind, axis, out float current, out _))
                {
                    data.Set(axis, current);
                }
            }
        }

        /// <summary>
        /// _axis 방향의 층 사이 간격을 _spacing으로 바꾼다. 몸체 밖으로 나가면 바꾸지 않고 false(_max에 허용되는 최대 간격).
        /// </summary>
        public static bool TrySetSpacing(Transform _target, MainSceneManager.TargetKind _kind, int _axis, float _spacing, out float _max)
        {
            _max = 0.0f;
            if (!TryGetSpacing(_target, _kind, _axis, out _, out _max))
            {
                return true;
            }

            if (_spacing < 0.0f || _spacing > _max + Tolerance)
            {
                return false;
            }

            TryBuildLayers(_target, _kind, _axis, out List<Layer> layerList, out _);
            bool reverse = _axis == 2;
            float cursor = reverse ? layerList[0].Min : layerList[0].Max;
            for (int i = 1; i < layerList.Count; i++)
            {
                Layer layer = layerList[i];
                float size = layer.Max - layer.Min;
                float delta;
                if (reverse)
                {
                    delta = (cursor - _spacing) - layer.Max;
                    cursor = layer.Max + delta - size;
                }
                else
                {
                    delta = (cursor + _spacing) - layer.Min;
                    cursor = layer.Min + delta + size;
                }

                Vector3 offset = Vector3.zero;
                offset[_axis] = delta;
                for (int m = 0; m < layer.Members.Count; m++)
                {
                    layer.Members[m].position += offset;
                }
            }

            return true;
        }

        private static bool TryBuildLayers(Transform _target, MainSceneManager.TargetKind _kind, int _axis, out List<Layer> _layerList, out Bounds _body)
        {
            _layerList = null;
            _body = default;
            Renderer bodyRenderer = null;
            bool unbounded = _kind == MainSceneManager.TargetKind.Container;
            if (_target == null || (!unbounded && !DeviceSizeCalculator.TryGetSizeRenderer(_target, _kind, out bodyRenderer)))
            {
                return false;
            }

            List<Transform> children = GetChildren(_target);
            if (children.Count < 2)
            {
                return false;
            }

            // Container는 몸체가 없는 빈 오브젝트라 자식이 나갈 한계가 없다. 사실상 제한이 없는 큰 범위로 본다.
            _body = unbounded ? new Bounds(Vector3.zero, Vector3.one * UnboundedSize) : bodyRenderer.bounds;
            bool reverse = _axis == 2;
            List<KeyValuePair<Transform, Bounds>> boundsList = new List<KeyValuePair<Transform, Bounds>>();
            for (int i = 0; i < children.Count; i++)
            {
                Renderer[] rendererArray = children[i].GetComponentsInChildren<Renderer>(true);
                Bounds bounds = rendererArray[0].bounds;
                for (int r = 1; r < rendererArray.Length; r++)
                {
                    bounds.Encapsulate(rendererArray[r].bounds);
                }

                boundsList.Add(new KeyValuePair<Transform, Bounds>(children[i], bounds));
            }

            // 기준 모서리에서 가까운 것부터(x, y는 min 오름차순, z는 max 내림차순) 훑으며 겹치는 구간끼리 한 층으로 묶는다.
            boundsList.Sort((a, b) => reverse
                ? b.Value.max[_axis].CompareTo(a.Value.max[_axis])
                : a.Value.min[_axis].CompareTo(b.Value.min[_axis]));

            _layerList = new List<Layer>();
            for (int i = 0; i < boundsList.Count; i++)
            {
                Bounds b = boundsList[i].Value;
                float min = b.min[_axis];
                float max = b.max[_axis];
                int last = _layerList.Count - 1;
                if (last >= 0 && (reverse ? max > _layerList[last].Min + Tolerance : min < _layerList[last].Max - Tolerance))
                {
                    Layer layer = _layerList[last];
                    layer.Min = Mathf.Min(layer.Min, min);
                    layer.Max = Mathf.Max(layer.Max, max);
                    layer.Members.Add(boundsList[i].Key);
                    _layerList[last] = layer;
                }
                else
                {
                    _layerList.Add(new Layer { Min = min, Max = max, Members = new List<Transform> { boundsList[i].Key } });
                }
            }

            return _layerList.Count >= 2;
        }
    }
}
