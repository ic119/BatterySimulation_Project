using System.Collections.Generic;
using DG.Tweening;
using Incheol.Utils;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// 시뮬레이션이 연출 중일 때 Container 안의 Module / Cell 중 무작위로 고른 것들을 DOTween으로 빨갛게 점멸시킨다.
    /// 고른 대상이 한꺼번에 시작하지 않고, 작업의 duringTime에 걸쳐 하나씩 차례로 이상이 생기듯이 점멸을 시작한다.
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

            // 이 대상의 점멸 진행(0 = 원래 색, 1 = 빨강)과 점멸 트윈. 점멸을 시작하기 전에는 tween이 null이다.
            public float blend;
            public Tween tween;
        }

        private readonly List<Renderer> moduleRenderers = new List<Renderer>();
        private readonly List<Renderer> cellRenderers = new List<Renderer>();
        private readonly List<BlinkTarget> activeTargets = new List<BlinkTarget>();

        // 대상마다 점멸을 시작할 시점을 기다리는 트윈(DelayedCall). Stop()에서 아직 시작하지 않은 것도 취소한다.
        private readonly List<Tween> startTweens = new List<Tween>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        /// <summary>마지막 Begin()에서 점멸 대상으로 뽑힌 Module 개수. Stop() / Clear()를 부르면 0으로 돌아간다.</summary>
        public int AbnormalModuleCount { get; private set; }

        /// <summary>마지막 Begin()에서 점멸 대상으로 뽑힌 Cell 개수. Stop() / Clear()를 부르면 0으로 돌아간다.</summary>
        public int AbnormalCellCount { get; private set; }

        /// <summary>
        /// _container의 Pack마다 modulesList의 Module과 각 Module의 cellsList의 Cell에서 몸체 Renderer를 모아 둔다(이전 대상은 비운다).
        /// 씬 오브젝트가 없거나 Renderer가 없는 Module / Cell은 건너뛴다.
        /// </summary>
        public void Collect(ContainerDataModel _container)
        {
            Clear();

            foreach (PackDataModel pack in DeviceHierarchy.GetPacks(_container))
            {
                foreach (ModuleDataModel module in DeviceHierarchy.GetModules(pack))
                {
                    AddRenderer(moduleRenderers, module.modulePosition);

                    foreach (CellDataModel cell in DeviceHierarchy.GetCells(module))
                    {
                        AddRenderer(cellRenderers, cell.cellPosition);
                    }
                }
            }
        }

        /// <summary>
        /// 모아 둔 Module / Cell에서 각각 무작위 개수(1개 ~ 전체의 MaxBlinkRatio, 최소 1개)를 무작위로 골라 점멸 대상으로 정하고,
        /// _duration(초)에 걸쳐 하나씩 차례로 점멸을 시작한다. 시작 순서는 Module / Cell 구분 없이 무작위이며,
        /// i번째(0부터) 대상은 _duration x i / 대상 수 초 뒤에 시작한다(첫 대상은 바로 시작, 마지막 대상도 _duration 안에는 시작한다).
        /// 이전 점멸은 먼저 멈추고 원복한다. 점멸은 Stop()을 부를 때까지 계속되므로, 작업이 끝날 때 Stop()을 부르면 된다.
        /// </summary>
        public void Begin(float _duration)
        {
            Stop();

            AbnormalModuleCount = AddRandomTargets(moduleRenderers, ModuleBlinkColor, ModuleOutlineBlinkColor);
            AbnormalCellCount = AddRandomTargets(cellRenderers, CellBlinkColor, CellOutlineBlinkColor);

            int count = activeTargets.Count;
            if (count == 0)
            {
                return;
            }

            // Module과 Cell이 번갈아 섞여 이상이 생기도록 시작 순서를 무작위로 섞는다.
            for (int i = count - 1; i > 0; i--)
            {
                int pick = Random.Range(0, i + 1);
                (activeTargets[i], activeTargets[pick]) = (activeTargets[pick], activeTargets[i]);
            }

            for (int i = 0; i < count; i++)
            {
                BlinkTarget target = activeTargets[i];
                float delay = Mathf.Max(0f, _duration) * i / count;

                if (delay <= 0f)
                {
                    StartBlink(target);
                    continue;
                }

                // Runner가 쓰는 시간(Time.deltaTime)과 같은 기준이 되도록 timeScale을 따른다.
                startTweens.Add(DOVirtual.DelayedCall(delay, () => StartBlink(target), false));
            }
        }

        /// <summary>점멸(시작을 기다리는 것 포함)을 모두 멈추고 점멸하던 Module / Cell을 원래 색으로 되돌린다.</summary>
        public void Stop()
        {
            foreach (Tween startTween in startTweens)
            {
                startTween.Kill();
            }

            startTweens.Clear();

            foreach (BlinkTarget target in activeTargets)
            {
                if (target.tween != null)
                {
                    target.tween.Kill();
                    target.tween = null;
                }

                if (target.renderer != null)
                {
                    target.renderer.SetPropertyBlock(null);
                }
            }

            activeTargets.Clear();
            AbnormalModuleCount = 0;
            AbnormalCellCount = 0;
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

        /// <summary>_renderers에서 무작위로 점멸 대상을 골라 activeTargets에 추가하고, 실제로 추가한 개수를 돌려준다.</summary>
        private int AddRandomTargets(List<Renderer> _renderers, Color _baseBlinkColor, Color _outlineBlinkColor)
        {
            if (_renderers.Count == 0)
            {
                return 0;
            }

            int addedCount = 0;

            int maxCount = Mathf.Max(1, Mathf.FloorToInt(_renderers.Count * MaxBlinkRatio));
            int count = Random.Range(1, maxCount + 1);
            int[] picked = RandomPicker.PickIndices(_renderers.Count, count);

            for (int i = 0; i < picked.Length; i++)
            {
                Renderer targetRenderer = _renderers[picked[i]];
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
                    addedCount++;
                }
            }

            return addedCount;
        }

        /// <summary>_target의 점멸을 시작한다. 이 대상만의 트윈이라 시작 시점이 달라도 항상 원래 색에서부터 빨강으로 간다.</summary>
        private void StartBlink(BlinkTarget _target)
        {
            if (_target.renderer == null)
            {
                return;
            }

            _target.blend = 0f;
            ApplyBlend(_target);
            _target.tween = DOTween.To(() => _target.blend, _value =>
                {
                    _target.blend = _value;
                    ApplyBlend(_target);
                }, 1f, BlinkHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>_target의 blend(0 = 원래 색, 1 = 빨강)에 맞춰 Renderer의 색(알파 포함)을 PropertyBlock으로 바꾼다.</summary>
        private void ApplyBlend(BlinkTarget _target)
        {
            if (_target.renderer == null)
            {
                return;
            }

            propertyBlock.Clear();

            if (_target.hasBaseColor)
            {
                propertyBlock.SetColor(BaseColorId, Color.Lerp(_target.baseColor, _target.baseBlinkColor, _target.blend));
            }

            if (_target.hasOutlineColor)
            {
                propertyBlock.SetColor(OutlineColorId, Color.Lerp(_target.outlineColor, _target.outlineBlinkColor, _target.blend));
            }

            _target.renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
