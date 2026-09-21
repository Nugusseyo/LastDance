using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>같은 약속 이름을 가진 다른 손님과 만난다. 다른 차에서 내린 손님이어도 된다.
    /// 지금은 중간 지점에서 만나 잠시 머무는 데까지만 한다 — 싸움 연출은 애니메이션과 피격이 생긴 뒤에 붙인다.</summary>
    [Serializable]
    public sealed class MeetUpState : CustomerState
    {
        [Tooltip("짝을 맺어 줄 등록소. 같은 에셋을 쓰는 손님끼리만 만난다.")]
        [SerializeField] private CustomerRendezvousSO rendezvous;

        [Tooltip("약속 이름. 같은 이름끼리 짝이 된다. 싸움·대화처럼 용도가 다르면 이름을 나눈다.")]
        [SerializeField] private string meetKey = "fight";

        [Tooltip("짝이 나타날 때까지 기다릴 시간(초). 넘기면 아무 일 없이 다음 행동으로 넘어간다.")]
        [SerializeField, Min(0f)] private float waitTimeout = 10f;

        [Tooltip("만날 지점까지 걸어갈 때의 한계 시간(초).")]
        [SerializeField, Min(0f)] private float moveTimeout = 15f;

        [Tooltip("만나서 머무는 시간(초). 나중에 싸움 연출이 들어갈 자리다.")]
        [SerializeField, Min(0f)] private float stayDuration = 3f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (rendezvous == null)
            {
                Debug.LogError($"[{nameof(MeetUpState)}] 만남 등록소를 지정해야 합니다.", Ctx.Customer);
                return VisitOutcome.Failed;
            }

            try
            {
                if (!rendezvous.TryPair(meetKey, Ctx, out CustomerContext _))
                {
                    // 내가 먼저 왔다. 짝이 나를 집어갈 때까지 기다린다.
                    float deadline = Time.time + waitTimeout;

                    while (Ctx.Partner == null)
                    {
                        if (Time.time > deadline)
                        {
                            // 아무도 안 왔다. 방문을 망치지 않고 다음 행동으로 넘긴다.
                            return VisitOutcome.Blocked;
                        }

                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }

                VisitOutcome outcome = await MoveAndWait(Ctx.MeetPoint, moveTimeout, ct);

                if (outcome != VisitOutcome.Done)
                {
                    return outcome;
                }

                // 상대가 오는 동안 머문다. 그 사이 상대가 퇴치되거나 반납되면 Partner가 null이 되어 여기서 빠진다.
                float until = Time.time + stayDuration;

                while (Time.time < until)
                {
                    if (Ctx.Partner == null)
                    {
                        return VisitOutcome.Blocked;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                //TODO 싸움 연출 추가. Animator와 피격이 붙으면 여기서 재생한다.

                return VisitOutcome.Done;
            }
            finally
            {
                // Phase 전환이나 인터럽트로 취소되어도 여기는 반드시 지난다.
                // 빼먹으면 등록소에 내가 영영 남아 다음 손님이 유령과 짝지어진다.
                rendezvous.Leave(meetKey, Ctx);
            }
        }

        /// <summary>방문이 중단돼 Run이 다시 돌지 않는 경우에도 짝을 풀기 위해 여기서도 정리한다.</summary>
        public override void Reset()
        {
            if (rendezvous != null && Ctx != null)
            {
                rendezvous.Leave(meetKey, Ctx);
            }
        }
    }
}
