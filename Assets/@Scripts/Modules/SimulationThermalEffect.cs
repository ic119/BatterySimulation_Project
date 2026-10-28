using System;
using System.Collections.Generic;
using Incheol.Utils;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// ThermalDetection 연출. PackThermalView가 있는 Pack마다 Module / Cell 중 무작위로 열원을 고르고,
    /// 작업 진행도에 맞춰 열원이 하나씩 나타나 자라게 하며, 끝나면 열원 기준으로 최고 / 최저 온도와 과열 수량을 계산한다.
    /// 몸체 머티리얼을 바꾸고 되돌리는 일은 하지 않는다(SimulationEffectPlayer가 맡는다). 이 클래스는 PackThermalView에 열원만 넘긴다.
    /// </summary>
    public class SimulationThermalEffect
    {
        // 프로토타입용 값. 열원은 Pack 안의 Module / Cell 중 무작위로 고른다.
        private const float FadeDuration = 0.3f;
        private const float MaxHeatRatio = 0.3f;
        private const float HeatRadiusRatio = 0.25f;
        private const float MinHeatStrength = 0.6f;

        // 열원이 생기는 시점은 작업 진행도(0~1)의 앞쪽 HeatStartSpread 안에 고르게 나뉘고,
        // 각 열원은 생긴 뒤 진행도의 HeatGrowDuration만큼 걸려 최대 세기 / 반경까지 자란다. 처음 반경은 최대 반경의 HeatMinRadiusRatio배다.
        private const float HeatStartSpread = 0.5f;
        private const float HeatGrowDuration = 0.35f;
        private const float HeatMinRadiusRatio = 0.3f;

        // 결과에 표시하는 온도(섭씨). 열원 세기(0~1)를 PeakTemperature까지의 온도로 환산하며, 열원이 아닌 곳은 AmbientTemperature다.
        // OverheatTemperature 이상인 열원을 "과하게 오른 부분"으로 센다. 실제 측정값이 아니라 프로토타입용 환산이다.
        private const float AmbientTemperature = 25f;
        private const float PeakTemperature = 80f;
        private const float OverheatTemperature = 70f;

        /// <summary>연출이 끝날 때 도달하는 열원의 최종 모습과 생기는 시점. 진행도에 따라 현재 값을 계산하는 기준이다.</summary>
        private struct HeatSeed
        {
            public Vector3 position;
            public float radius;
            public float strength;
            public float startProgress;
            public float noiseSeed;
        }

        private class Target
        {
            public PackThermalView view;
            public readonly List<Renderer> heatCandidates = new List<Renderer>();
            public readonly List<HeatSeed> seeds = new List<HeatSeed>();
        }

        // 연출을 입힐 Pack(PackThermalView가 있는 것)과 그 Pack 안에서 열원이 될 수 있는 Module / Cell의 몸체 Renderer.
        private readonly List<Target> targets = new List<Target>();
        private readonly List<PackThermalView.HeatSource> heatSources = new List<PackThermalView.HeatSource>();

        /// <summary>_pack의 Module / Cell 몸체 Renderer를 열원 후보로 모아 연출 대상에 추가한다. 씬 오브젝트나 Renderer가 없는 것은 건너뛴다.</summary>
        public void AddTarget(PackThermalView _view, PackDataModel _pack)
        {
            Target target = new Target { view = _view };

            foreach (ModuleDataModel module in DeviceHierarchy.GetModules(_pack))
            {
                AddHeatCandidate(target, module.modulePosition);

                foreach (CellDataModel cell in DeviceHierarchy.GetCells(module))
                {
                    AddHeatCandidate(target, cell.cellPosition);
                }
            }

            targets.Add(target);
        }

        /// <summary>열화상을 끄고 열원을 비운 뒤 대상도 비운다.</summary>
        public void Clear()
        {
            Reset();
            targets.Clear();
        }

        /// <summary>
        /// 연출을 시작한다. 몸체를 가진 대상마다 _prepareBody(몸체 Renderer)를 불러 머티리얼을 바꾸게 하고,
        /// 열원을 정해 짧게 열화상으로 전환한다. 열원이 실제로 나타나고 자라는 것은 UpdateProgress가 맡는다.
        /// </summary>
        public void Begin(float _duration, Action<Renderer> _prepareBody)
        {
            float fadeDuration = Mathf.Min(FadeDuration, _duration * 0.2f);

            foreach (Target target in targets)
            {
                if (target.view == null || target.view.BodyRenderer == null)
                {
                    continue;
                }

                _prepareBody?.Invoke(target.view.BodyRenderer);
                BuildHeatSeeds(target);
                ApplyHeat(target, 0f);
                target.view.SetThermalMode(true, fadeDuration);
            }
        }

        /// <summary>연출의 진행도(_normalized, 0~1)에 맞춰 열원이 하나씩 나타나고 자라게 한다.</summary>
        public void UpdateProgress(float _normalized)
        {
            foreach (Target target in targets)
            {
                if (target.view != null && target.seeds.Count > 0)
                {
                    ApplyHeat(target, _normalized);
                }
            }
        }

        /// <summary>모든 PackThermalView의 열화상을 즉시 끄고 열원을 비운다. 머티리얼은 되돌리지 않는다.</summary>
        public void Reset()
        {
            foreach (Target target in targets)
            {
                if (target.view == null)
                {
                    continue;
                }

                target.view.SetThermalAmount(0f);
                target.view.ClearHeatSources();
            }
        }

        /// <summary>
        /// 최고 / 최저 온도와 과열 수량을 _result에 채운다. 모든 Pack의 열원(seeds)이 대상이며, 열원이 아닌 Module / Cell은 주변 온도로 본다.
        /// 열원이 하나도 없으면 최고 / 최저 모두 주변 온도다. Reset() 전에 불러야 한다(열원 기준이 남아 있어야 한다).
        /// </summary>
        public void FillResult(SimulationStepResult _result)
        {
            int candidateCount = 0;
            int seedCount = 0;
            float maxTemperature = AmbientTemperature;
            float minSeedTemperature = float.MaxValue;

            foreach (Target target in targets)
            {
                candidateCount += target.heatCandidates.Count;

                foreach (HeatSeed seed in target.seeds)
                {
                    float temperature = Mathf.Lerp(AmbientTemperature, PeakTemperature, seed.strength);
                    seedCount++;
                    maxTemperature = Mathf.Max(maxTemperature, temperature);
                    minSeedTemperature = Mathf.Min(minSeedTemperature, temperature);

                    if (temperature >= OverheatTemperature)
                    {
                        _result.overheatCount++;
                    }
                }
            }

            _result.maxTemperature = maxTemperature;
            _result.minTemperature = seedCount == 0 || seedCount < candidateCount ? AmbientTemperature : minSeedTemperature;
        }

        private static void AddHeatCandidate(Target _target, Transform _transform)
        {
            if (_transform != null && DeviceBody.TryGetRenderer(_transform, out Renderer bodyRenderer))
            {
                _target.heatCandidates.Add(bodyRenderer);
            }
        }

        /// <summary>
        /// _progress(0~1)에서의 열원 상태를 계산해 _target의 PackThermalView에 넘긴다.
        /// 열원은 startProgress 이후로 HeatGrowDuration에 걸쳐 세기와 반경이 함께 자라며, 아직 생기지 않은 열원은 넘기지 않는다.
        /// </summary>
        private void ApplyHeat(Target _target, float _progress)
        {
            heatSources.Clear();

            foreach (HeatSeed seed in _target.seeds)
            {
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_progress - seed.startProgress) / HeatGrowDuration));
                if (grow <= 0f)
                {
                    continue;
                }

                heatSources.Add(new PackThermalView.HeatSource(
                    seed.position,
                    seed.radius * Mathf.Lerp(HeatMinRadiusRatio, 1f, grow),
                    seed.strength * grow,
                    seed.noiseSeed));
            }

            _target.view.SetHeatSources(heatSources);
        }

        /// <summary>
        /// _target의 열원 후보에서 무작위 개수(1개 ~ 후보의 MaxHeatRatio, 최대 PackThermalView.MaxHeatSources개)를 무작위로 골라
        /// 몸체 중심을 월드 좌표로 하는 열원 기준(seeds)으로 만든다. 반경은 Pack 몸체 크기에 비례한다.
        /// 뽑힌 순서대로 나타나는 시점(startProgress)이 0 ~ HeatStartSpread 사이에 고르게 나뉜다. 이전 기준은 비운다.
        /// </summary>
        private static void BuildHeatSeeds(Target _target)
        {
            _target.seeds.Clear();

            List<Renderer> candidates = _target.heatCandidates;
            if (candidates.Count == 0)
            {
                return;
            }

            float radius = _target.view.BodyRenderer.bounds.size.magnitude * HeatRadiusRatio;
            int maxCount = Mathf.Clamp(Mathf.FloorToInt(candidates.Count * MaxHeatRatio), 1, PackThermalView.MaxHeatSources);
            int count = UnityEngine.Random.Range(1, maxCount + 1);

            int[] picked = RandomPicker.PickIndices(candidates.Count, count);
            for (int i = 0; i < picked.Length; i++)
            {
                Renderer candidate = candidates[picked[i]];
                if (candidate != null)
                {
                    _target.seeds.Add(new HeatSeed
                    {
                        position = candidate.bounds.center,
                        radius = radius,
                        strength = UnityEngine.Random.Range(MinHeatStrength, 1f),
                        startProgress = HeatStartSpread * i / count,
                        noiseSeed = UnityEngine.Random.Range(0f, 100f),
                    });
                }
            }
        }
    }
}
