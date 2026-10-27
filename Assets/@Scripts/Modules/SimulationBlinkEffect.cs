using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// 시뮬레이션이 연출 중일 때 Container 안의 Module / Cell 중 무작위로 고른 것들을 DOTween으로 빨갛게 점멸시킨다.
    /// 고르는 개수와 대상은 Begin()을 부를 때마다 새로 정하며, Stop()을 부르면 점멸을 멈추고 원래 색으로 되돌린다.
    /// 색은 공유 머티리얼을 건드리지 않고 MaterialPropertyBlock으로만 바꾸므로, Stop()에서 PropertyBlock을 지우면 원복된다.
    /// 프로토타입용 연출이라 추후 삭제될 수 있다. 삭제하려면 이 파일과 SimulationEffectPlayer의 blinkEffect 호출만 지우면 된다(다른 곳에서 쓰지 않는다).
    /// </summary>
    public class SimulationBlinkEffect
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        // 점멸이 가장 빨개졌을 때의 색. 알파는 반투명한 몸체도 선명하게 보이도록 올린 값이다.
        // Cell은 불투명하게 진하게, Module은 안쪽의 Cell이 비쳐 보이도록 조금 연하게(알파를 낮게) 한다.
        private static readonly Color CellBlinkColor = new Color(1f, 0f, 0f, 1f);
        private static readonly Color ModuleBlinkColor = new Color(1f, 0f, 0f, 0.5f);

        // 윤곽선(_OutlineColor)은 HDR 색이라 1보다 큰 값을 줘서 더 강하게 빛나 보이게 한다.
        private static readonly Color CellOutlineBlinkColor = new Color(4f, 0f, 0f, 1f);
        private static readonly Color ModuleOutlineBlinkColor = new Color(2f, 0f, 0f, 1f);

        // 한 번에 점멸시킬 수 있는 Module / Cell 개수의 상한 비율(각각의 전체 개수 대비). 프로토타입용 값이다.
        private const float MaxBlinkRatio = 0.3f;

        // 원래 색 -> 빨강으로 가는 데 걸리는 시간(초). 되돌아오는 시간도 같아 한 번 깜빡이는 데 이 값의 2배가 걸린다.
        private const float BlinkHalfDuration = 0.25f;

        private class BlinkTarget
        {
            public Renderer renderer;
            public Color baseBlinkColor;
            public Color outlineBlinkColor;
            public bool hasBaseColor;
            public Color baseColor;
            public bool hasOutlineColor;
            public Color outlineColor;
        }

        private readonly List<Renderer> moduleRenderers = new List<Renderer>();
        private readonly List<Renderer> cellRenderers = new List<Renderer>();
        private readonly List<BlinkTarget> activeTargets = new List<BlinkTarget>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private Tween blinkTween;
        private float blend;

        /// <summary>
        /// _container의 Pack마다 modulesList의 Module과 각 Module의 cellsList의 Cell에서 몸체 Renderer를 모아 둔다(이전 대상은 비운다).
        /// 씬 오브젝트가 없거나 Renderer가 없는 Module / Cell은 건너뛴다.
        /// </summary>
        public void Collect(ContainerDataModel _container)
        {
            Clear();

            if (_container == null || _container.packsList == null)
            {
                return;
            }

            foreach (PackDataModel pack in _container.packsList)
            {
                if (pack == null || pack.modulesList == null)
                {
                    continue;
                }

                foreach (ModuleDataModel module in pack.modulesList)
                {
                    if (module == null)
                    {
                        continue;
                    }

                    AddRenderer(moduleRenderers, module.modulePosition);

                    if (module.cellsList == null)
                    {
                        continue;
                    }

                    foreach (CellDataModel cell in module.cellsList)
                    {
                        if (cell != null)
                        {
                            AddRenderer(cellRenderers, cell.cellPosition);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 모아 둔 Module / Cell에서 각각 무작위 개수(1개 ~ 전체의 MaxBlinkRatio, 최소 1개)를 무작위로 골라 점멸을 시작한다. 이전 점멸은 먼저 멈추고 원복한다.
        /// 점멸은 Stop()을 부를 때까지 계속되므로, 작업의 duringTime이 지날 때 Stop()을 부르면 duringTime 동안만 점멸한다.
        /// </summary>
        public void Begin()
        {
            Stop();

            AddRandomTargets(moduleRenderers, ModuleBlinkColor, ModuleOutlineBlinkColor);
            AddRandomTargets(cellRenderers, CellBlinkColor, CellOutlineBlinkColor);

            if (activeTargets.Count == 0)
            {
                return;
            }

            blend = 0f;
            ApplyBlend();
            blinkTween = DOTween.To(() => blend, _value =>
                {
                    blend = _value;
                    ApplyBlend();
                }, 1f, BlinkHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>점멸을 멈추고 점멸하던 Module / Cell을 원래 색으로 되돌린다.</summary>
        public void Stop()
        {
            if (blinkTween != null)
            {
                blinkTween.Kill();
                blinkTween = null;
            }

            foreach (BlinkTarget target in activeTargets)
            {
                if (target.renderer != null)
                {
                    target.renderer.SetPropertyBlock(null);
                }
            }

            activeTargets.Clear();
        }

        /// <summary>점멸을 멈추고 원복한 뒤, 모아 둔 대상도 비운다.</summary>
        public void Clear()
        {
            Stop();
            moduleRenderers.Clear();
            cellRenderers.Clear();
        }

        private static void AddRenderer(List<Renderer> _list, Transform _target)
        {
            if (_target != null && DeviceBody.TryGetRenderer(_target, out Renderer bodyRenderer))
            {
                _list.Add(bodyRenderer);
            }
        }

        private void AddRandomTargets(List<Renderer> _renderers, Color _baseBlinkColor, Color _outlineBlinkColor)
        {
            if (_renderers.Count == 0)
            {
                return;
            }

            // 앞에서부터 _count개를 무작위로 뽑는 부분 셔플. 원본 목록은 바꾸지 않도록 인덱스만 섞는다.
            int maxCount = Mathf.Max(1, Mathf.FloorToInt(_renderers.Count * MaxBlinkRatio));
            int count = Random.Range(1, maxCount + 1);
            int[] indices = new int[_renderers.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(i, indices.Length);
                (indices[i], indices[pick]) = (indices[pick], indices[i]);

                Renderer targetRenderer = _renderers[indices[i]];
                if (targetRenderer == null)
                {
                    continue;
                }

                Material material = targetRenderer.sharedMaterial;
                BlinkTarget target = new BlinkTarget
                {
                    renderer = targetRenderer,
                    baseBlinkColor = _baseBlinkColor,
                    outlineBlinkColor = _outlineBlinkColor,
                };
                if (material != null && material.HasProperty(BaseColorId))
                {
                    target.hasBaseColor = true;
                    target.baseColor = material.GetColor(BaseColorId);
                }

                if (material != null && material.HasProperty(OutlineColorId))
                {
                    target.hasOutlineColor = true;
                    target.outlineColor = material.GetColor(OutlineColorId);
                }

                if (target.hasBaseColor || target.hasOutlineColor)
                {
                    activeTargets.Add(target);
                }
            }
        }

        /// <summary>blend(0 = 원래 색, 1 = 빨강)에 맞춰 점멸 중인 Renderer의 색(알파 포함)을 PropertyBlock으로 바꾼다.</summary>
        private void ApplyBlend()
        {
            foreach (BlinkTarget target in activeTargets)
            {
                if (target.renderer == null)
                {
                    continue;
                }

                propertyBlock.Clear();

                if (target.hasBaseColor)
                {
                    propertyBlock.SetColor(BaseColorId, Blend(target.baseColor, target.baseBlinkColor));
                }

                if (target.hasOutlineColor)
                {
                    propertyBlock.SetColor(OutlineColorId, Blend(target.outlineColor, target.outlineBlinkColor));
                }

                target.renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private Color Blend(Color _original, Color _blinkColor)
        {
            return Color.Lerp(_original, _blinkColor, blend);
        }
    }
}
