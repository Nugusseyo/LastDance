using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Customers.Animation;
using _Works.CJW.Scripts.Customers.Movement;
using _Works.CJW.Scripts.MapSystems;
using DevLib.AnimatorSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>손님 행동 하나. [SerializeReference]로 프리팹에 직접 직렬화되어 손님마다 자기 인스턴스와 설정값을 갖는다. 클래스 이름/네임스페이스를 옮길 때는 [MovedFrom]을 붙여야 직렬화 참조가 끊기지 않는다.</summary>
    [Serializable]
    public abstract class CustomerState
    {
        [Tooltip("비워두면 항상 실행한다. 채우면 조건이 맞을 때만 실행하고, 맞지 않으면 건너뛰고 다음 행동으로 넘어간다.")]
        [SerializeReference] private StateCondition condition;

        protected CustomerContext Ctx { get; private set; }

        public void Bind(CustomerContext ctx)
        {
            Ctx = ctx;
        }

        /// <summary>지금 이 행동을 실행해도 되는지. 시퀀스를 도는 쪽이 매 실행 직전에 묻는다.</summary>
        public bool CanRun()
        {
            return condition == null || condition.IsMet(Ctx);
        }

        /// <summary>이 행동을 수행하고 어떻게 끝났는지 반환한다. 모든 대기에 <paramref name="ct"/>를 물려야 반납 후에도 태스크가 계속 도는 일이 없다.</summary>
        public abstract UniTask<VisitOutcome> Run(CancellationToken ct);

        /// <summary>방문 시작 시 호출. 인스턴스가 재사용되므로 진행값을 들고 있다면 여기서 되돌린다.</summary>
        public virtual void Reset() { }

        /// <summary>목적지로 이동하고 도착할 때까지 기다리는 공용 절차. 걷는 방법은 <see cref="IMover"/>가 알고,
        /// 여기서는 그 결과를 방문 흐름의 말로 옮기기만 한다.</summary>
        protected async UniTask<VisitOutcome> MoveAndWait(Vector3 destination, float timeout, CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            IMover mover = customer.Mover;

            if (mover == null)
            {
                // 여기서 멈추면 방문 전체가 굳는다. 못 걸어간 사실만 남기고 넘어간다.
                Debug.LogError(
                    $"[{GetType().Name}] {customer.name}에 이동 모듈이 없어 움직이지 못했습니다. " +
                    $"프리팹에 {nameof(IMover)} 모듈을 붙여야 합니다.", customer);
                return VisitOutcome.Blocked;
            }

            MoveResult result = await mover.MoveAndWait(destination, timeout, ct);

            return result switch
            {
                MoveResult.Done => VisitOutcome.Done,
                MoveResult.Timeout => VisitOutcome.Timeout,
                _ => VisitOutcome.Blocked
            };
        }

        /// <summary>경로 계산용 공용 버퍼. 상태 탐색은 메인 스레드에서만 일어나므로 하나를 돌려 쓴다.
        /// 필드 초기화로 만들면 직렬화 도중에 생성돼 Unity가 막으므로, 처음 쓸 때 만든다.</summary>
        private static NavMeshPath _reachPath;

        /// <summary>이 종류의 지점 중 지금 걸어서 끝까지 닿는 가장 가까운 곳(경로 길이 기준)을 찾는다.
        /// 세워 둔 차가 NavMesh를 깎아 줄지어 서면 벽이 되므로, 직선으로 가까운 지점이 그 벽 너머일 수 있다.</summary>
        protected bool TryGetReachablePoint(MapPointType type, out MapPosition point)
        {
            point = null;
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (Ctx.MapData == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return false;
            }

            _reachPath ??= new NavMeshPath();
            IReadOnlyList<MapPosition> points = Ctx.MapData.GetAll(type);
            float best = float.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                MapPosition candidate = points[i];
                if (candidate == null || !candidate.IsAvailable)
                {
                    continue;
                }

                if (!agent.CalculatePath(candidate.Position, _reachPath) || _reachPath.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float length = PathLength(_reachPath);
                if (length < best)
                {
                    best = length;
                    point = candidate;
                }
            }

            return point != null;
        }

        /// <summary>닿는 지점이 나올 때까지 <paramref name="wait"/>초 동안 다시 찾아본다. 벽을 이룬 차가 떠나면 길이 열린다.
        /// 끝내 없으면 직선으로 가장 가까운 지점을 돌려준다 — 가다 막히더라도 그쪽으로 향하는 게 제자리보다 낫다.</summary>
        protected async UniTask<MapPosition> WaitForReachablePoint(MapPointType type, float wait, CancellationToken ct)
        {
            const float retryInterval = 0.5f;
            float deadline = Time.time + wait;

            while (true)
            {
                if (TryGetReachablePoint(type, out MapPosition point))
                {
                    return point;
                }

                if (Time.time >= deadline)
                {
                    return Ctx.MapData != null && Ctx.MapData.TryGetNearest(type, Ctx.Customer.transform.position, out point)
                        ? point
                        : null;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(retryInterval), cancellationToken: ct);
            }
        }

        private static readonly MapPointType[] PointTypes = (MapPointType[])Enum.GetValues(typeof(MapPointType));

        /// <summary><paramref name="from"/>에서 걸어서 끝까지 닿는 맵 지점의 수. 주차 자리는 차가 서 있어 막히므로 세지 않는다.
        /// 어느 쪽에 내려야 갇히지 않는지 비교할 때 쓴다 — 벽 안쪽에 갇힌 자리는 이 값이 작다.</summary>
        protected int CountReachablePoints(Vector3 from)
        {
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (Ctx.MapData == null || agent == null)
            {
                return 0;
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 2f, filter))
            {
                return 0;
            }

            _reachPath ??= new NavMeshPath();
            int count = 0;

            foreach (MapPointType type in PointTypes)
            {
                if (type is MapPointType.None or MapPointType.ParkingSlot)
                {
                    continue;
                }

                IReadOnlyList<MapPosition> points = Ctx.MapData.GetAll(type);
                for (int i = 0; i < points.Count; i++)
                {
                    if (points[i] != null
                        && NavMesh.CalculatePath(start.position, points[i].Position, filter, _reachPath)
                        && _reachPath.status == NavMeshPathStatus.PathComplete)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static float PathLength(NavMeshPath path)
        {
            Vector3[] corners = path.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        /// <summary>연출 클립을 <paramref name="duration"/>초 동안 재생하는 공용 절차.
        /// 연출 모듈이 없어도 방문을 세우지 않는다 — 그 시간만큼 가만히 서 있고 문제만 남긴다.</summary>
        protected async UniTask PlayAction(HashDataSO clip, float duration, CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            IActionAnimator action = customer.ActionAnimator;

            if (action == null || clip == null)
            {
                if (action == null)
                {
                    Debug.LogWarning(
                        $"[{GetType().Name}] {customer.name}에 연출 모듈이 없어 그 자리에 서 있기만 합니다. " +
                        $"프리팹에 {nameof(ActionAnimatorModule)}을 붙여야 합니다.", customer);
                }

                if (duration > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
                }
                else
                {
                    // duration이 0이면 "취소될 때까지"라는 뜻이다. 모듈이 없다고 곧장 끝내 버리면
                    // 시퀀스가 의도보다 빨리 다음 행동으로 넘어가 버린다.
                    await UniTask.WaitUntilCanceled(ct);
                }

                return;
            }

            await action.PlayFor(clip, duration, ct);
        }

        /// <summary>목표 지점 쪽으로 몸을 돌리고 다 돌 때까지 기다린다. 때리거나 마주 서는 연출은
        /// 등을 돌린 채로는 말이 되지 않으므로, 연출을 틀기 전에 이걸 먼저 지난다.
        /// 걷는 중에는 이동 모듈이 방향을 쥐고 있으므로 멈춘 뒤에 불러야 한다.</summary>
        protected async UniTask FaceTowards(Vector3 position, float degreesPerSecond, CancellationToken ct)
        {
            Transform body = Ctx.Customer.transform;

            Vector3 direction = position - body.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                // 발밑에 있는 대상이다. 어느 쪽을 봐야 할지 정할 수 없으니 지금 방향을 그대로 둔다.
                return;
            }

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            float speed = Mathf.Max(degreesPerSecond, 1f);

            // 영영 못 돌아 굳는 일이 없도록 한 바퀴 돌 시간까지만 준다.
            float deadline = Time.time + 360f / speed + 0.5f;

            while (Quaternion.Angle(body.rotation, target) > 1f && Time.time < deadline)
            {
                body.rotation = Quaternion.RotateTowards(body.rotation, target, speed * Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}
