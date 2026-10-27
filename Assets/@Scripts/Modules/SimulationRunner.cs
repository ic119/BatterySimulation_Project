using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// SimulationDataModel의 작업(stepList)을 순서대로 실행한다. 작업마다 duringTime(초)이 지난 뒤에야 다음 작업으로 넘어가며,
    /// 무엇을 보여줄지(연출)는 알지 못하고 StepBegan / StepEnded / Finished 이벤트로 시점만 알린다.
    /// 코루틴을 돌리기 위해 MonoBehaviour(host)를 빌려 쓰므로, host가 비활성화되어도 Stop()을 직접 불러야 상태가 정리된다.
    /// </summary>
    public class SimulationRunner
    {
        private readonly MonoBehaviour host;
        private Coroutine routine;

        public bool IsRunning { get; private set; }

        /// <summary>작업의 시작 시점에 호출된다.</summary>
        public event Action<SimulationStepModel> StepBegan;

        /// <summary>작업이 진행되는 동안 매 프레임 (작업, 진행도 0~1)로 호출된다. 진행도는 지난 시간 / duringTime이다.</summary>
        public event Action<SimulationStepModel, float> StepProgressed;

        /// <summary>작업의 duringTime이 지난 시점에 호출된다.</summary>
        public event Action<SimulationStepModel> StepEnded;

        /// <summary>모든 작업이 끝까지 실행됐을 때 한 번 호출된다. Stop()으로 중단된 경우에는 호출되지 않는다.</summary>
        public event Action Finished;

        public SimulationRunner(MonoBehaviour _host)
        {
            host = _host;
        }

        /// <summary>
        /// _data의 작업을 순서대로 실행한다. 시작 시점의 복사본으로 진행하므로 실행 중 stepList가 바뀌어도 영향이 없다.
        /// 이미 실행 중이거나 작업이 없으면 false.
        /// </summary>
        public bool Start(SimulationDataModel _data)
        {
            if (IsRunning || _data == null || _data.StepCount == 0)
            {
                return false;
            }

            IsRunning = true;
            routine = host.StartCoroutine(Run(_data.ToQueue()));
            return true;
        }

        /// <summary>실행 중인 작업을 중단한다. 진행 중이던 작업의 StepEnded와 Finished는 호출되지 않는다.</summary>
        public void Stop()
        {
            if (routine != null)
            {
                host.StopCoroutine(routine);
                routine = null;
            }

            IsRunning = false;
        }

        /// <summary>
        /// 프레임 단위로 시간을 누적해 duringTime이 지나면 다음 작업으로 넘어간다.
        /// 한 작업에서 초과된 시간은 다음 작업으로 넘겨, 전체 실행 시간이 duringTime의 합과 어긋나지 않게 한다.
        /// </summary>
        private IEnumerator Run(Queue<SimulationStepModel> _stepQueue)
        {
            float carry = 0f;

            while (_stepQueue.Count > 0)
            {
                SimulationStepModel step = _stepQueue.Dequeue();
                StepBegan?.Invoke(step);

                float elapsed = carry;
                while (elapsed < step.duringTime)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                    StepProgressed?.Invoke(step, Mathf.Clamp01(elapsed / step.duringTime));
                }

                carry = elapsed - step.duringTime;
                StepEnded?.Invoke(step);
            }

            routine = null;
            IsRunning = false;
            Finished?.Invoke();
        }
    }
}
