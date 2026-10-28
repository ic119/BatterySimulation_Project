using System;
using System.Collections.Generic;
using Incheol.Utils;
using UnityEngine;
using SimulationType = Incheol.Models.Define.SimulationType;

namespace Incheol.Modules
{
    /// <summary>
    /// SimulationRunner가 알려주는 작업의 시작 / 진행 / 끝 시점에 맞춰 SimulationType별 연출을 재생하고 정리한다.
    /// 언제 실행할지(시간 관리)는 Runner가, 무엇을 보여줄지는 이 클래스가 맡는다. 현재는 Charge / Discharge / ThermalDetection이 구현되어 있다.
    /// </summary>
    public class SimulationEffectPlayer
    {
        private static readonly int ChargeProgressId = Shader.PropertyToID("_ChargeProgress");

        // ThermalDetection 연출의 프로토타입용 값. 열원은 Container 안의 Module / Cell 중 무작위로 고른다.
        private const float ThermalFadeDuration = 0.3f;
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
        }

        private class ThermalTarget
        {
            public PackThermalView view;
            public readonly List<Renderer> heatCandidates = new List<Renderer>();
            public readonly List<HeatSeed> seeds = new List<HeatSeed>();
        }

        private readonly Material packPrefabOutLine;
        private readonly Material electricChargeFlowMaterial;
        private readonly Material thermalDetectionMaterial;

        private readonly List<Renderer> packBodies = new List<Renderer>();

        // ThermalDetection 연출을 입힐 Pack(PackThermalView가 있는 것)과 그 Pack 안에서 열원이 될 수 있는 Module / Cell의 몸체 Renderer.
        private readonly List<ThermalTarget> thermalTargets = new List<ThermalTarget>();
        private readonly List<PackThermalView.HeatSource> heatSources = new List<PackThermalView.HeatSource>();

        // 연출이 바꾼 Body의 원래 머티리얼. Clear에서 되돌린다.
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        // 공유 머티리얼 에셋은 건드리지 않고 Body마다 값을 줄 수 있도록, 진행도는 머티리얼 대신 PropertyBlock으로 넣는다.
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        // Charge / Discharge 연출 중 Container 안의 Module / Cell을 무작위로 빨갛게 점멸시킨다.
        private readonly SimulationBlinkEffect blinkEffect = new SimulationBlinkEffect();

        // 이번 실행에서 끝난 작업 수. 결과의 실행 순서 번호로 쓴다.
        private int completedStepCount;

        /// <summary>작업 1개가 끝났을 때(연출 정리 직전) 그 작업의 결과와 함께 호출된다.</summary>
        public event Action<SimulationStepResult> StepResultReady;

        /// <summary>연출을 입힐 대상(선택된 Container). SetContainer로 지정한다.</summary>
        public ContainerDataModel Container { get; private set; }

        /// <summary>Container에 속한 Pack들의 몸체(Body) Renderer. SetContainer가 채우고 Clear가 비운다.</summary>
        public IReadOnlyList<Renderer> PackBodies => packBodies;

        public SimulationEffectPlayer(SimulationRunner _runner, Material _packPrefabOutLine, Material _electricChargeFlowMaterial, Material _thermalDetectionMaterial)
        {
            packPrefabOutLine = _packPrefabOutLine;
            electricChargeFlowMaterial = _electricChargeFlowMaterial;
            thermalDetectionMaterial = _thermalDetectionMaterial;

            _runner.StepBegan += OnStepBegan;
            _runner.StepProgressed += OnStepProgressed;
            _runner.StepEnded += OnStepEnded;
            _runner.Finished += Clear;
        }

        /// <summary>
        /// _container의 Pack마다 몸체 Renderer를 찾아 PackBodies에 담는다(이전 대상은 되돌리고 비운다). 실행을 시작하기 전에 호출한다.
        /// Pack의 씬 Transform은 _packFactory가 들고 있다. 씬 오브젝트가 없거나 몸체 Renderer가 없는 Pack은 건너뛴다.
        /// </summary>
        public void SetContainer(ContainerDataModel _container, PackFactory _packFactory)
        {
            Clear();
            Container = _container;
            blinkEffect.Collect(_container);

            if (_container == null || _container.packsList == null || _packFactory == null)
            {
                return;
            }

            foreach (PackDataModel pack in _container.packsList)
            {
                Transform packTransform = _packFactory.GetPackTransform(pack);
                if (packTransform != null && DeviceBody.TryGetRenderer(packTransform, out Renderer bodyRenderer))
                {
                    packBodies.Add(bodyRenderer);

                    if (packTransform.TryGetComponent(out PackThermalView thermalView))
                    {
                        thermalTargets.Add(CreateThermalTarget(thermalView, pack));
                    }
                }
            }
        }

