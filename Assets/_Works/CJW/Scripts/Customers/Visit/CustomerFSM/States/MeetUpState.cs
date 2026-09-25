using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>같은 약속 이름을 가진 다른 손님과 만난다. 다른 차에서 내린 손님이어도 된다.
    /// 만난 뒤에는 서로를 마주 보고 지정한 클립을 번갈아 재생한다 — 싸움이든 말다툼이든 클립만 갈아끼우면 된다.
    /// 상대에게 실제로 피해를 주지는 않는다. 둘 다 손님이라 한쪽만 쓰러지면 방문 하나가 갈 곳을 잃기 때문이다.</summary>
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

        [Tooltip("만나서 머무는 시간(초).")]
        [SerializeField, Min(0f)] private float stayDuration = 3f;

        [Header("연출")]
        [Tooltip("만나서 재생할 클립 후보. 한 동작이 끝날 때마다 하나를 무작위로 고른다. 비워두면 마주 선 채로 머물기만 한다.")]
        [SerializeField] private HashDataSO[] fightClips;

        [Tooltip("한 동작에 걸리는 시간(초). 클립 길이에 맞춰야 동작이 끊기지 않는다.")]
        [SerializeField, Min(0.05f)] private float swingInterval = 0.7f;

        [Tooltip("상대를 향해 돌아설 때의 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

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

                // 걷기를 접지 않으면 이동 모듈이 연출 중에도 방향을 붙들고 있다.
                Ctx.Customer.Mover?.Stop();

                CustomerContext partner = Ctx.Partner;
                if (partner?.Customer != null)
                {
                    await FaceTowards(partner.Customer.transform.position, turnSpeed, ct);
                }

                return await Perform(ct);
            }
            finally
            {
                // Phase 전환이나 인터럽트로 취소되어도 여기는 반드시 지난다.
                // 빼먹으면 등록소에 내가 영영 남아 다음 손님이 유령과 짝지어진다.
                rendezvous.Leave(meetKey, Ctx);
            }
        }

        /// <summary>마주 선 채로 <see cref="stayDuration"/>만큼 버틴다. 그 사이 상대가 퇴치되거나 반납되면
        /// Partner가 null이 되므로, 혼자 허공에 주먹질하지 않도록 매번 확인하고 빠져나온다.</summary>
        private async UniTask<VisitOutcome> Perform(CancellationToken ct)
        {
            float until = Time.time + stayDuration;

            try
            {
                while (Time.time < until)
                {
                    if (Ctx.Partner == null)
                    {
                        return VisitOutcome.Blocked;
                    }

                    if (fightClips == null || fightClips.Length == 0)
                    {
                        // 클립이 없으면 예전처럼 마주 선 채로 머물기만 한다.
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        continue;
                    }

                    Ctx.Customer.ActionAnimator?.Begin(fightClips[Random.Range(0, fightClips.Length)]);

                    await UniTask.Delay(TimeSpan.FromSeconds(swingInterval), cancellationToken: ct);
                }

                return VisitOutcome.Done;
            }
            finally
            {
                // 취소로 끊겨도 여기는 반드시 지난다. 빼먹으면 손님이 걷는 내내 주먹을 휘두른다.
                Ctx.Customer.ActionAnimator?.End();
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
