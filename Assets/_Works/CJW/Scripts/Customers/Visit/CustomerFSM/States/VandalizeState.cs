using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Interaction;
using _Works.CJW.Scripts.Customers.Movement;
using _Works.CJW.Scripts.MapSystems;
using DevLib.AnimatorSystem;
using DevLib.ObjectPool.Runtime;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>물건 하나를 찾아가 때린다. 자판기를 때리든 남의 차에 주먹질을 하든 대상을 고르는 방법만 다르고
    /// 나머지는 같아서 한 상태로 묶었다 — 종류가 늘 때 프리팹에서 대상만 바꾸면 된다.
    /// 맞는 쪽은 <see cref="IVandalTarget"/>으로만 안다. 대상이 그걸 구현하지 않았으면 때리는 시늉만 하고 끝난다.</summary>
    [Serializable]
    public sealed class VandalizeState : CustomerState, IDestinationState, ISpeakingState
    {
        public int[] SpokenLines => targetLines;

        public MapPointType Destination => source == TargetSource.MapPoint ? targetPoint : MapPointType.None;

        /// <summary>때릴 물건을 고르는 방법.</summary>
        private enum TargetSource
        {
            /// <summary>맵에 등록된 지점 중 가장 가까운 곳. 자판기처럼 자리가 정해진 물건에 쓴다.</summary>
            MapPoint = 0,

            /// <summary>주변에 서 있는 다른 차. 자기가 타고 온 차는 고르지 않는다.</summary>
            OtherCar = 1,

            /// <summary>인터럽트를 건 쪽이 <see cref="CustomerContext.Target"/>에 넣어 둔 대상.</summary>
            ContextTarget = 2,
        }

        [Header("대상")]
        [Tooltip("때릴 물건을 고르는 방법.")]
        [SerializeField] private TargetSource source = TargetSource.MapPoint;

        [Tooltip("MapPoint일 때 찾을 지점의 종류.")]
        [SerializeField] private MapPointType targetPoint = MapPointType.VendingMachine;

        [Tooltip("OtherCar일 때 둘러볼 반경(m).")]
        [SerializeField, Min(0f)] private float searchRadius = 25f;

        [Tooltip("대상이 없을 때 다시 찾아볼 시간(초). 닿는 자판기가 아직 없으면 생길 때까지 기다린다. 0이면 바로 넘어간다.\n" +
                 "OtherCar는 이 시간과 상관없이 차가 나타날 때까지 돌아다니며 찾는다.")]
        [SerializeField, Min(0f)] private float targetWaitTimeout = 10f;

        [Tooltip("끄면(기본) 아직 때릴 대상이 없는 동안은 일반 손님처럼 돌아다니고 평판도 일반인으로 셈한다(때리면 깎이고, 그냥 떠나도 깎이지 않는다).\n" +
                 "차를 때리는 손님은 돌아다니다 차가 나타나면 그때부터 진상으로 돌아가 때리러 간다. 자판기처럼 대상이 아예 없으면 끝까지 돌아다닌다.\n" +
                 "켜면 예전처럼 대상이 없을 땐 다음 행동으로 넘어가고 평판은 늘 진상으로 셈한다.\n" +
                 "켜는 쪽을 옵션으로 둔 건 [SerializeReference] 상태의 새 필드가 기존 프리팹에 false로 들어가기 때문이다.")]
        [SerializeField] private bool standIfNoTarget;

        [Header("접근")]
        [Tooltip("대상에서 이만큼 떨어져 선다(m). 0이면 대상 한가운데로 파고들어 몸이 겹친다.")]
        [SerializeField, Min(0f)] private float standoff = 1.2f;

        [Tooltip("대상까지 걸어갈 때의 한계 시간(초).")]
        [SerializeField, Min(0f)] private float moveTimeout = 40f;

        [Tooltip("몸을 돌리는 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

        [Header("타격")]
        [Tooltip("때릴 때 재생할 클립 후보. 여럿이면 타격마다 하나를 무작위로 고른다.")]
        [SerializeField] private HashDataSO[] hitClips;

        [Tooltip("몇 번 때리는지. 0이면 스스로 멈추지 않고 Phase가 바뀌거나 인터럽트가 들어올 때까지 계속 때린다.")]
        [SerializeField, Min(0)] private int hitCount = 4;

        [Tooltip("한 번 때리는 데 걸리는 시간(초). 클립 길이에 맞춰야 동작이 끊기지 않는다.")]
        [SerializeField, Min(0.05f)] private float hitInterval = 0.8f;

        [Tooltip("한 대의 세기. 맞는 쪽이 이 값을 어떻게 쓸지는 그쪽이 정한다.")]
        [SerializeField, Min(0f)] private float power = 1f;

        [Tooltip("켜면 대상 앞까지 가서 동작만 하고 때리지는 않는다(타격·타격 소리 없음). 남의 차 앞에서 말을 걸거나 자판기 앞에서 서성이는 정상 손님에 쓴다.\n" +
                 "기본값을 false로 둔 건 이 필드가 없던 프리팹이 기존처럼 때리게 하기 위해서다.")]
        [SerializeField] private bool noContact;

        [Header("사운드")]
        [Tooltip("주먹이 대상에 닿을 때마다 낼 소리. 맞은 자리에서 난다.")]
        [SerializeField] private SoundClipSo hitSound;

        [Header("말풍선")]
        [Tooltip("말풍선을 꺼내 올 풀. 비우면 말하지 않는다.")]
        [SerializeField] private PoolManagerSO speechPool;

        [Tooltip("말풍선 프리팹의 풀 아이템.")]
        [SerializeField] private PoolItemSO speechBubble;

        [Tooltip("때릴 대상(차·자판기)을 찾아 다가가기 시작할 때 할 대사(HumanDB index) 후보. 비우면 말하지 않는다.\n" +
                 "'아저씨 창문 내려봐요'처럼 대상에게 거는 말은 여기에 둔다 — 내리자마자 말하면 차가 없어도 허공에 대고 말한다.")]
        [SerializeField] private int[] targetLines;

        [Tooltip("손님 기준으로 말풍선을 띄울 위치(m). 0이면 머리 위 기본 위치를 쓴다.")]
        [SerializeField] private Vector3 speechOffset;

        [Tooltip("말풍선이 뜰 때 낼 소리.")]
        [SerializeField] private SoundClipSo speechSound;

        /// <summary>이번 방문에서 이미 대상을 찾아 진상 짓을 시작했는지. 맞아서 이 행동이 처음부터 다시 돌아도 일반인 취급으로 되돌리지 않는다.</summary>
        [NonSerialized] private bool _engaged;

        /// <summary>마지막으로 말을 건 대상. 같은 대상에게 다시 다가갈 때(맞고 나서 등) 같은 말을 되풀이하지 않는다.</summary>
        [NonSerialized] private Transform _spokenTo;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            _unreachableCar = null;

            int hits = 0;

            try
            {
                // 남의 차를 때리는 손님은 차가 떠나도 멈추지 않는다. 다음 차를 찾고, 없으면 돌아다니며 새 차를 기다린다.
                // hitCount가 0이면 취소(Phase 전환·퇴치)로만 벗어난다.
                while (hitCount == 0 || hits < hitCount)
                {
                    // 걸을 때는 연출을 접어야 한다. 안 접으면 이동 모듈이 걷기 클립을 틀지 못해 주먹을 뻗은 채 미끄러진다.
                    customer.ActionAnimator?.End();

                    // 아직 대상을 못 찾았다. 차가 나타날 때까지 일반 손님처럼 돌아다니므로 그동안은 평판도 일반인으로 셈한다.
                    if (!_engaged && !standIfNoTarget)
                    {
                        Ctx.Harmless = true;
                    }

                    Transform target = source == TargetSource.OtherCar ? await WanderUntilCar(ct) : await WaitForTarget(ct);
                    if (target == null)
                    {
                        if (hits > 0)
                        {
                            break;
                        }

                        if (!standIfNoTarget)
                        {
                            // 때릴 게 아예 없다(자판기가 없는 맵 등). 멍하니 서 있지 않고 일반 손님처럼 돌아다닌다(평판도 일반인으로).
                            Debug.Log($"[{nameof(VandalizeState)}] {customer.name}이(가) 때릴 대상이 없어 일반 손님처럼 돌아다닙니다.", customer);
                            customer.ActionAnimator?.End();
                            return await Wander.Run(ct);
                        }

                        // 때릴 게 없다고 방문을 세우지는 않는다. 다음 행동으로 넘긴다.
                        Debug.LogWarning($"[{nameof(VandalizeState)}] {customer.name}이(가) 때릴 대상을 찾지 못했습니다.", customer);
                        return VisitOutcome.Blocked;
                    }

                    // 대상이 생겼다. 이제부터는 진상이다. 대상에게 거는 말도 이때 한다.
                    _engaged = true;
                    Ctx.Harmless = false;
                    SayTo(target, ct);

                    if (!await Approach(target, ct))
                    {
                        if (source == TargetSource.OtherCar)
                        {
                            // 닿지 않는 차에 매달리면 그 앞에서 멍하니 선다. 잠시 그 차를 빼고 다른 차를 찾는다.
                            MarkUnreachable(target);
                            continue;
                        }

                        if (IsGone(target))
                        {
                            // 걸어가는 사이 대상이 사라졌다. 빈 자리에 주먹질하지 않는다.
                            break;
                        }

                        return VisitOutcome.Blocked;
                    }

                    StrikeResult result = await Strike(target, hitCount == 0 ? int.MaxValue : hitCount - hits, ct);
                    hits += result.Hits;

                    // 차를 때리는 손님만 대상이 떠나도 이어 간다. 자판기처럼 한 대상만 때리는 손님은 대상이 사라지면 끝낸다.
                    if (result.Gone && source != TargetSource.OtherCar)
                    {
                        break;
                    }
                }
            }
            finally
            {
                // 퇴치나 Phase 전환으로 끊겨도 여기는 반드시 지난다.
                // 빼먹으면 손님이 걷는 내내 주먹을 휘두르는 클립을 붙들고 있다.
                customer.ActionAnimator?.End();
            }

            return VisitOutcome.Done;
        }

        private readonly struct StrikeResult
        {
            public readonly int Hits;

            /// <summary>대상이 사라져서 멈췄는지. false면 다 때렸거나 몸이 밀려나 다시 다가가야 한다.</summary>
            public readonly bool Gone;

            public StrikeResult(int hits, bool gone)
            {
                Hits = hits;
                Gone = gone;
            }
        }

        /// <summary>대상 앞까지 걸어가 주먹이 닿는 자리에 선다. 가는 사이 대상이 사라졌거나 끝내 닿지 못하면 false.</summary>
        private async UniTask<bool> Approach(Transform target, CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            Vector3 approach = GetApproachPoint(target, customer.transform.position);
            if (await MoveAndWait(approach, moveTimeout, ct) == VisitOutcome.Blocked)
            {
                return false;
            }

            // 차는 몸체 표면에 붙어 서야 주먹이 닿는다. 가는 동안 차가 조금 움직였거나 누구에게 막혀 멀찍이 섰으면
            // 지금 자리에서 설 자리를 다시 잡아 한두 번 더 다가간다.
            for (int retry = 0; retry < CarApproachRetries && !IsGone(target) && IsTooFarFromCar(target, customer.transform.position); retry++)
            {
                approach = GetApproachPoint(target, customer.transform.position);
                if (await MoveAndWait(approach, moveTimeout, ct) == VisitOutcome.Blocked)
                {
                    break;
                }
            }

            return !IsGone(target) && !IsTooFarFromCar(target, customer.transform.position);
        }

        /// <summary>대상을 최대 <paramref name="maxHits"/>번 때린다. 대상이 사라지거나 루트 모션에 밀려 주먹이 닿지 않게 되면 멈춘다.</summary>
        private async UniTask<StrikeResult> Strike(Transform target, int maxHits, CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            IVandalTarget victim = target.GetComponentInParent<IVandalTarget>();

            // 걷기를 접지 않으면 이동 모듈이 타격 중에도 방향을 붙들고 있다.
            customer.Mover?.Stop();

            // 도착하자마자 휘두른다. 다 돌아선 뒤에 틀면 그 사이 멍하니 서 있는 것처럼 보인다 — 돌아서는 건 첫 동작과 겹쳐도 어색하지 않다.
            BeginSwing();
            float nextHit = Time.time + hitInterval;
            await FaceTowards(AimPoint(target, customer.transform.position), turnSpeed, ct);

            int hits = 0;
            while (hits < maxHits)
            {
                // 차가 떠나는 순간 멈춘다. 타격 시각까지 통째로 기다리면 떠난 자리에 주먹질을 한다.
                await UniTask.WaitUntil(() => Time.time >= nextHit || IsGone(target), cancellationToken: ct);
                if (IsGone(target))
                {
                    return new StrikeResult(hits, true);
                }

                // 때리는 동작이 끝난 뒤에 알린다. 먼저 알리면 소리와 이펙트가 주먹보다 앞선다.
                Vector3 point = AimPoint(target, customer.transform.position);
                if (!noContact)
                {
                    customer.Sound?.Play(hitSound, point);
                }

                if (!noContact && victim != null && victim.CanTakeHit)
                {
                    Vector3 direction = point - customer.transform.position;
                    direction.y = 0f;

                    victim.TakeVandalHit(new VandalHit(customer.gameObject, power, point, direction.normalized));
                }

                hits++;
                if (hits >= maxHits)
                {
                    break;
                }

                // 주먹질의 루트 모션이 몸을 조금씩 밀어낸다. 닿지 않을 만큼 멀어졌으면 다시 다가간다.
                if (IsTooFarFromCar(target, customer.transform.position))
                {
                    break;
                }

                // 다음 동작을 곧바로 잇는다. 클립이 끝나기 전에 다음 클립을 틀어야 주먹질이 끊기지 않는다.
                BeginSwing();
                nextHit += hitInterval;
            }

            return new StrikeResult(hits, false);
        }

        private void BeginSwing()
        {
            HashDataSO clip = PickClip();
            if (clip != null)
            {
                Ctx.Customer.ActionAnimator?.Begin(clip, true);
            }
        }

        /// <summary>조건이 안 맞아 이 행동을 건너뛸 때도 서 있지 않고 돌아다닌다.</summary>
        public override CustomerState WhenConditionFails => standIfNoTarget ? null : Wander;

        [NonSerialized] private WanderState _wander;

        /// <summary>때릴 대상이 없을 때 돌릴 돌아다니기. 프리팹에 필드로 두면 기존 손님들에 빈 값으로 들어가므로 코드로 만든다.</summary>
        private WanderState Wander
        {
            get
            {
                _wander ??= new WanderState();
                _wander.Bind(Ctx);
                return _wander;
            }
        }

        /// <summary>때릴 차가 나타날 때까지 주변을 돌아다닌다. 제자리에 서서 기다리면 손님이 굳은 것처럼 보인다.
        /// 걷는 도중에도 계속 둘러보다가 차가 서면 곧장 그 차를 돌려준다. 취소(Phase 전환)로만 빈손으로 끝난다.</summary>
        private async UniTask<Transform> WanderUntilCar(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            IMover mover = customer.Mover;

            while (true)
            {
                Transform car = FindOtherCar();
                if (car != null)
                {
                    return car;
                }

                if (mover != null && mover.IsReady && TryPickWanderPoint(out Vector3 destination))
                {
                    mover.MoveTo(destination);

                    // SetDestination 직후 한 프레임은 경로가 없어 도착으로 보인다.
                    await UniTask.NextFrame(ct);

                    float walkUntil = Time.time + WanderWalkLimit;
                    while (!mover.IsArrived && Time.time < walkUntil)
                    {
                        car = FindOtherCar();
                        if (car != null)
                        {
                            return car;
                        }

                        await UniTask.Delay(TimeSpan.FromSeconds(CarPollInterval), cancellationToken: ct);
                    }

                    mover.Stop();
                }

                // 도착하면 잠깐 서서 둘러본다. 이 사이에도 차가 서면 바로 간다.
                float pauseUntil = Time.time + Random.Range(WanderPauseMin, WanderPauseMax);
                while (Time.time < pauseUntil)
                {
                    car = FindOtherCar();
                    if (car != null)
                    {
                        return car;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(CarPollInterval), cancellationToken: ct);
                }
            }
        }

        /// <summary>돌아다닐 다음 자리. 자기 차 둘레에서 고른다 — 지금 자리를 중심으로 고르면 걸음마다 조금씩 멀어져 결국 가게를 벗어난다.</summary>
        private bool TryPickWanderPoint(out Vector3 destination)
        {
            destination = default;
            AbstractCustomer customer = Ctx.Customer;
            NavMeshAgent agent = customer.Agent;
            if (Ctx.MapData == null || agent == null)
            {
                return false;
            }

            Car ownCar = Ctx.Visit?.Car;
            Vector3 center = ownCar != null ? ownCar.transform.position : customer.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

            return Ctx.MapData.TryGetRandomPosition(center, WanderRadius, filter, WanderTries, IsGoodWanderSpot, out destination);
        }

        private bool IsGoodWanderSpot(Vector3 position)
        {
            // 자기 차 옆에 서면 동행이 타고 내리는 길을 막는다.
            Car ownCar = Ctx.Visit?.Car;
            if (ownCar != null)
            {
                Vector3 delta = position - ownCar.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < WanderMinFromCar * WanderMinFromCar)
                {
                    return false;
                }
            }

            return WalkLengthTo(position) <= WanderRadius * 2f;
        }

        // 아래 값은 직렬화하지 않는다 — [SerializeReference] 상태의 새 필드는 기존 프리팹에 0으로 들어간다.

        /// <summary>돌아다닐 반경(m). 자기 차를 중심으로 잰다.</summary>
        private const float WanderRadius = 12f;

        private const float WanderMinFromCar = 4f;

        private const int WanderTries = 20;

        /// <summary>한 번 걸어가는 한계 시간(초). 막혀서 제자리걸음을 하면 새 자리를 고른다.</summary>
        private const float WanderWalkLimit = 12f;

        private const float WanderPauseMin = 1f;

        private const float WanderPauseMax = 3f;

        /// <summary>돌아다니는 동안 차가 섰는지 보는 간격(초).</summary>
        private const float CarPollInterval = 0.25f;

        /// <summary>닿지 못한 차를 후보에서 빼 두는 시간(초). 그 사이 차가 조금 움직이거나 길이 열릴 수 있어 영영 빼지는 않는다.</summary>
        private const float UnreachableForgetSeconds = 8f;

        [NonSerialized] private Car _unreachableCar;
        [NonSerialized] private float _unreachableUntil;

        private void MarkUnreachable(Transform target)
        {
            _unreachableCar = target != null ? target.GetComponentInParent<Car>() : null;
            _unreachableUntil = Time.time + UnreachableForgetSeconds;
        }

        public override void Reset()
        {
            _unreachableCar = null;
            _engaged = false;
            _spokenTo = null;
        }

        /// <summary>새 대상에게 다가가기 시작할 때 한마디 한다. 말풍선은 걸어가며 머리 위를 따라다니다 대사가 끝나거나 이 행동이 끊기면 접힌다.</summary>
        private void SayTo(Transform target, CancellationToken ct)
        {
            if (target == _spokenTo)
            {
                return;
            }

            _spokenTo = target;
            if (CustomerSpeech.TrySay(Ctx, speechPool, speechBubble, targetLines, speechOffset, speechSound, ct, out UniTask speaking))
            {
                speaking.Forget();
            }
        }

        /// <summary>차를 때릴 때 차 몸체 표면에서 설 거리(m). 몸 반지름(0.5)에 조금 더한 값이라 사실상 붙어 선다.
        /// 차가 파낸 NavMesh 구멍도 몸 반지름만큼 넓어 이보다 가까이는 갈 수 없다.
        /// 직렬화하지 않는다 — [SerializeReference] 상태의 새 필드는 기존 프리팹에 0으로 들어간다.</summary>
        private const float CarPunchReach = 0.6f;

        private const int CarApproachRetries = 2;

        /// <summary>대상이 없어졌는지. 차는 풀로 돌아가도 파괴되지 않으므로 꺼졌거나 다시 움직이기 시작했으면(떠나는 중) 사라진 것으로 본다.</summary>
        private static bool IsGone(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return true;
            }

            Car car = target.GetComponentInParent<Car>();
            return car != null && !car.IsArrived;
        }

        /// <summary>차를 때리기엔 멀리 섰는지. 설 거리에 멈춤 오차(stoppingDistance)만큼 여유를 둔다.</summary>
        private static bool IsTooFarFromCar(Transform target, Vector3 from)
        {
            Car car = target.GetComponentInParent<Car>();
            return car != null && CarLandings.DistanceToBody(car, from) > CarPunchReach + 0.5f;
        }

        /// <summary>때릴 때 바라볼 곳. 차면 가장 가까운 몸체 표면, 아니면 대상 위치.</summary>
        private static Vector3 AimPoint(Transform target, Vector3 from)
        {
            Car car = target.GetComponentInParent<Car>();
            return car != null && CarLandings.TryClosestPointOnBody(car, from, out Vector3 surface) ? surface : target.position;
        }

        private HashDataSO PickClip()
        {
            if (hitClips == null || hitClips.Length == 0)
            {
                return null;
            }

            return hitClips[Random.Range(0, hitClips.Length)];
        }

        /// <summary>대상에서 손님 쪽으로 <see cref="standoff"/>만큼 물러난 자리. 대상 한가운데로 걸어가면 몸이 겹치고,
        /// NavMesh가 대상 밑을 덮지 않으면 영영 도착하지 못한다.</summary>
        private Vector3 GetApproachPoint(Transform target, Vector3 from)
        {
            // 차는 길쭉해서 중심에서 재면 옆에서는 멀고 앞뒤에서는 붙는다. 차 몸체 표면에서 재 주먹이 닿는 거리에 선다.
            Car car = target.GetComponentInParent<Car>();
            if (car != null && CarLandings.TryClosestPointOnBody(car, from, out Vector3 surface))
            {
                Vector3 outward = from - surface;
                outward.y = 0f;
                if (outward.sqrMagnitude > 0.0001f)
                {
                    return surface + outward.normalized * CarPunchReach;
                }
            }

            Vector3 toCustomer = from - target.position;
            toCustomer.y = 0f;

            if (toCustomer.sqrMagnitude < 0.0001f)
            {
                // 대상 바로 위에 서 있다. 방향을 정할 수 없으니 지금 자리를 그대로 쓴다.
                return from;
            }

            return target.position + toCustomer.normalized * standoff;
        }

        /// <summary>대상을 찾고, 없으면 <see cref="targetWaitTimeout"/> 동안 다시 찾아본다.
        /// 세워 둔 차에 길이 막혀 자판기에 닿지 못하는 순간이 있어서, 한 번 못 찾았다고 할 일을 버리지 않는다.</summary>
        private async UniTask<Transform> WaitForTarget(CancellationToken ct)
        {
            const float retryInterval = 0.5f;
            float deadline = Time.time + targetWaitTimeout;

            while (true)
            {
                Transform target = ResolveTarget();
                if (target != null)
                {
                    return target;
                }

                if (Time.time >= deadline)
                {
                    // 닿는 자판기가 끝내 없으면 가장 가까운 쪽으로라도 간다. 가다 막혀도 제자리보다 낫다.
                    return source == TargetSource.MapPoint && Ctx.MapData != null
                           && Ctx.MapData.TryGetNearest(targetPoint, Ctx.Customer.transform.position, out MapPosition nearest)
                        ? nearest.transform
                        : null;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(retryInterval), cancellationToken: ct);
            }
        }

        private Transform ResolveTarget()
        {
            switch (source)
            {
                case TargetSource.ContextTarget:
                    return Ctx.Target;

                case TargetSource.OtherCar:
                    return FindOtherCar();

                default:
                    if (Ctx.MapData == null)
                    {
                        Debug.LogError($"[{nameof(VandalizeState)}] CustomerFSMModule에 MapData를 지정해야 합니다.", Ctx.Customer);
                        return null;
                    }

                    return TryGetReachablePoint(targetPoint, out MapPosition point) ? point.transform : null;
            }
        }

        /// <summary>주변에서 자기가 타고 온 차가 아닌 차를 하나 찾는다. 가장 가까운 차를 고르므로
        /// 옆자리에 선 차가 있으면 그쪽으로 간다. 물리 탐색 대신 <see cref="CarTraffic"/>에 등록된 차만 본다 —
        /// 차가 Default 레이어라 콜라이더로 훑으면 바닥·건물에 버퍼가 먼저 차서 정작 차를 놓친다.</summary>
        private Transform FindOtherCar()
        {
            AbstractCustomer customer = Ctx.Customer;
            Car ownCar = Ctx.Visit?.Car;
            Vector3 origin = customer.transform.position;
            float maxDistance = searchRadius * searchRadius;

            Car best = null;
            float bestDistance = float.MaxValue;

            IReadOnlyList<ICarTrafficSensor> sensors = CarTraffic.Sensors;
            for (int i = 0; i < sensors.Count; i++)
            {
                if (sensors[i] is not Component sensor)
                {
                    continue;
                }

                Car car = sensor.GetComponentInParent<Car>();

                // 자기가 타고 온 차를 때리면 방문 내내 제자리다. 남의 차만 고른다.
                // 들어오거나 나가는 중인 차는 뺀다. 고른 순간의 위치로 설 자리를 잡으면 차가 멈춘 곳과 어긋나 허공을 때린다.
                if (car == null || car == ownCar || !car.IsArrived)
                {
                    continue;
                }

                if (car == _unreachableCar && Time.time < _unreachableUntil)
                {
                    continue;
                }

                float distance = (car.transform.position - origin).sqrMagnitude;
                if (distance > maxDistance || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = car;
            }

            return best != null ? best.transform : null;
        }
    }
}
