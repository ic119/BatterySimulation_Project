using System.Threading;
using UnityEngine;

namespace Incheol.Utils
{
    /// <summary>
    /// 코루틴의 WaitForSecondsRealtime을 대신하는 Awaitable 타이머.
    /// UI 안내 문구나 확인 상태처럼 "일정 시간 뒤에 되돌리는" 일에 쓴다. Time.timeScale의 영향을 받지 않는다.
    /// </summary>
    public static class AwaitableTimer
    {
        /// <summary>
        /// _seconds(실제 시간)가 지나면 true를 돌려준다. 그 전에 _token이 취소되면 예외 없이 false를 돌려주므로,
        /// 호출하는 쪽은 false면 아무것도 하지 않고 끝내면 된다.
        /// </summary>
        public static async Awaitable<bool> WaitRealtimeAsync(float _seconds, CancellationToken _token)
        {
            float endTime = Time.unscaledTime + _seconds;
            while (Time.unscaledTime < endTime)
            {
                if (_token.IsCancellationRequested)
                {
                    return false;
                }

                await Awaitable.NextFrameAsync();
            }

            return !_token.IsCancellationRequested;
        }
    }
}
