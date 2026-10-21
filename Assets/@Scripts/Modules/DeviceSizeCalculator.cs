using Incheol.Presenter;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// Pack / Module / Cell 몸체의 크기를 읽고 바꾸는 계산 모음. 입력 필드나 알람 같은 UI에는 의존하지 않는다.
    /// Pack / Module / Cell 모두 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 크기가 바뀐다고 본다.
    /// </summary>
    public static class DeviceSizeCalculator
    {
        private const float Tolerance = 0.001f;

        /// <summary>크기를 바꿀 Renderer. Cell은 본인, Pack / Module은 자식 Body(자식 Module / Cell의 Renderer는 제외). 없으면 false.</summary>
        public static bool TryGetSizeRenderer(Transform _target, MainSceneManager.TargetKind _kind, out Renderer _renderer)
        {
            _renderer = null;
            if (_kind == MainSceneManager.TargetKind.Cell)
            {
                _renderer = _target.GetComponent<Renderer>();
                return _renderer != null;
            }

            _renderer = _target.GetComponent<Renderer>();
            if (_renderer != null)
            {
                return true;
            }

            for (int i = 0; i < _target.childCount; i++)
            {
                Transform child = _target.GetChild(i);
                if (child.name == "Body" && child.TryGetComponent(out _renderer))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 선택 항목 본인 몸체(Pack / Module은 자식 Body, Cell은 본체)의 월드 크기.
        /// 몸체 Renderer가 없으면 자식 Renderer를 합친 크기로 대신한다.
        /// </summary>
        public static bool TryGetOwnSize(Transform _target, MainSceneManager.TargetKind _kind, out Vector3 _size)
        {
            _size = Vector3.zero;
            if (_target == null)
            {
                return false;
            }

            if (TryGetSizeRenderer(_target, _kind, out Renderer body))
            {
                _size = body.bounds.size;
                return true;
            }

            return TryGetWorldSize(_target, out _size);
        }

        private static bool TryGetWorldSize(Transform _target, out Vector3 _size)
        {
            _size = Vector3.zero;
            if (_target == null)
            {
                return false;
            }

            Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
            if (rendererArray.Length == 0)
            {
                return false;
            }

            Bounds bounds = rendererArray[0].bounds;
            for (int i = 1; i < rendererArray.Length; i++)
            {
                bounds.Encapsulate(rendererArray[i].bounds);
            }

            _size = bounds.size;
            return true;
        }

        /// <summary>
        /// 몸체를 _newSize로 바꿨을 때 자식(Module의 Cell, Pack의 Module과 그 Cell)이 모두 몸체 안에 남는지 검사한다.
        /// 벗어나면 false이고 _minSize에는 자식을 담기 위한 최소 크기가 담긴다.
        /// </summary>
        public static bool ChildrenFit(Transform _target, Renderer _body, Vector3 _newSize, out Vector3 _minSize)
        {
            _minSize = Vector3.zero;
            bool hasChild = false;
            Bounds childBounds = default;
            Renderer[] rendererArray = _target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rendererArray.Length; i++)
            {
                if (rendererArray[i] == _body)
                {
                    continue;
                }

                if (!hasChild)
                {
                    childBounds = rendererArray[i].bounds;
                    hasChild = true;
                }
                else
                {
                    childBounds.Encapsulate(rendererArray[i].bounds);
                }
            }

            if (!hasChild)
            {
                return true;
            }

            Bounds body = _body.bounds;
            _minSize = new Vector3(
                childBounds.max.x - body.min.x,
                childBounds.max.y - body.min.y,
                body.max.z - childBounds.min.z);

            return _newSize.x >= _minSize.x - Tolerance && _newSize.y >= _minSize.y - Tolerance && _newSize.z >= _minSize.z - Tolerance;
        }

        /// <summary>
        /// _child(Cell은 Module 안, Module은 Pack 안)를 _newSize로 바꿨을 때 부모 몸체 안에 남는지 검사한다.
        /// _current는 _child 몸체의 현재 bounds. 부모 몸체를 찾을 수 없으면 검사하지 않고 true.
        /// 벗어나면 false이고 _maxSize에는 그 모서리에서 키울 수 있는 최대 크기가 담긴다.
        /// </summary>
        public static bool FitsInParent(Transform _child, MainSceneManager.TargetKind _kind, Bounds _current, Vector3 _newSize, out Vector3 _maxSize)
        {
            _maxSize = _newSize;
            if (_child.parent == null || !TryGetSizeRenderer(_child.parent, _kind == MainSceneManager.TargetKind.Cell ? MainSceneManager.TargetKind.Module : MainSceneManager.TargetKind.Pack, out Renderer parentBody))
            {
                return true;
            }

            Bounds inner = parentBody.bounds;
            _maxSize = new Vector3(
                inner.max.x - _current.min.x,
                inner.max.y - _current.min.y,
                _current.max.z - inner.min.z);

            return _newSize.x <= _maxSize.x + Tolerance && _newSize.y <= _maxSize.y + Tolerance && _newSize.z <= _maxSize.z + Tolerance;
        }

        /// <summary>
        /// Pack(_pack) 몸체를 _newSize로 바꿨을 때 같은 Container 아래의 다른 항목(Pack, 단독 Module과 그 자식)과 겹치는지 검사한다.
        /// 몸체는 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 바뀐다고 본다(Resize와 같다).
        /// 겹치면 true이고 _overlapped에는 겹친 영역이 담긴다. _current는 바꾸기 전 몸체 bounds.
        /// </summary>
        public static bool OverlapsSiblings(Transform _pack, Bounds _current, Vector3 _newSize, out Bounds _overlapped)
        {
            _overlapped = default;
            if (_pack == null || _pack.parent == null)
            {
                return false;
            }

            Vector3 min = new Vector3(_current.min.x, _current.min.y, _current.max.z - _newSize.z);
            Bounds resized = new Bounds(min + _newSize * 0.5f, _newSize);

            System.Collections.Generic.List<Bounds> occupied = PackLayout.GetOccupiedBounds(_pack.parent, _pack);
            for (int i = 0; i < occupied.Count; i++)
            {
                if (PackLayout.Overlaps(resized, occupied[i]))
                {
                    _overlapped = occupied[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// _sizeRenderer 몸체를 _newSize(월드 크기)로 바꾸고 좌측 바닥 뒤쪽 모서리를 제자리에 둔다. _current는 바꾸기 전 bounds.
        /// _isTarget이 true면(Cell처럼 선택한 오브젝트가 곧 몸체) 현재 스케일을 비율만큼 바꾸고,
        /// false면(Pack / Module의 자식 Body) 단위 크기 메시(Cube)라 목표 월드 크기를 부모 스케일로 나눠 로컬 스케일로 쓴다.
        /// </summary>
        public static void Resize(Renderer _sizeRenderer, bool _isTarget, Bounds _current, Vector3 _newSize)
        {
            Transform bodyTransform = _sizeRenderer.transform;
            if (_isTarget)
            {
                Vector3 anchor = new Vector3(_current.min.x, _current.min.y, _current.max.z);
                Vector3 scale = bodyTransform.localScale;
                bodyTransform.localScale = new Vector3(
                    Scale(scale.x, _current.size.x, _newSize.x),
                    Scale(scale.y, _current.size.y, _newSize.y),
                    Scale(scale.z, _current.size.z, _newSize.z));
                Bounds resized = _sizeRenderer.bounds;
                bodyTransform.position += anchor - new Vector3(resized.min.x, resized.min.y, resized.max.z);
            }
            else
            {
                Vector3 parentScale = bodyTransform.parent.lossyScale;
                bodyTransform.localScale = new Vector3(
                    Divide(_newSize.x, parentScale.x),
                    Divide(_newSize.y, parentScale.y),
                    Divide(_newSize.z, parentScale.z));
                bodyTransform.position = new Vector3(
                    _current.min.x + _newSize.x * 0.5f,
                    _current.min.y + _newSize.y * 0.5f,
                    _current.max.z - _newSize.z * 0.5f);
            }
        }

        private static float Scale(float _scale, float _currentSize, float _newSize)
        {
            return Mathf.Approximately(_currentSize, 0.0f) ? _scale : _scale * _newSize / _currentSize;
        }

        private static float Divide(float _value, float _divisor)
        {
            return Mathf.Approximately(_divisor, 0.0f) ? _value : _value / _divisor;
        }
    }
}
