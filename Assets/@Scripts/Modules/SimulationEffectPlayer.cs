using System.Collections.Generic;
using Incheol.Utils;
using UnityEngine;
using SimulationType = Incheol.Models.Define.SimulationType;

namespace Incheol.Modules
{
    /// <summary>
    /// SimulationRunner가 알려주는 작업의 시작 / 진행 / 끝 시점에 맞춰 SimulationType별 연출을 재생하고 정리한다.
    /// 언제 실행할지(시간 관리)는 Runner가, 무엇을 보여줄지는 이 클래스가 맡는다. 현재는 Charge만 구현되어 있다.
    /// </summary>
    public class SimulationEffectPlayer
    {
        private static readonly int ChargeProgressId = Shader.PropertyToID("_ChargeProgress");

        private readonly Material packPrefabOutLine;
        private readonly Material electricChargeFlowMaterial;

        private readonly List<Renderer> packBodies = new List<Renderer>();

        // 연출이 바꾼 Body의 원래 머티리얼. Clear에서 되돌린다.
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        // 공유 머티리얼 에셋은 건드리지 않고 Body마다 값을 줄 수 있도록, 진행도는 머티리얼 대신 PropertyBlock으로 넣는다.
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        // Charge / Discharge 연출 중 Container 안의 Module / Cell을 무작위로 빨갛게 점멸시킨다.
        private readonly SimulationBlinkEffect blinkEffect = new SimulationBlinkEffect();

        /// <summary>연출을 입힐 대상(선택된 Container). SetContainer로 지정한다.</summary>
        public ContainerDataModel Container { get; private set; }

        /// <summary>Container에 속한 Pack들의 몸체(Body) Renderer. SetContainer가 채우고 Clear가 비운다.</summary>
        public IReadOnlyList<Renderer> PackBodies => packBodies;

        public SimulationEffectPlayer(SimulationRunner _runner, Material _packPrefabOutLine, Material _electricChargeFlowMaterial)
        {
            packPrefabOutLine = _packPrefabOutLine;
            electricChargeFlowMaterial = _electricChargeFlowMaterial;

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
                }
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
            RestoreMaterials();
            Container = null;
            packBodies.Clear();
        }

        private void OnStepBegan(SimulationStepModel _step)
        {
            switch (_step.simulationType)
            {
                case SimulationType.Charge:
                    EnsureChargeMaterial();
                    SetChargeProgress(0f);
                    blinkEffect.Begin();
                    break;
                case SimulationType.Discharge:
                    EnsureChargeMaterial();
                    SetChargeProgress(1f);
                    blinkEffect.Begin();
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
            }
        }

        private void OnStepEnded(SimulationStepModel _step)
        {
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
                case SimulationType.None:
                default:
                    break;
            }
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
                if (bodyRenderer == null)
                {
                    continue;
                }

                Material[] currentMaterials = bodyRenderer.sharedMaterials;
                if (IsAllChargeMaterial(currentMaterials))
                {
                    continue;
                }

                if (!originalMaterials.ContainsKey(bodyRenderer))
                {
                    originalMaterials[bodyRenderer] = currentMaterials;
                }

                Material[] materials = new Material[currentMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = electricChargeFlowMaterial;
                }

                bodyRenderer.sharedMaterials = materials;
            }
        }

        private bool IsAllChargeMaterial(Material[] _materials)
        {
            if (_materials.Length == 0)
            {
                return false;
            }

            foreach (Material material in _materials)
            {
                if (material != electricChargeFlowMaterial)
                {
                    return false;
                }
            }

            return true;
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

                // 연출에 쓰이지 않은 Body(electricChargeFlowMaterial이 아닌 것)는 머티리얼을 건드리지 않는다.
                Material[] currentMaterials = bodyRenderer.sharedMaterials;
                bool isEffectBody = originalMaterials.ContainsKey(bodyRenderer) || IsAllChargeMaterial(currentMaterials);
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