        /// <summary>_pack의 Module / Cell 몸체 Renderer를 열원 후보로 모아 둔다. 씬 오브젝트나 Renderer가 없는 것은 건너뛴다.</summary>
        private static ThermalTarget CreateThermalTarget(PackThermalView _view, PackDataModel _pack)
        {
            ThermalTarget target = new ThermalTarget { view = _view };

            if (_pack.modulesList == null)
            {
                return target;
            }

            foreach (ModuleDataModel module in _pack.modulesList)
            {
                if (module == null)
                {
                    continue;
                }

                AddHeatCandidate(target, module.modulePosition);

                if (module.cellsList == null)
                {
                    continue;
                }

                foreach (CellDataModel cell in module.cellsList)
                {
                    if (cell != null)
                    {
                        AddHeatCandidate(target, cell.cellPosition);
                    }
                }
            }

            return target;
        }

        private static void AddHeatCandidate(ThermalTarget _target, Transform _transform)
        {
            if (_transform != null && DeviceBody.TryGetRenderer(_transform, out Renderer bodyRenderer))
            {
                _target.heatCandidates.Add(bodyRenderer);
            }
        }

        /// <summary>
        /// 연출 중인 Body(electricChargeFlowMaterial)를 packPrefabOutLine으로 바꾸고 대상을 비운다. packPrefabOutLine이 없으면 연출 전 머티리얼로 되돌린다.
        /// 모든 작업이 끝났을 때(Finished), 다른 Container가 선택되었을 때, 다음 실행을 시작할 때(SetContainer),
        /// Runner가 중간에 중단되었을 때(StepEnded가 오지 않으므로) 호출한다.
        /// </summary>
        public void Clear()
        {
            blinkEffect.Clear();
            ResetThermalViews();
            RestoreMaterials();
            completedStepCount = 0;
            Container = null;
            packBodies.Clear();
            thermalTargets.Clear();
        }

        private void OnStepBegan(SimulationStepModel _step)
        {
            switch (_step.simulationType)
            {
                case SimulationType.Charge:
                    EnsureChargeMaterial();
                    SetChargeProgress(0f);
                    blinkEffect.Begin(_step.duringTime);
                    break;
                case SimulationType.Discharge:
                    EnsureChargeMaterial();
                    SetChargeProgress(1f);
                    blinkEffect.Begin(_step.duringTime);
                    break;
                case SimulationType.ThermalDetection:
                    BeginThermalDetection(_step.duringTime);
                    break;
                case SimulationType.None:
                default:
                    break;
            }
        }

        private void OnStepProgressed(SimulationStepModel _step, float _normalized)
        {
            switch (_step.simulationType)
            {
                case SimulationType.Charge:
                    SetChargeProgress(_normalized);
                    break;
                case SimulationType.Discharge:
                    SetChargeProgress(1f - _normalized);
                    break;
                case SimulationType.ThermalDetection:
                    UpdateThermalDetection(_normalized);
                    break;
            }
        }

        private void OnStepEnded(SimulationStepModel _step)
        {
            // 점멸을 멈추면 이상 수량이 0으로 돌아가므로, 결과는 연출을 정리하기 전에 만든다.
            RaiseStepResult(_step);

            switch (_step.simulationType)
            {
                case SimulationType.Charge:
                    SetChargeProgress(1f);
                    blinkEffect.Stop();
                    break;
                case SimulationType.Discharge:
                    SetChargeProgress(0f);
                    blinkEffect.Stop();
                    break;
                case SimulationType.ThermalDetection:
                    EndThermalDetection();
                    break;
                case SimulationType.None:
                default:
                    break;
            }
        }

