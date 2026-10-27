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

        /// <summary>연출을 입힐 대상(선택된 Container). 실행을 시작하기 전에 지정한다.</summary>
        public Transform Target { get; set; }

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
        /// 재생 중인 연출을 모두 정리하고 대상을 비운다. 작업이 끝났을 때와, Runner가 중간에 중단되었을 때(StepEnded가 오지 않으므로) 호출한다.
        /// </summary>
        public void Clear()
        {
            Target = null;
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
