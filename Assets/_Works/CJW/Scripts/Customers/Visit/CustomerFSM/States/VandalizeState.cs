using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Interaction;
using _Works.CJW.Scripts.MapSystems;
using DevLib.AnimatorSystem;
using DevLib.SoundSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>물건 하나를 찾아가 때린다. 자판기를 때리든 남의 차에 주먹질을 하든 대상을 고르는 방법만 다르고
    /// 나머지는 같아서 한 상태로 묶었다 — 종류가 늘 때 프리팹에서 대상만 바꾸면 된다.
    /// 맞는 쪽은 <see cref="IVandalTarget"/>으로만 안다. 대상이 그걸 구현하지 않았으면 때리는 시늉만 하고 끝난다.</summary>
    [Serializable]
    public sealed class VandalizeState : CustomerState, IDestinationState
    {
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

        [Tooltip("대상이 없을 때 다시 찾아볼 시간(초). 때릴 차나 닿는 자판기가 아직 없으면 생길 때까지 기다린다. 0이면 바로 넘어간다.")]
        [SerializeField, Min(0f)] private float targetWaitTimeout = 10f;

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

        [Header("사운드")]
        [Tooltip("주먹이 대상에 닿을 때마다 낼 소리. 맞은 자리에서 난다.")]
        [SerializeField] private SoundClipSo hitSound;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            Transform target = await WaitForTarget(ct);
            if (target == null)
            {
                // 때릴 게 없다고 방문을 세우지는 않는다. 다음 행동으로 넘긴다.
                Debug.LogWarning($"[{nameof(VandalizeState)}] {customer.name}이(가) 때릴 대상을 찾지 못했습니다.", customer);
                return VisitOutcome.Blocked;
            }

            // 걸어가는 사이 대상 차가 떠날 수 있다. 그러면 남은 차 중에서 한 번 더 고른다.
            for (int attempt = 0; ; attempt++)
            {
                Vector3 approach = GetApproachPoint(target, customer.transform.position);

                VisitOutcome moved = await MoveAndWait(approach, moveTimeout, ct);
                if (moved == VisitOutcome.Blocked)
                {
                    return moved;
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

                if (!IsGone(target))
                {
                    break;
                }

                // 대상이 사라졌다. 빈 자리에 주먹질하지 않는다.
                target = attempt < RetargetLimit ? ResolveTarget() : null;
                if (target == null)
                {
                    return VisitOutcome.Blocked;
                }
            }

            // 걷기를 접지 않으면 이동 모듈이 타격 중에도 방향을 붙들고 있다.
            customer.Mover?.Stop();
            await FaceTowards(AimPoint(target, customer.transform.position), turnSpeed, ct);

            IVandalTarget victim = target.GetComponentInParent<IVandalTarget>();

            try
            {
                // hitCount가 0이면 취소(Phase 전환·퇴치)로만 벗어난다.
                for (int i = 0; hitCount == 0 || i < hitCount; i++)
                {
                    // 때리는 도중 차가 떠나면 멈춘다.
                    if (IsGone(target))
                    {
                        break;
                    }

                    HashDataSO clip = PickClip();
                    if (clip != null)
                    {
                        customer.ActionAnimator?.Begin(clip);
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(hitInterval), cancellationToken: ct);

                    // 때리는 동작이 끝난 뒤에 알린다. 먼저 알리면 소리와 이펙트가 주먹보다 앞선다.
                    Vector3 point = AimPoint(target, customer.transform.position);
                    customer.Sound?.Play(hitSound, point);

                    if (victim != null && victim.CanTakeHit)
                    {
                        Vector3 direction = point - customer.transform.position;
                        direction.y = 0f;

                        victim.TakeVandalHit(new VandalHit(customer.gameObject, power, point, direction.normalized));
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

        /// <summary>차를 때릴 때 차 몸체 표면에서 설 거리(m). 몸 반지름(0.5)에 조금 더한 값이라 사실상 붙어 선다.
        /// 차가 파낸 NavMesh 구멍도 몸 반지름만큼 넓어 이보다 가까이는 갈 수 없다.
        /// 직렬화하지 않는다 — [SerializeReference] 상태의 새 필드는 기존 프리팹에 0으로 들어간다.</summary>
        private const float CarPunchReach = 0.6f;

        private const int CarApproachRetries = 2;

        /// <summary>걸어가는 사이 대상이 사라졌을 때 새 대상을 고르는 횟수.</summary>
        private const int RetargetLimit = 1;

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