        /// <summary>
        /// 끝난 작업의 결과(Container의 Pack / Module / Cell 전체 수량 + 연출이 진행된 Module / Cell 수량)를 만들어 StepResultReady로 알린다.
        /// </summary>
        private void RaiseStepResult(SimulationStepModel _step)
        {
            if (Container == null || Container.packsList == null)
            {
                return;
            }

            SimulationStepResult result = new SimulationStepResult
            {
                stepNumber = ++completedStepCount,
                simulationType = _step.simulationType,
                duringTime = _step.duringTime,
                packCount = Container.packsList.Count,
                abnormalModuleCount = blinkEffect.AbnormalModuleCount,
                abnormalCellCount = blinkEffect.AbnormalCellCount,
            };

            foreach (PackDataModel pack in Container.packsList)
            {
                if (pack == null || pack.modulesList == null)
                {
                    continue;
                }

                result.moduleCount += pack.modulesList.Count;
                foreach (ModuleDataModel module in pack.modulesList)
                {
                    if (module != null && module.cellsList != null)
                    {
                        result.cellCount += module.cellsList.Count;
                    }
                }
            }

            if (_step.simulationType == SimulationType.ThermalDetection)
            {
                FillThermalResult(result);
            }

            StepResultReady?.Invoke(result);
        }

        /// <summary>
        /// 열감지 작업의 최고 / 최저 온도와 과열 수량을 _result에 채운다. 모든 Pack의 열원(seeds)이 대상이며, 열원이 아닌 Module / Cell은 주변 온도로 본다.
        /// 열원이 하나도 없으면 최고 / 최저 모두 주변 온도다. 연출을 정리하기 전에 불러야 한다(정리 전까지 열원 기준이 남아 있다).
        /// </summary>
        private void FillThermalResult(SimulationStepResult _result)
        {
            int candidateCount = 0;
            int seedCount = 0;
            float maxTemperature = AmbientTemperature;
            float minSeedTemperature = float.MaxValue;

            foreach (ThermalTarget target in thermalTargets)
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

        /// <summary>
        /// PackBodies의 머티리얼이 electricChargeFlowMaterial인지 확인하고, 아니면 바꾼다(Charge / Discharge 공통).
        /// 이미 전부 electricChargeFlowMaterial인 Body는 건드리지 않으며, 바꾼 Body의 원래 머티리얼만 처음 한 번 기억한다.
        /// </summary>
        private void EnsureChargeMaterial()
        {
            if (electricChargeFlowMaterial == null)
            {
                DebugLogManager.GenerateWarningMessage<SimulationEffectPlayer>("electricChargeFlowMaterial이 지정되지 않아 Charge 연출을 하지 않습니다.");
                return;
            }

            foreach (Renderer bodyRenderer in packBodies)
            {
                ApplyMaterial(bodyRenderer, electricChargeFlowMaterial);
            }
        }

        /// <summary>
        /// _renderer의 머티리얼을 전부 _material로 바꾼다. 이미 전부 _material이면 건드리지 않으며, 바꾸는 경우 원래 머티리얼은 처음 한 번만 기억한다.
        /// </summary>
        private void ApplyMaterial(Renderer _renderer, Material _material)
        {
            if (_renderer == null)
            {
                return;
            }

            Material[] currentMaterials = _renderer.sharedMaterials;
            if (IsAllMaterial(currentMaterials, _material))
            {
                return;
            }

            if (!originalMaterials.ContainsKey(_renderer))
            {
                originalMaterials[_renderer] = currentMaterials;
            }

            Material[] materials = new Material[currentMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = _material;
            }

            _renderer.sharedMaterials = materials;
        }

        private static bool IsAllMaterial(Material[] _materials, Material _material)
        {
            if (_material == null || _materials.Length == 0)
            {
                return false;
            }

            foreach (Material material in _materials)
            {
                if (material != _material)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 열감지 연출을 시작한다. PackThermalView가 있는 Pack의 몸체(bodyRenderer)를 thermalDetectionMaterial로 바꾸고,
        /// 무작위로 고른 Module / Cell을 열원으로 정해 짧게 열화상으로 전환한다. 열원이 실제로 나타나고 자라는 것은 UpdateThermalDetection이 진행도에 맞춰 맡는다.
        /// </summary>
        private void BeginThermalDetection(float _duration)
        {
            if (thermalDetectionMaterial == null)
            {
                DebugLogManager.GenerateWarningMessage<SimulationEffectPlayer>("thermalDetectionMaterial이 지정되지 않아 ThermalDetection 연출을 하지 않습니다.");
                return;
            }

            float fadeDuration = Mathf.Min(ThermalFadeDuration, _duration * 0.2f);

            foreach (ThermalTarget target in thermalTargets)
            {
                if (target.view == null || target.view.BodyRenderer == null)
                {
                    continue;
                }

                ApplyMaterial(target.view.BodyRenderer, thermalDetectionMaterial);
                BuildHeatSeeds(target);
                ApplyHeat(target, 0f);
                target.view.SetThermalMode(true, fadeDuration);
            }
        }

        /// <summary>열감지 연출의 진행도(_normalized, 0~1)에 맞춰 열원이 하나씩 나타나고 자라게 한다.</summary>
        private void UpdateThermalDetection(float _normalized)
        {
            foreach (ThermalTarget target in thermalTargets)
            {
                if (target.view != null && target.seeds.Count > 0)
                {
                    ApplyHeat(target, _normalized);
                }
            }
        }

        /// <summary>
        /// _progress(0~1)에서의 열원 상태를 계산해 _target의 PackThermalView에 넘긴다.
        /// 열원은 startProgress 이후로 HeatGrowDuration에 걸쳐 세기와 반경이 함께 자라며, 아직 생기지 않은 열원은 넘기지 않는다.
        /// </summary>
        private void ApplyHeat(ThermalTarget _target, float _progress)
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
                    seed.strength * grow));
            }

