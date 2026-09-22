using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Interaction;
using _Works.CJW.Scripts.MapSystems;
using DevLib.AnimatorSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>물건 하나를 찾아가 때린다. 자판기를 때리든 남의 차에 주먹질을 하든 대상을 고르는 방법만 다르고
    /// 나머지는 같아서 한 상태로 묶었다 — 종류가 늘 때 프리팹에서 대상만 바꾸면 된다.
    /// 맞는 쪽은 <see cref="IVandalTarget"/>으로만 안다. 대상이 그걸 구현하지 않았으면 때리는 시늉만 하고 끝난다.</summary>
    [Serializable]
    public sealed class VandalizeState : CustomerState
    {
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

        [Tooltip("OtherCar일 때 차를 찾을 레이어. 전부 켜 두면 엉뚱한 콜라이더까지 훑는다.")]
        [SerializeField] private LayerMask carLayers = ~0;

        [Header("접근")]
        [Tooltip("대상에서 이만큼 떨어져 선다(m). 0이면 대상 한가운데로 파고들어 몸이 겹친다.")]
        [SerializeField, Min(0f)] private float standoff = 1.2f;

        [Tooltip("대상까지 걸어갈 때의 한계 시간(초).")]
        [SerializeField, Min(0f)] private float moveTimeout = 15f;

        [Tooltip("몸을 돌리는 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

        [Header("타격")]
        [Tooltip("때릴 때 재생할 클립 후보. 여럿이면 타격마다 하나를 무작위로 고른다.")]
        [SerializeField] private HashDataSO[] hitClips;

        [Tooltip("몇 번 때리는지.")]
        [SerializeField, Min(1)] private int hitCount = 4;

        [Tooltip("한 번 때리는 데 걸리는 시간(초). 클립 길이에 맞춰야 동작이 끊기지 않는다.")]
        [SerializeField, Min(0.05f)] private float hitInterval = 0.8f;

        [Tooltip("한 대의 세기. 맞는 쪽이 이 값을 어떻게 쓸지는 그쪽이 정한다.")]
        [SerializeField, Min(0f)] private float power = 1f;

        /// <summary>차를 찾을 때 쓰는 공용 버퍼. 상태 인스턴스마다 들면 손님 종류 수만큼 배열이 생기고,
        /// 탐색은 메인 스레드에서만 일어나므로 하나를 돌려 쓴다.</summary>
        private static readonly Collider[] SearchBuffer = new Collider[16];

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            Transform target = ResolveTarget();
            if (target == null)
            {
                // 때릴 게 없다고 방문을 세우지는 않는다. 다음 행동으로 넘긴다.
                Debug.LogWarning($"[{nameof(VandalizeState)}] {customer.name}이(가) 때릴 대상을 찾지 못했습니다.", customer);
                return VisitOutcome.Blocked;
            }

            Vector3 approach = GetApproachPoint(target, customer.transform.position);

            VisitOutcome moved = await MoveAndWait(approach, moveTimeout, ct);
            if (moved == VisitOutcome.Blocked)
            {
                return moved;
            }

            // 대상이 그 사이 사라졌을 수 있다. 반납된 차를 때리려 들면 여기서 걸러진다.
            if (target == null)
            {
                return VisitOutcome.Blocked;
            }

            // 걷기를 접지 않으면 이동 모듈이 타격 중에도 방향을 붙들고 있다.
            customer.Mover?.Stop();
            await FaceTowards(target.position, turnSpeed, ct);

            IVandalTarget victim = target.GetComponentInParent<IVandalTarget>();

            try
            {
                for (int i = 0; i < hitCount; i++)
                {
                    if (target == null)
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
                    if (victim != null && victim.CanTakeHit)
                    {
                        Vector3 point = target.position;
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
            Vector3 toCustomer = from - target.position;
            toCustomer.y = 0f;

            if (toCustomer.sqrMagnitude < 0.0001f)
            {
                // 대상 바로 위에 서 있다. 방향을 정할 수 없으니 지금 자리를 그대로 쓴다.
                return from;
            }

            return target.position + toCustomer.normalized * standoff;
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

                    return Ctx.MapData.TryGetNearest(targetPoint, Ctx.Customer.transform.position, out MapPosition point)
                        ? point.transform
                        : null;
            }
        }

        /// <summary>주변에서 자기가 타고 온 차가 아닌 차를 하나 찾는다. 가장 가까운 차를 고르므로
        /// 옆자리에 선 차가 있으면 그쪽으로 간다.</summary>
        private Transform FindOtherCar()
        {
            AbstractCustomer customer = Ctx.Customer;
            Car ownCar = Ctx.Visit?.Car;

            int count = Physics.OverlapSphereNonAlloc(
                customer.transform.position, searchRadius, SearchBuffer, carLayers, QueryTriggerInteraction.Ignore);

            Car best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = SearchBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                Car car = hit.GetComponentInParent<Car>();

                // 자기가 타고 온 차를 때리면 방문 내내 제자리다. 남의 차만 고른다.
                if (car == null || car == ownCar)
                {
                    continue;
                }

                float distance = (car.transform.position - customer.transform.position).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = car;
            }

            if (best == null && count >= SearchBuffer.Length)
            {
                // 버퍼가 꽉 차면 그 너머의 차는 아예 보이지 않는다. 반경이 너무 넓다는 신호다.
                Debug.LogWarning(
                    $"[{nameof(VandalizeState)}] 주변 콜라이더가 {SearchBuffer.Length}개를 넘어 일부만 살펴봤습니다. " +
                    $"{nameof(searchRadius)}를 줄이거나 {nameof(carLayers)}를 좁히세요.", customer);
            }

            return best != null ? best.transform : null;
        }
    }
}
