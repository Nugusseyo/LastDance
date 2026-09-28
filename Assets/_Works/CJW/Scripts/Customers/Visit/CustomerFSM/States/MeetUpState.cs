using System;
using System.Threading;
using _Works.CJW.Scripts.Customers.Health;
using _Works.CJW.Scripts.Sounds;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>같은 약속 이름을 가진 다른 손님과 만난다. 다른 차에서 내린 손님이어도 된다.
    /// 만난 뒤에는 서로를 마주 보고 지정한 클립을 번갈아 재생한다 — 싸움이든 말다툼이든 클립만 갈아끼우면 된다.
    /// 상대에게 실제로 피해를 주지는 않는다. 둘 다 손님이라 한쪽만 쓰러지면 방문 하나가 갈 곳을 잃기 때문이다.
    /// 짝이 (플레이어에게 맞아) 죽으면 남은 쪽은 그 자리에 서 있지 않고 자기 차로 돌아가 탄다.</summary>
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
        [SerializeField, Min(0f)] private float moveTimeout = 40f;

        [Tooltip("먼저 도착한 쪽이 상대가 자기 설 자리의 이 반경(m) 안에 들어올 때까지 기다린다. 만날 지점이 랜덤이라 둘의 도착 시각이 크게 벌어진다.")]
        [SerializeField, Min(0.5f)] private float partnerArriveRadius = 2f;

        [Tooltip("싸움을 주고받는 시간(초). 0이면 방문 단계가 바뀌어(탑승) 끊길 때까지 계속 싸운다 — 시간이 끝나면 다음 행동이 가만히 서 있기라 싸우다 멈춘 것처럼 보인다.")]
        [SerializeField, Min(0f)] private float stayDuration = 3f;

        [Header("연출")]
        [Tooltip("공격 차례에 재생할 클립 후보. 차례마다 하나를 무작위로 고른다. 비워두면 마주 선 채로 머물기만 한다.")]
        [SerializeField] private HashDataSO[] fightClips;

        [Tooltip("방어 차례에 재생할 클립. 둘이 번갈아 한 명은 공격, 한 명은 방어한다. 비워두면 방어 차례엔 가만히 선다.")]
        [SerializeField] private HashDataSO blockClip;

        [Tooltip("한 차례(공격 하나를 주고받는 시간, 초). 차례마다 공격과 방어가 바뀐다. 공격 클립 길이에 맞춰야 동작이 끊기지 않는다.")]
        [SerializeField, Min(0.05f)] private float swingInterval = 0.7f;

        [Tooltip("상대를 향해 돌아설 때의 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

        [Header("사운드")]
        [Tooltip("공격 차례가 올 때마다 낼 소리(주먹 휘두르는 소리·기합).")]
        [SerializeField] private SoundClipSo attackSound;

        [Tooltip("방어 차례가 올 때마다 낼 소리(막는 소리). 공격 소리와 같은 순간에 나므로 비워 두어도 된다.")]
        [SerializeField] private SoundClipSo blockSound;

        [Tooltip("싸우는 동안 깔리는 반복 소리(몸싸움·고함). loop를 켜야 한다. 둘이 겹치지 않게 먼저 공격하는 쪽만 튼다.")]
        [SerializeField] private SoundClipSo fightLoopSound;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (rendezvous == null)
            {
                Debug.LogError($"[{nameof(MeetUpState)}] 만남 등록소를 지정해야 합니다.", Ctx.Customer);
                return VisitOutcome.Failed;
            }

            try
            {
                // 짝이 죽은 뒤 맞거나 치여 이 행동이 처음부터 다시 돌면, 짝을 다시 찾지 않고 하던 대로 차로 간다.
                if (_partnerDefeated)
                {
                    return await ReturnToCar(ct);
                }

                // 하차 때 JoinRendezvousState로 이미 짝을 맺었으면 그대로 이어받는다.
                // 다시 TryPair를 부르면 맺은 짝을 두고 등록소에 새로 올라가 버린다.
                bool paired = Ctx.Partner != null && ReferenceEquals(Ctx.Partner.Partner, Ctx);
                if (!paired && !rendezvous.TryPair(meetKey, Ctx, out CustomerContext _))
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

                // 짝이 죽으면 Partner가 곧바로 비므로, 죽었는지는 맺은 순간 잡아 둔 짝으로 본다.
                CustomerContext met = Ctx.Partner;
                if (IsDefeated(met))
                {
                    return await ReturnToCar(ct);
                }

                VisitOutcome outcome = await WalkToMeetPoint(met, ct);

                if (IsDefeated(met))
                {
                    return await ReturnToCar(ct);
                }

                if (outcome != VisitOutcome.Done)
                {
                    return outcome;
                }

                // 걷기를 접지 않으면 이동 모듈이 연출 중에도 방향을 붙들고 있다.
                Ctx.Customer.Mover?.Stop();

                // 주먹이 닿는 거리는 Agent 지름보다 가깝다. 회피를 켜 두면 둘이 서로를 밀어내 자리에서 벌어진다.
                SuppressAvoidance();

                // 혼자 먼저 싸우다 떠나지 않도록 상대가 올 때까지 기다린다.
                if (!await WaitForPartnerArrival(ct))
                {
                    return IsDefeated(met) ? await ReturnToCar(ct) : VisitOutcome.Blocked;
                }

                CustomerContext partner = Ctx.Partner;
                if (partner?.Customer != null)
                {
                    await FaceTowards(partner.Customer.transform.position, turnSpeed, ct);
                }

                VisitOutcome fought = await Perform(ct);
                return fought == VisitOutcome.Blocked && IsDefeated(met) ? await ReturnToCar(ct) : fought;
            }
            finally
            {
                // Phase 전환이나 인터럽트로 취소되어도 여기는 반드시 지난다.
                // 빼먹으면 등록소에 내가 영영 남아 다음 손님이 유령과 짝지어진다.
                rendezvous.Leave(meetKey, Ctx);

                // 빼먹으면 이후 걸을 때 다른 손님을 피하지 않고 뚫고 지나간다.
                RestoreAvoidance();
            }
        }

        /// <summary>만날 지점으로 걷는다. 가는 도중 짝이 죽으면 더 갈 이유가 없으니 바로 멈춘다.</summary>
        private async UniTask<VisitOutcome> WalkToMeetPoint(CustomerContext met, CancellationToken ct)
        {
            // using으로 닫지 않는다. 진 쪽 태스크가 취소를 알아차리는 건 다음 프레임이라, 그 전에 닫으면 닫힌 토큰을 만진다.
            var walk = CancellationTokenSource.CreateLinkedTokenSource(ct);
            UniTask<VisitOutcome> move = MoveAndWait(Ctx.MeetPoint, moveTimeout, walk.Token);
            UniTask defeated = UniTask.WaitUntil(() => IsDefeated(met), cancellationToken: walk.Token);

            try
            {
                (bool moveFinished, VisitOutcome result) = await UniTask.WhenAny(move, defeated);
                return moveFinished ? result : VisitOutcome.Blocked;
            }
            finally
            {
                // 진 쪽을 끊는다. 걷기가 지면 이동 모듈이 finally에서 걷기 애니메이션을 내린다.
                walk.Cancel();
            }
        }

        /// <summary>짝이 맞아 죽었는지. 죽은 짝은 방문에서 빠지며 짝을 놓으므로 <see cref="CustomerContext.Partner"/>로는 알 수 없다.</summary>
        private static bool IsDefeated(CustomerContext met)
        {
            ICustomerHealth health = met?.Customer != null ? met.Customer.Health : null;
            return health != null && health.IsDead;
        }

        /// <summary>싸울 짝이 죽었다. 그 자리에 멍하니 서 있지 않고 자기 차로 걸어가 탄다.
        /// 같은 차 일행도 할 일을 마쳤으면 차가 바로 떠나고, 주유를 기다리는 일행이 있으면 평소대로 주유 뒤에 떠난다.</summary>
        private async UniTask<VisitOutcome> ReturnToCar(CancellationToken ct)
        {
            _partnerDefeated = true;

            // 짝을 놓고 회피를 되돌린 뒤 걷는다. 회피를 끈 채 걸으면 다른 손님을 뚫고 지나간다.
            rendezvous.Leave(meetKey, Ctx);
            RestoreAvoidance();

            // 탑승 단계의 걸음과 같다. 프리팹에 필드로 두면 기존 손님들에 빈 값으로 들어가므로 코드로 만든다.
            _returnToCar ??= new BoardState();
            _returnToCar.Bind(Ctx);
            VisitOutcome outcome = await _returnToCar.Run(ct);

            if (Ctx.Customer.Boarding != null && Ctx.Customer.Boarding.IsBoarded)
            {
                Ctx.Customer.Session?.DepartIfEveryoneDone();
            }

            return outcome;
        }

        /// <summary>짝이 죽어 차로 돌아가는 중인지. 다시 돌아도 짝을 새로 찾지 않게 방문 동안 기억한다.</summary>
        [NonSerialized] private bool _partnerDefeated;

        [NonSerialized] private BoardState _returnToCar;

        /// <summary>상대가 만날 지점 근처에 올 때까지 기다린다. 상대가 사라지거나 이동 한계 시간을 넘기면 false.</summary>
        private async UniTask<bool> WaitForPartnerArrival(CancellationToken ct)
        {
            float deadline = Time.time + moveTimeout;
            float radiusSqr = partnerArriveRadius * partnerArriveRadius;

            while (true)
            {
                CustomerContext partner = Ctx.Partner;
                if (partner?.Customer == null)
                {
                    return false;
                }

                // 둘은 떨어진 자리에 따로 선다. 상대는 내 자리가 아니라 자기 자리에 왔는지 본다.
                Vector3 delta = partner.Customer.transform.position - partner.MeetPoint;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    return true;
                }

                if (Time.time > deadline)
                {
                    return false;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        /// <summary>마주 선 채로 <see cref="stayDuration"/>만큼 공격과 방어를 번갈아 주고받는다. 한 차례(<see cref="swingInterval"/>)마다
        /// 한 명은 공격 클립, 상대는 방어 클립을 틀고 다음 차례에 역할을 바꾼다. 둘이 같은 시작 시각을 써서 차례가 어긋나지 않는다.
        /// 그 사이 상대가 퇴치되거나 반납되면 Partner가 null이 되므로, 혼자 허공에 주먹질하지 않도록 매번 확인하고 빠져나온다.</summary>
        private async UniTask<VisitOutcome> Perform(CancellationToken ct)
        {
            CustomerContext partner = Ctx.Partner;
            if (partner?.Customer == null)
            {
                return VisitOutcome.Blocked;
            }

            // 먼저 준비된 쪽이 시작 시각을 정해 둘 다에 넣는다. 뒤늦게 온 쪽도 같은 시각으로 차례를 센다.
            if (Ctx.ExchangeStartTime < 0f)
            {
                Ctx.ExchangeStartTime = partner.ExchangeStartTime >= 0f ? partner.ExchangeStartTime : Time.time;
                partner.ExchangeStartTime = Ctx.ExchangeStartTime;
            }

            float start = Ctx.ExchangeStartTime;
            float until = stayDuration > 0f ? start + stayDuration : float.PositiveInfinity;

            // 누가 먼저 공격할지는 둘이 똑같이 계산할 수 있는 값으로 정한다.
            bool attacksFirst = Ctx.Customer.GetInstanceID() < partner.Customer.GetInstanceID();
            int lastTurn = -1;

            if (attacksFirst)
            {
                Ctx.Customer.Sound?.PlayLoop(fightLoopSound);
            }

            try
            {
                while (Time.time < until)
                {
                    partner = Ctx.Partner;
                    if (partner?.Customer == null)
                    {
                        return VisitOutcome.Blocked;
                    }

                    int turn = Mathf.FloorToInt((Time.time - start) / swingInterval);
                    if (turn != lastTurn)
                    {
                        lastTurn = turn;

                        // 클립부터 바꾼다. 돌아서기를 먼저 기다리면 더 많이 돌아야 하는 쪽이 늦게 바꿔 잠깐 둘 다 공격하거나 둘 다 막는다.
                        bool attacking = (turn % 2 == 0) == attacksFirst;
                        HashDataSO clip = attacking ? PickFightClip() : blockClip;
                        Ctx.Customer.Sound?.Play(attacking ? attackSound : blockSound);
                        if (clip != null)
                        {
                            // 주먹을 뻗고 막을 때 몸이 실리도록 루트 모션으로 튼다.
                            Ctx.Customer.ActionAnimator?.Begin(clip, true);
                        }
                        else
                        {
                            Ctx.Customer.ActionAnimator?.End();
                        }
                    }

                    HoldGround(partner.Customer.transform.position);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                return VisitOutcome.Done;
            }
            finally
            {
                // 취소로 끊겨도 여기는 반드시 지난다. 빼먹으면 손님이 걷는 내내 주먹을 휘두른다.
                Ctx.Customer.ActionAnimator?.End();

                ISoundEmitter sound = Ctx.Customer.Sound;
                if (sound != null && fightLoopSound != null && sound.CurrentLoop == fightLoopSound)
                {
                    sound.StopLoop();
                }
            }
        }

        /// <summary>회피를 끄기 전 설정. 끄지 않았으면 null.</summary>
        [NonSerialized] private ObstacleAvoidanceType? _savedAvoidance;

        private void SuppressAvoidance()
        {
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (agent == null || _savedAvoidance != null)
            {
                return;
            }

            _savedAvoidance = agent.obstacleAvoidanceType;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        private void RestoreAvoidance()
        {
            NavMeshAgent agent = Ctx?.Customer != null ? Ctx.Customer.Agent : null;
            if (agent != null && _savedAvoidance != null)
            {
                agent.obstacleAvoidanceType = _savedAvoidance.Value;
            }

            _savedAvoidance = null;
        }

        /// <summary>싸우는 동안 자기 자리에서 이만큼(m)은 루트 모션으로 자유롭게 움직인다. 주먹을 뻗으며 앞으로 실리는 정도다.</summary>
        private const float DriftTolerance = 0.2f;

        /// <summary>그보다 밀리면 자기 자리로 돌아오는 속도(m/s). 루트 모션보다 느려 한 동작 안의 움직임은 그대로 보이고,
        /// 여러 번 주고받으며 쌓이는 쏠림만 걷어낸다. 안 걷어내면 공격이 앞으로만 실려 둘이 점점 붙는다.</summary>
        private const float ReturnSpeed = 1.5f;

        /// <summary>두 몸 중심이 이보다 가까워지지 않게 한다(m). 어깨 폭쯤이라 이보다 붙으면 몸이 겹쳐 보인다.</summary>
        private const float MinBodyGap = 0.55f;

        /// <summary>루트 모션으로 쌓이는 쏠림을 걷어내고, 상대를 계속 마주 본다. 매 프레임 부른다.</summary>
        private void HoldGround(Vector3 partnerPosition)
        {
            Transform body = Ctx.Customer.transform;
            float dt = Time.deltaTime;

            Vector3 offset = Ctx.MeetPoint - body.position;
            offset.y = 0f;
            float excess = offset.magnitude - DriftTolerance;
            if (excess > 0f)
            {
                body.position += offset.normalized * Mathf.Min(excess, ReturnSpeed * dt);
            }

            // 주먹이 닿을 만큼 붙어 서므로, 루트 모션이 겹쳐 실리면 몸이 파고든다. 하한보다 가까워지면 곧바로 떼어 놓는다.
            // 둘 다 이걸 부르므로 각자 모자란 만큼의 절반만 물러난다.
            Vector3 apart = body.position - partnerPosition;
            apart.y = 0f;
            float gap = apart.magnitude;
            if (gap < MinBodyGap)
            {
                Vector3 away = gap > 0.0001f ? apart / gap : -body.forward;
                body.position += away * ((MinBodyGap - gap) * 0.5f);
            }

            Vector3 look = partnerPosition - body.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(look, Vector3.up), turnSpeed * dt);
            }
        }

        private HashDataSO PickFightClip()
            => fightClips == null || fightClips.Length == 0 ? null : fightClips[Random.Range(0, fightClips.Length)];

        /// <summary>방문이 중단돼 Run이 다시 돌지 않는 경우에도 짝을 풀기 위해 여기서도 정리한다.</summary>
        public override void Reset()
        {
            RestoreAvoidance();
            _partnerDefeated = false;

            if (rendezvous != null && Ctx != null)
            {
                rendezvous.Leave(meetKey, Ctx);
            }
        }
    }
}
