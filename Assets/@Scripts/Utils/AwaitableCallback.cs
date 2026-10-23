using System;
using System.Threading;
using UnityEngine;

namespace Incheol.Utils
{
    /// <summary>콜백(Action&lt;bool&gt;)으로 완료를 알리는 API를 await 할 수 있게 감싼다.</summary>
    public static class AwaitableCallback
    {
        /// <summary>
        /// _start에 넘겨준 완료 콜백이 호출될 때까지 기다리고, 콜백이 받은 성공 여부를 돌려준다.
        /// 기다리는 도중 _cancellationToken이 취소되면 OperationCanceledException을 던지며, 그 뒤에 도착한 콜백은 무시한다.
        /// </summary>
        public static async Awaitable<bool> WaitAsync(Action<Action<bool>> _start, CancellationToken _cancellationToken)
        {
            AwaitableCompletionSource<bool> completionSource = new AwaitableCompletionSource<bool>();

            using (_cancellationToken.Register(() => completionSource.TrySetCanceled()))
            {
                _start(success => completionSource.TrySetResult(success));
                return await completionSource.Awaitable;
            }
        }
    }
}
