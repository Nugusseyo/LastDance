using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>제자리에 머문다. duration이 0이면 스스로 끝나지 않고 Phase가 바뀌거나 인터럽트가 들어올 때까지 기다린다(의도된 설계).</summary>
    [Serializable]
    public sealed class StayState : CustomerState
    {
        [Tooltip("머무는 시간. 0이면 Phase가 바뀔 때까지 무한 대기한다.")]
        [SerializeField, Min(0f)] private float duration;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (duration <= 0f)
            {
                // 취소로만 벗어난다. 예외를 던지지 않고 조용히 끝난다.
                await UniTask.WaitUntilCanceled(ct);
                return VisitOutcome.Done;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            return VisitOutcome.Done;
        }
    }
}
