using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>차에서 내리지 않고 주유를 기다린다. 주유기로 걸어가지 않는다는 것만 <see cref="OilingState"/>와 다르다.
    /// 주유 완료는 방문이 차 주유구를 듣고 기록한 값(<see cref="VisitContext.CarFueled"/>)으로 안다 — 언제 주유가 끝났든 놓치지 않는다.
    /// 주유를 받으면 방문에 알려 차가 출발하게 하고, 요구하던 말풍선을 접는다.</summary>
    [Serializable]
    public sealed class InCarFuelState : CustomerState
    {
        public override bool WantsFuel => true;

        [Tooltip("주유를 기다리는 최대 시간(초). 0 이하면 끝날 때까지 기다린다(자동 출발 타이머가 대신 끊는다).")]
        [SerializeField] private float fuelTimeout;

        [Tooltip("이 시간(초)이 지나도 주유가 안 끝나면 늦었다고 알린다(평판 감소). 계속 기다리는 건 fuelTimeout이 정한다. 0 이하면 알리지 않는다.")]
        [SerializeField] private float lateSeconds = 30f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            float start = Time.time;
            bool lateReported = false;

            // 인내심은 늦었다고 알리는 순간 바닥나게 맞춘다. 알리지 않는 손님은 떠나는 순간에 맞춘다.
            Ctx.Customer.Patience?.Begin(lateSeconds > 0f ? lateSeconds : fuelTimeout);

            try
            {
                while (Ctx.Visit == null || !Ctx.Visit.CarFueled)
                {
                    float waited = Time.time - start;

                    if (!lateReported && lateSeconds > 0f && waited >= lateSeconds)
                    {
                        lateReported = true;
                        Ctx.Visit?.ReportFuelLate(Ctx.Customer);
                    }

                    if (fuelTimeout > 0f && waited >= fuelTimeout)
                    {
                        // 기다리다 떠나는 것도 늦은 것이다. 이미 알렸으면 두 번 깎지 않는다.
                        if (!lateReported)
                        {
                            Ctx.Visit?.ReportFuelLate(Ctx.Customer);
                        }

                        // 주유를 못 받고 그만둔다. 주유를 달라던 말풍선도 접는다.
                        Ctx.EndSpeech();
                        return VisitOutcome.Timeout;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                // 인터럽트나 Phase 전환으로 끊겨도 끈다. 안 그러면 떠나는 손님 위에 인내심이 계속 준다.
                Ctx.Customer.Patience?.End();
            }

            Ctx.EndSpeech();
            Ctx.Visit.ReportFueled(Ctx.Customer);
            return VisitOutcome.Done;
        }
    }
}