            _target.view.SetHeatSources(heatSources);
        }

        /// <summary>
        /// 열감지 연출을 끝낸다. 열화상을 끄고 열원을 비운 뒤, 연출에 쓴 몸체를 packPrefabOutLine으로 되돌린다.
        /// </summary>
        private void EndThermalDetection()
        {
            ResetThermalViews();
            RestoreMaterials();
        }

        /// <summary>모든 PackThermalView의 열화상을 즉시 끄고 열원을 비운다. 머티리얼은 되돌리지 않는다.</summary>
        private void ResetThermalViews()
        {
            foreach (ThermalTarget target in thermalTargets)
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
        /// _target의 열원 후보에서 무작위 개수(1개 ~ 후보의 MaxHeatRatio, 최대 PackThermalView.MaxHeatSources개)를 무작위로 골라
        /// 몸체 중심을 월드 좌표로 하는 열원 기준(seeds)으로 만든다. 반경은 Pack 몸체 크기에 비례한다.
        /// 뽑힌 순서대로 나타나는 시점(startProgress)이 0 ~ HeatStartSpread 사이에 고르게 나뉜다. 이전 기준은 비운다.
        /// </summary>
        private void BuildHeatSeeds(ThermalTarget _target)
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

            // 앞에서부터 count개를 무작위로 뽑는 부분 셔플. 원본 목록은 바꾸지 않도록 인덱스만 섞는다.
            int[] indices = new int[candidates.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(i, indices.Length);
                (indices[i], indices[pick]) = (indices[pick], indices[i]);

                Renderer candidate = candidates[indices[i]];
                if (candidate != null)
                {
                    _target.seeds.Add(new HeatSeed
                    {
                        position = candidate.bounds.center,
                        radius = radius,
                        strength = UnityEngine.Random.Range(MinHeatStrength, 1f),
                        startProgress = HeatStartSpread * i / count,
                    });
                }
            }
        }

        /// <summary>충전 진행도(0~1)를 PackBodies의 셰이더 프로퍼티 _ChargeProgress에 넣는다. electricChargeFlowMaterial이 없으면 아무것도 하지 않는다.</summary>
        private void SetChargeProgress(float _progress)
        {
            if (electricChargeFlowMaterial == null)
            {
                return;
            }

            propertyBlock.Clear();
            propertyBlock.SetFloat(ChargeProgressId, Mathf.Clamp01(_progress));

            foreach (Renderer bodyRenderer in packBodies)
            {
                if (bodyRenderer != null)
                {
                    bodyRenderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private void RestoreMaterials()
        {
            foreach (Renderer bodyRenderer in packBodies)
            {
                if (bodyRenderer == null)
                {
                    continue;
                }

                // 연출에 쓰이지 않은 Body(electricChargeFlowMaterial / thermalDetectionMaterial이 아닌 것)는 머티리얼을 건드리지 않는다.
                Material[] currentMaterials = bodyRenderer.sharedMaterials;
                bool isEffectBody = originalMaterials.ContainsKey(bodyRenderer)
                    || IsAllMaterial(currentMaterials, electricChargeFlowMaterial)
                    || IsAllMaterial(currentMaterials, thermalDetectionMaterial);
                if (!isEffectBody)
                {
                    continue;
                }

                bodyRenderer.SetPropertyBlock(null);

                if (packPrefabOutLine != null)
                {
                    Material[] materials = new Material[currentMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = packPrefabOutLine;
                    }

                    bodyRenderer.sharedMaterials = materials;
                }
                else if (originalMaterials.TryGetValue(bodyRenderer, out Material[] original))
                {
                    bodyRenderer.sharedMaterials = original;
                }
            }

            originalMaterials.Clear();
        }
    }
}
