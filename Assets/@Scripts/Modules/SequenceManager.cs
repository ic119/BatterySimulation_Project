using Incheol.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Incheol.Modules
{
    public interface ISequenceStep
    {
        string StepName { get; }
        Awaitable<bool> Execute(CancellationToken _cancellationToken);
    }
    public class SequenceManager : SingletonObject<SequenceManager>
    {
        /// <summary>
        /// 다른 씬의 부트스트랩/단계 실행도 이 매니저를 재사용할 수 있어야 하므로(GameManager/ObjectPoolManager처럼), 씬 전환에 파괴되지 않아야 한다.
        /// </summary>
        protected override bool PersistAcrossScenes => true;

        #region Variable
        [Tooltip("단계 하나가 허용되는 최대 실행 시간(초). 이 시간을 넘기면 실패로 간주하고 다음 처리로 넘어간다. " +
            "단, 각 ISequenceStep 구현체가 CancellationToken을 실제로 감시하고 있어야 타임아웃이 즉시 반영된다.")]
        [SerializeField] private float stepTimeoutSeconds = 30f;

        private readonly Queue<ISequenceStep> sequenceQueue = new Queue<ISequenceStep>();

        private CancellationTokenSource runCts;

        public bool IsRunning { get; private set; }
        public bool HasError { get; private set; }

        /// <summary>
        /// 단계 하나가 끝날 때마다 (완료된 단계 수, 이번 실행에 등록됐던 전체 단계 수)를 알려준다.
        /// 로딩 화면에 진행률("3/6 완료")을 표시하는 용도로 사용할 수 있다.
        /// </summary>
        public event Action<int, int> OnStepCompleted;

        /// <summary>
        /// 이번 실행에 등록됐던 모든 단계가 끝났을 때(성공/실패 여부와 무관하게) 한 번 호출된다.
        /// 인자는 전체 시퀀스 성공 여부(true: 모든 단계 성공, false: 한 단계라도 실패했거나 예외가 발생함)이다.
        /// </summary>
        public event Action<bool> OnSequenceCompleted;

        #endregion

        #region Method
        /// <summary>
        /// 실행할 단계를 큐에 등록한다.
        /// 실행 중에 큐에 몰래 끼워 넣으면 "무엇이 이번 실행에 포함된 것인지"가 모호해지므로,
        /// 의도적으로 실행 중 Enqueue를 막는다. 모든 단계는 DoSequenceAction 호출 전에 등록해야 한다.
        /// </summary>
        public void Enqueue(ISequenceStep _step)
        {
            if (IsRunning)
            {
                DebugLogManager.GenerateErrorMessage<SequenceManager>("시퀀스 실행 중에는 Enqueue할 수 없습니다. 실행이 끝난 뒤 다시 시도하세요.");
                return;
            }

            sequenceQueue.Enqueue(_step);
        }

        /// <summary>
        /// 큐에 등록된 단계를 순서대로 실행한다. 이미 실행 중이면 중복 실행을 막고 경고만 남긴다.
        /// _onComplete는 이번 실행이 끝났을 때(중복 호출로 거부된 경우 포함) 성공 여부와 함께 호출된다.
        /// </summary>
        public void DoSequenceAction(Action<bool> _onComplete = null)
        {
            if (IsRunning)
            {
                DebugLogManager.GenerateErrorMessage<SequenceManager>("이미 시퀀스가 실행 중입니다. DoSequenceAction 중복 호출을 무시합니다.");
                _onComplete?.Invoke(false);
                return;
            }

            _ = StartExecute(_onComplete);
        }

        /// <summary>
        /// 실행 중인 시퀀스를 즉시 취소한다. 대기 중인 단계는 CancellationToken을 통해 취소를 통지받는다
        /// (단, 구현체가 토큰을 감시하고 있어야 실제로 중간에 멈춘다).
        /// </summary>
        public void Cancel()
        {
            runCts?.Cancel();
        }

        /// <summary>
        /// 실패로 중단된 상태(HasError)와 남은 큐를 초기화해서 처음부터 다시 사용할 수 있게 한다.
        /// 실행 중에는 상태가 뒤섞일 수 있으므로 Reset을 거부한다 — 먼저 Cancel로 멈춘 뒤 호출해야 한다.
        /// </summary>
        public void Reset()
        {
            if (IsRunning)
            {
                DebugLogManager.GenerateErrorMessage<SequenceManager>("시퀀스 실행 중에는 Reset할 수 없습니다. 먼저 Cancel을 호출하세요.");
                return;
            }

            HasError = false;
            sequenceQueue.Clear();
        }

        private async Awaitable StartExecute(Action<bool> _onComplete)
        {
            IsRunning = true;
            HasError = false;

            runCts?.Dispose();
            runCts = new CancellationTokenSource();

            int totalCount = sequenceQueue.Count;
            int completedCount = 0;

            try
            {
                while (sequenceQueue.Count > 0)
                {
                    if (HasError || runCts.IsCancellationRequested)
                    {
                        break;
                    }

                    ISequenceStep currentStep = sequenceQueue.Dequeue();
                    bool isSuccess = await ExecuteWithTimeout(currentStep, runCts.Token);

                    completedCount++;
                    OnStepCompleted?.Invoke(completedCount, totalCount);

                    if (!isSuccess)
                    {
                        HasError = true;

                        // 실패 시점에 남아있던 나머지 단계는 이번 실행 기준으로는 더 이상 의미가 없으므로 비운다.
                        // (비우지 않으면 Reset 없이 재실행할 방법이 없어 항상 남지만, 안전하게 명시적으로 비워둔다)
                        sequenceQueue.Clear();

                        DebugLogManager.GenerateErrorMessage<SequenceManager>($"'{currentStep.GetType().Name}' 단계가 실패를 반환하여 시퀀스를 중단합니다.");
                    }
                }
            }
            catch (Exception e)
            {
                // ExecuteWithTimeout이 각 단계의 예외를 이미 처리하므로, 여기 도달하는 예외는
                // StartExecute 자체 로직(예: DebugLogManager 호출 등)에서 발생한 예상 밖의 예외다.
                // 잡지 않으면 IsRunning=false가 실행되지 못해 이후 Enqueue/DoSequenceAction이 영구히 막히므로 반드시 처리한다.
                HasError = true;
                sequenceQueue.Clear();
                DebugLogManager.GenerateErrorMessage<SequenceManager>($"시퀀스 실행 중 처리되지 않은 예외가 발생했습니다: {e}");
            }
            finally
            {
                IsRunning = false;
            }

            bool isSequenceSuccess = !HasError;
            _onComplete?.Invoke(isSequenceSuccess);
            OnSequenceCompleted?.Invoke(isSequenceSuccess);
        }

        /// <summary>
        /// 단계 하나를 stepTimeoutSeconds 안에서 실행한다. 실행 도중 예외가 발생하면
        /// (기존 SequenceController와 달리) 원인을 로그로 남긴 뒤 실패(false)로 처리한다.
        /// </summary>
        private async Awaitable<bool> ExecuteWithTimeout(ISequenceStep _step, CancellationToken _runToken)
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_runToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(stepTimeoutSeconds));

            try
            {
                return await _step.Execute(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (_runToken.IsCancellationRequested)
                {
                    DebugLogManager.GenerateErrorMessage<SequenceManager>($"'{_step.GetType().Name}' 단계가 Cancel 호출로 중단되었습니다.");
                }
                else
                {
                    DebugLogManager.GenerateErrorMessage<SequenceManager>($"'{_step.GetType().Name}' 단계가 {stepTimeoutSeconds}초 안에 끝나지 않아 타임아웃 처리했습니다.");
                }

                return false;
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<SequenceManager>($"'{_step.GetType().Name}' 단계 실행 중 예외가 발생했습니다: {e}");
                return false;
            }
        }

        protected override void OnDestroy()
        {
            runCts?.Cancel();
            runCts?.Dispose();
            base.OnDestroy();
        }
        #endregion
    }
}