using System.Collections.Generic;
using UnityEngine;
using SimulationType = Incheol.Models.Define.SimulationType;

namespace Incheol.Modules
{
    /// <summary>
    /// SimulationRunner가 알려주는 작업의 시작 / 끝 시점에 맞춰 SimulationType별 연출을 재생하고 정리한다.
    /// 언제 실행할지(시간 관리)는 Runner가, 무엇을 보여줄지는 이 클래스가 맡는다. 연출은 아직 구현하지 않았다.
    /// </summary>
    public class SimulationEffectPlayer
    {
        private readonly Material packPrefabOutLine;
        private readonly Material electricChargeFlowMaterial;

        private readonly List<Renderer> packBodies = new List<Renderer>();

        /// <summary>연출을 입힐 대상(선택된 Container). SetContainer로 지정한다.</summary>
        public ContainerDataModel Container { get; private set; }

        /// <summary>Container에 속한 Pack들의 몸체(Body) Renderer. SetContainer가 채우고 Clear가 비운다.</summary>
        public IReadOnlyList<Renderer> PackBodies => packBodies;

        public SimulationEffectPlayer(
            SimulationRunner _runner,
            Material _packPrefabOutLine,
            Material _electricChargeFlowMaterial)
        {
            packPrefabOutLine = _packPrefabOutLine;
            electricChargeFlowMaterial = _electricChargeFlowMaterial;

            _runner.StepBegan += OnStepBegan;
            _runner.StepEnded += OnStepEnded;
            _runner.Finished += Clear;
        }

        /// <summary>
        /// _container의 Pack마다 몸체 Renderer를 찾아 PackBodies에 담는다(이전 대상은 비운다). 실행을 시작하기 전에 호출한다.
        /// Pack의 씬 Transform은 _packFactory가 들고 있다. 씬 오브젝트가 없거나 몸체 Renderer가 없는 Pack은 건너뛴다.
        /// </summary>
        public void SetContainer(ContainerDataModel _container, PackFactory _packFactory)
        {
            Clear();
            Container = _container;

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
        /// 재생 중인 연출을 모두 정리하고 대상을 비운다. 작업이 끝났을 때와, Runner가 중간에 중단되었을 때(StepEnded가 오지 않으므로) 호출한다.
        /// </summary>
        public void Clear()
        {
            Container = null;
            packBodies.Clear();
        }

        private void OnStepBegan(SimulationStepModel _step)
        {
            switch (_step.simulationType)
            {
                case SimulationType.Charge:
                    break;
                case SimulationType.Discharge:
                    break;
                case SimulationType.None:
                default:
                    break;
            }
        }

        private void OnStepEnded(SimulationStepModel _step)
        {
            switch (_step.simulationType)
            {
                case SimulationType.Charge:
                    break;
                case SimulationType.Discharge:
                    break;
                case SimulationType.None:
                default:
                    break;
            }
        }
    }
}
