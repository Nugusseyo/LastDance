using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>만남 등록소에 이름만 올려 두고 곧바로 끝난다. 하차(Unloading) 시퀀스 끝에 둔다.
    /// <see cref="MeetUpState"/>는 대기(Waiting) 단계에서야 돌기 때문에, 같은 차 동행이 다 내릴 때까지 짝을 찾지 못한다 —
    /// 그동안 먼저 와서 기다리던 손님은 이 손님이 내린 걸 모른다. 여기서 먼저 짝을 맺어 두면 기다리던 쪽은 바로 만나러 출발하고,
    /// 이 손님의 <see cref="MeetUpState"/>는 맺어진 짝을 그대로 이어받는다.
    /// 짝을 푸는 일은 <see cref="MeetUpState"/>가 맡는다. 이 상태는 올리기만 하므로 같은 등록소·약속 이름을 써야 한다.</summary>
    [Serializable]
    public sealed class JoinRendezvousState : CustomerState
    {
        [Tooltip("짝을 맺어 줄 등록소. 뒤에 올 MeetUpState와 같은 에셋이어야 한다.")]
        [SerializeField] private CustomerRendezvousSO rendezvous;

        [Tooltip("약속 이름. 뒤에 올 MeetUpState와 같아야 한다.")]
        [SerializeField] private string meetKey = "fight";

        /// <summary>내리는 연출이 끝나 NavMesh 위에 설 때까지 기다리는 최대 시간(초). 서기 전에 짝을 맺으면
        /// 만날 자리를 차 안의 위치로 계산해 엉뚱한 곳을 고른다.</summary>
        private const float ReadyWaitLimit = 3f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (rendezvous == null)
            {
                Debug.LogError($"[{nameof(JoinRendezvousState)}] 만남 등록소를 지정해야 합니다.", Ctx.Customer);
                return VisitOutcome.Failed;
            }

            if (Ctx.Partner != null)
            {
                return VisitOutcome.Done;
            }

            float deadline = Time.time + ReadyWaitLimit;
            while (Ctx.Customer.Mover != null && !Ctx.Customer.Mover.IsReady && Time.time < deadline)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 짝이 없으면 내가 기다리는 쪽으로 올라간다. 다음 손님이 이 자리를 보고 나를 집어 간다.
            rendezvous.TryPair(meetKey, Ctx, out CustomerContext _);
            return VisitOutcome.Done;
        }
    }
}
