using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Customers.Animation;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.JJH._02_Scripts.Agents.Modules;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>
    /// 걸어다니는 에이전트의 이동. NavMeshAgent는 경로만 계산하고, 몸통을 실제로 옮기는 일은
    /// 걷기 애니메이션의 루트 모션이 맡는다. 그래서 발이 미끄러지지 않고 애니메이션 속도가 곧 이동 속도가 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationMover : AbstractModule, IMover, IRootMotionReceiver, IUpdate
    {
        [Header("참조")]
        [Tooltip("비워두면 소유자 계층에서 찾아 쓴다.")]
        [SerializeField] private NavMeshAgent agent;

        [Tooltip("루트 모션을 만들 애니메이터. 비워두면 소유자 계층에서 찾아 쓴다.")]
        [SerializeField] private Animator animator;

        [Header("애니메이션")]
        [Tooltip("걸을 때 재생할 애니메이터 상태. 몸을 옮기는 건 이 클립의 루트 모션이므로 반드시 채워야 한다.")]
        [SerializeField] private HashDataSO walkClip;

        [Tooltip("멈춰 있을 때 재생할 애니메이터 상태.")]
        [SerializeField] private HashDataSO idleClip;

        [Tooltip("걷기와 서기를 섞는 시간(초).")]
        [SerializeField, Min(0f)] private float clipCrossFade = 0.15f;

        [Header("이동")]
        [Tooltip("루트 모션으로 움직인다. 끄면 NavMeshAgent가 예전처럼 몸통을 직접 옮긴다.")]
        [SerializeField] private bool useRootMotion = true;

        [Tooltip("켜면 애니메이션이 몸통의 방향까지 돌린다. 끄면 Agent가 가려는 쪽으로 이 모듈이 직접 돌린다.")]
        [SerializeField] private bool useRootRotation;

        [Tooltip("몸통을 돌리는 각속도(도/초). 0이면 Agent의 Angular Speed를 쓴다.")]
        [SerializeField, Min(0f)] private float turnSpeed;

        [Tooltip("하차 직후처럼 Agent가 아직 살아나지 않았을 때 기다려 주는 시간(초).")]
        [SerializeField, Min(0f)] private float readyGrace = 1f;

        [Tooltip("받은 경로를 잃었을 때 다시 요청해 보는 시간(초). 옆에 선 차가 NavMesh를 새로 깎으면 방금 받은 경로가 버려진다.")]
        [SerializeField, Min(0f)] private float pathRetry = 1.5f;

        [Tooltip("몸통과 Agent가 이만큼(m) 벌어지면 Agent를 몸통 자리로 옮긴다. 풀에서 꺼내 순간이동했을 때를 위한 안전장치.")]
        [SerializeField, Min(0.1f)] private float resyncDistance = 1f;

        /// <summary>루트 모션으로 들어온 이동 거리. 걷기 애니메이션이 없어 제자리에 서 있는 경우를 잡아내는 데만 쓴다.</summary>
        private float _movedSinceCheck;

        private float _stuckTime;
        private bool _stuckWarned;

        /// <summary>애니메이션을 재생해 주는 모듈. 이동 모듈이 Animator를 직접 조작하지 않도록 한 겹 둔다.</summary>
        private IRenderer _renderer;

        /// <summary>춤·주먹질 같은 연출을 트는 모듈. 없으면 null이고, 이 모듈은 예전처럼 걷기·서기만 튼다.</summary>
        private IActionAnimator _action;

        /// <summary>걸음걸이 변조. 없으면 null이고, 기본 걷기 클립을 흔들림 없이 쓴다.</summary>
        private IGait _gait;

        /// <summary>지금 이 모듈이 틀어 둔 상태 해시. 매 프레임 CrossFade를 다시 걸면 애니메이션이 첫 프레임에서 맴돈다.</summary>
        private int _playingClip;

        /// <summary>실제로 옮길 트랜스폼. 모듈이 자식에 달려 있어도 몸통이 움직여야 한다.</summary>
        private Transform Body => _owner != null ? _owner.transform : transform;

        /// <summary>지금 이동을 시킬 수 있는지. 탑승 중에는 탑승 모듈이 Agent를 꺼두므로 여기서 자연히 걸러진다.</summary>
        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh;

        /// <summary>목적지에 닿았는지. 경로가 아직 계산 중이면 도착으로 보지 않는다.</summary>
        public bool IsArrived =>
            IsReady &&
            !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _renderer = owner != null ? owner.GetModule<IRenderer>() : null;
            _action = owner != null ? owner.GetModule<IActionAnimator>() : null;
            _gait = owner != null ? owner.GetModule<IGait>() : null;

            if (agent == null && owner != null)
            {
                agent = owner.GetComponentInParent<NavMeshAgent>(true);
            }

            // 애니메이터는 렌더러 모듈이 들고 있다. 모듈이 없는 프리팹만 계층에서 직접 찾는다.
            if (animator == null)
            {
                animator = _renderer?.Animator;
            }

            if (animator == null && owner != null)
            {
                animator = owner.GetComponentInParent<Animator>(true);
            }

            if (agent == null)
            {
                Debug.LogError($"[{nameof(NavigationMover)}] {name}에 NavMeshAgent가 없어 이동하지 못합니다.", this);
                return;
            }

            if (!useRootMotion)
            {
                return;
            }

            if (animator == null)
            {
                // 여기서 루트 모션을 켜 두면 아무도 몸통을 옮기지 않아 손님이 제자리에 얼어붙는다.
                Debug.LogError($"[{nameof(NavigationMover)}] {name}에 Animator가 없어 루트 모션 대신 Agent 이동으로 돌립니다.", this);
                useRootMotion = false;
                return;
            }

            animator.applyRootMotion = true;

            // Agent는 경로와 회피만 계산하고 트랜스폼은 건드리지 않는다. 방향도 이 모듈이 돌린다.
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.nextPosition = Body.position;

            // OnAnimatorMove는 Animator와 같은 GameObject에서만 불린다. 손님은 Animator가 visual 자식에 있다.
            RootMotionRelay relay = animator.GetComponent<RootMotionRelay>();
            if (relay == null)
            {
                relay = animator.gameObject.AddComponent<RootMotionRelay>();
            }

            relay.Bind(this);
        }

        public void OnUpdate(float dt)
        {
            if (!IsReady)
            {
                // 탑승 중이거나 NavMesh 밖이다. 이때 애니메이션은 탑승 모듈 같은 다른 쪽이 틀고 있으므로
                // 여기서 덮어쓰지 않는다. 다만 다시 걸을 때 제 상태를 새로 틀도록 기억은 지운다.
                _playingClip = 0;
                return;
            }

            if (useRootMotion)
            {
                ResyncAgent(dt);
            }

            Vector3 desired = agent.desiredVelocity;
            desired.y = 0f;

            float speed = agent.hasPath && !IsArrived ? desired.magnitude : 0f;

            if (_action != null && _action.IsPlaying)
            {
                // 연출이 화면을 잡고 있다. 여기서 걷기·서기를 덮어쓰면 둘이 매 프레임 서로를 밀어내 손님이 부들거린다.
                // 연출이 끝나는 순간 제 클립을 새로 틀도록 기억만 지운다.
                _playingClip = 0;
            }
            else
            {
                PlayClip(speed > 0.01f ? WalkingClip : idleClip);
            }

            if (useRootMotion && !useRootRotation && speed > 0.01f)
            {
                Face(ApplySway(desired), dt);
            }

            DetectMissingRootMotion(speed, dt);
        }

        /// <summary>목적지만 찍어 둔다. 도착까지 기다리려면 <see cref="MoveAndWait"/>를 쓴다.</summary>
        public void MoveTo(Vector3 destination)
        {
            if (!IsReady)
            {
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        /// <summary>이동을 접는다. 경로를 비우고 걷기 애니메이션도 바로 내린다.</summary>
        public void Stop()
        {
            // 플레이 종료처럼 오브젝트가 먼저 파괴되고 취소가 뒤늦게 도착하면 여기로 들어온다. 건드릴 게 없다.
            if (this == null)
            {
                return;
            }

            if (IsReady && agent.hasPath)
            {
                agent.ResetPath();
            }

            _movedSinceCheck = 0f;
            _stuckTime = 0f;

            PlayClip(idleClip);
        }

        /// <summary>목적지로 이동하고 도착할 때까지 기다린다. 모든 대기에 <paramref name="ct"/>를 물려야 반납 후에도 태스크가 계속 도는 일이 없다.</summary>
        public async UniTask<MoveResult> MoveAndWait(Vector3 destination, float timeout, CancellationToken ct)
        {
            // 하차 직후에는 탑승 모듈이 Agent를 다시 켜는 데 몇 프레임이 걸릴 수 있다.
            // 곧바로 Blocked로 빠지지 않도록 잠깐 기다려 준다.
            float ready = Time.time + readyGrace;
            while (!IsReady)
            {
                if (Time.time > ready)
                {
                    return MoveResult.Blocked;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 이미 목적지에 서 있다. 경로가 없는 게 정상이므로 막힌 것으로 보지 않는다.
            if (IsNear(destination))
            {
                return MoveResult.Done;
            }

            // 옆에 선 차가 막 NavMesh를 깎으면 방금 받은 경로가 버려지고 목적지가 제자리로 돌아간다.
            // 한 번 실패로 포기하지 않고 잠깐 다시 요청한다.
            float retryUntil = Time.time + pathRetry;
            int escapes = 0;
            while (true)
            {
                MoveTo(destination);

                // SetDestination 직후 같은 프레임에는 pathPending이 아직 false이고
                // remainingDistance가 0이라 IsArrived가 곧바로 true가 된다. 한 프레임 양보한다.
                await UniTask.NextFrame(ct);
                await UniTask.WaitWhile(() => IsReady && agent.pathPending, cancellationToken: ct);

                if (IsReady && agent.hasPath)
                {
                    break;
                }

                // 목적지가 NavMesh 밖이고 지금 선 곳이 거기에 가장 가까운 자리다(좌석처럼 차 안에 있는 목적지).
                // 다만 목적지에서 멀리 떨어진 "끝"이라면 차와 벽에 둘러싸인 섬에 갇힌 것이니 빠져나갈 자리로 옮겨 다시 걷는다.
                if (IsReady && IsAtReachableEnd(destination))
                {
                    if (TryEscape(destination, ref escapes))
                    {
                        continue;
                    }

                    return MoveResult.Done;
                }

                if (!IsReady || Time.time > retryUntil)
                {
                    if (IsReady && TryEscape(destination, ref escapes))
                    {
                        retryUntil = Time.time + pathRetry;
                        continue;
                    }

                    return MoveResult.Blocked;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(PathRetryInterval), cancellationToken: ct);
            }

            float deadline = timeout > 0f ? Time.time + timeout : float.MaxValue;

            float nextRetry = 0f;

            // 목적지 코앞에서 더 못 다가가는지 보는 기록. 차 옆 문처럼 목적지가 장애물에 붙어 있으면
            // 장애물 회피에 밀려 도착 기준 바로 바깥에서 제자리걸음을 한다.
            float closest = float.PositiveInfinity;
            float lastProgress = Time.time;

            // 몸이 실제로 움직이는지 보는 기록. 걷는 도중 옆자리에 차가 들어와 길을 막으면 제자리걸음만 한다.
            Vector3 lastMovedAt = Body.position;
            float lastMoveTime = Time.time;

            try
            {
                while (true)
                {
                    // 걷는 도중에 태워졌거나 NavMesh 밖으로 밀려났다. 영영 도착하지 못하므로 여기서 끊는다.
                    if (!IsReady)
                    {
                        return MoveResult.Blocked;
                    }

                    // 경로 상태와 상관없이 몸이 실제로 가까워지는지 본다. 코앞에서 밀려나 경로가 지워졌다 다시 생기기를
                    // 되풀이하면 remainingDistance로는 잡히지 않는다.
                    float distance = PlanarDistance(Body.position, destination);
                    if (distance < closest - StallProgress)
                    {
                        closest = distance;
                        lastProgress = Time.time;
                    }
                    else if (distance <= StallReach && Time.time - lastProgress >= StallSeconds)
                    {
                        return MoveResult.Done;
                    }

                    if (PlanarDistance(Body.position, lastMovedAt) > TrappedMove)
                    {
                        lastMovedAt = Body.position;
                        lastMoveTime = Time.time;
                    }
                    else if (Time.time - lastMoveTime >= TrappedSeconds)
                    {
                        // 한동안 제자리다. 끝까지 닿는 길이 없으면 갇힌 것이니 빠져나간다. 길이 있으면 회피로 잠깐 막힌 것이라 둔다.
                        lastMoveTime = Time.time;
                        if (TryEscape(destination, ref escapes, stalled: true))
                        {
                            lastMovedAt = Body.position;
                            closest = float.PositiveInfinity;
                            lastProgress = Time.time;
                        }
                    }

                    if (agent.pathPending)
                    {
                        // 다시 요청한 경로를 계산하는 중이다.
                    }
                    else if (agent.hasPath)
                    {
                        // 부분 경로면 목적지 대신 경로 끝이 갈 수 있는 끝이다. 차 옆 문처럼 목적지가 NavMesh 구멍 가장자리에 있으면
                        // 도착 기준(stoppingDistance)보다 조금 떨어진 곳에서 더 못 가므로, 몸 반경만큼은 봐준다.
                        if (IsArrived)
                        {
                            return MoveResult.Done;
                        }

                        if (agent.pathStatus == NavMeshPathStatus.PathPartial && agent.remainingDistance <= agent.stoppingDistance + agent.radius)
                        {
                            if (!TryEscape(destination, ref escapes))
                            {
                                return MoveResult.Done;
                            }

                            lastMovedAt = Body.position;
                            lastMoveTime = Time.time;
                        }

                    }
                    else if (IsNear(destination))
                    {
                        return MoveResult.Done;
                    }
                    else if (Time.time >= nextRetry)
                    {
                        // 목적지가 NavMesh 밖이라 지금 선 곳이 갈 수 있는 끝이면 도착이다.
                        if (IsAtReachableEnd(destination))
                        {
                            if (!TryEscape(destination, ref escapes))
                            {
                                return MoveResult.Done;
                            }

                            lastMovedAt = Body.position;
                            lastMoveTime = Time.time;
                            continue;
                        }

                        // 경로를 잃으면 remainingDistance가 0이 되어 도착한 것처럼 보인다.
                        // 차가 서거나 떠나며 NavMesh를 다시 깎을 때마다 생기므로, 도착으로 치지 않고 다시 요청한다.
                        nextRetry = Time.time + PathRetryInterval;
                        MoveTo(destination);
                    }

                    if (Time.time > deadline)
                    {
                        return MoveResult.Timeout;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                // 취소로 끊겨도 여기는 반드시 지난다. 빼먹으면 손님이 선 자리에서 걷는 애니메이션을 계속 돌린다.
                Stop();
            }
        }

        private const float PathRetryInterval = 0.2f;

        /// <summary>이 시간(초) 동안 <see cref="TrappedMove"/>(m)만큼도 못 움직이면 갇혔는지 확인한다.</summary>
        private const float TrappedSeconds = 1.5f;
        private const float TrappedMove = 0.3f;

        /// <summary>목적지에서 이만큼(m) 넘게 떨어진 곳이 "갈 수 있는 끝"이면 목적지가 NavMesh 밖이라서가 아니라 갇힌 것이다.</summary>
        private const float EscapeFar = 2.5f;

        /// <summary>한 번 걷는 동안 빠져나가기를 시도하는 최대 횟수. 옮긴 곳도 막히면 끝없이 순간이동하지 않도록 한다.</summary>
        private const int MaxEscapes = 2;

        /// <summary>빠져나갈 자리를 찾는 반경(m)과 둘레 간격(m), 한 둘레에서 볼 방향 수.</summary>
        private const float EscapeSearchRadius = 8f;
        private const float EscapeRingStep = 1f;
        private const int EscapeDirections = 16;

        /// <summary>끝까지 닿는 자리가 없을 때, 목적지에 이만큼(m)은 더 다가갈 수 있어야 옮긴다.</summary>
        private const float EscapeMinGain = 2f;

        private NavMeshPath _escapePath;

        /// <summary>목적지까지 끝까지 닿는 길이 없고 목적지에서 멀리 떨어져 있으면(= 갇혔으면), 몸 둘레를 가까운 곳부터 훑어
        /// 목적지까지 끝까지 닿는 가장 가까운 자리로 몸과 Agent를 옮기고 다시 걷게 한다. 끝까지 닿는 자리가 없으면
        /// 지금보다 목적지에 확실히 더 다가갈 수 있는 자리라도 고른다. 옮겼으면 true.
        /// 걷는 도중 옆자리에 차가 들어와 NavMesh를 깎으면 지나던 틈이 닫히거나, 몸이 새 차 안에 파묻혀 이렇게 갇힌다.
        /// <paramref name="stalled"/>는 한동안 제자리였다는 뜻이다. 이때는 Agent 혼자 구멍 밖으로 밀려나 "길이 있다"고
        /// 답하는 경우가 있어, Agent가 실제로 끝까지 가는 경로를 들고 몸과 붙어 있을 때만 갇히지 않은 것으로 본다.</summary>
        private bool TryEscape(Vector3 destination, ref int escapes, bool stalled = false)
        {
            if (escapes >= MaxEscapes || !IsReady || PlanarDistance(Body.position, destination) <= EscapeFar)
            {
                return false;
            }

            _probePath ??= new NavMeshPath();
            bool hasCompleteRoute = agent.CalculatePath(destination, _probePath) && _probePath.status == NavMeshPathStatus.PathComplete;
            if (stalled)
            {
                bool walkingFine = agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete
                                   && PlanarDistance(agent.nextPosition, Body.position) < 0.5f;
                if (walkingFine)
                {
                    return false;
                }
            }
            else if (hasCompleteRoute)
            {
                return false;
            }

            // 지금 자리에서 갈 수 있는 끝이 목적지에서 얼마나 먼지. 끝까지 닿는 자리가 없을 때 이보다 나은지를 가른다.
            float currentGap = hasCompleteRoute ? 0f : PlanarDistance(Body.position, destination);
            if (!hasCompleteRoute && _probePath.corners.Length > 0)
            {
                currentGap = PlanarDistance(_probePath.corners[_probePath.corners.Length - 1], destination);
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            NavMesh.SamplePosition(destination, out NavMeshHit goal, 2f, filter);
            Vector3 target = goal.hit ? goal.position : destination;

            _escapePath ??= new NavMeshPath();
            // 몸의 기준점은 NavMesh 바닥보다 떠 있다(Agent의 baseOffset). 그 높이로 둘레를 찍으면 바닥에 붙지 않으니 바닥 높이로 내린다.
            Vector3 origin = Body.position;
            origin.y = NavMesh.SamplePosition(origin, out NavMeshHit ground, 3f, filter) ? ground.position.y : origin.y - agent.baseOffset;
            Vector3 fallback = default;
            float fallbackGap = currentGap - EscapeMinGain;

            // 가까운 둘레부터 본다. 한 둘레에서 끝까지 닿는 자리를 찾으면 그중 목적지까지 가장 짧은 자리로 옮기고 멈춘다.
            for (float r = EscapeRingStep; r <= EscapeSearchRadius; r += EscapeRingStep)
            {
                Vector3 best = default;
                float bestLength = float.MaxValue;

                for (int i = 0; i < EscapeDirections; i++)
                {
                    float angle = i * Mathf.PI * 2f / EscapeDirections;
                    Vector3 probe = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                    if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, EscapeRingStep * 0.6f, filter)
                        || !NavMesh.CalculatePath(hit.position, target, filter, _escapePath)
                        || _escapePath.corners.Length == 0)
                    {
                        continue;
                    }

                    if (_escapePath.status == NavMeshPathStatus.PathComplete)
                    {
                        float length = PathLength(_escapePath);
                        if (length < bestLength)
                        {
                            bestLength = length;
                            best = hit.position;
                        }

                        continue;
                    }

                    float gap = PlanarDistance(_escapePath.corners[_escapePath.corners.Length - 1], destination);
                    if (gap < fallbackGap)
                    {
                        fallbackGap = gap;
                        fallback = hit.position;
                    }
                }

                if (bestLength < float.MaxValue)
                {
                    return EscapeTo(best, destination, ref escapes);
                }
            }

            if (fallbackGap < currentGap - EscapeMinGain)
            {
                return EscapeTo(fallback, destination, ref escapes);
            }

            Debug.LogWarning($"[{nameof(NavigationMover)}] {Body.name}이(가) 갇혔는데 {EscapeSearchRadius}m 안에 빠져나갈 자리가 없습니다.", this);
            escapes = MaxEscapes;
            return false;
        }

        /// <summary>빠져나갈 자리로 몸과 Agent를 함께 옮기고 다시 걷게 한다. 하나만 옮기면 서로 맞추느라 되돌아간다.</summary>
        private bool EscapeTo(Vector3 position, Vector3 destination, ref int escapes)
        {
            escapes++;
            Body.position = position;
            agent.Warp(position);
            MoveTo(destination);
            Debug.Log($"[{nameof(NavigationMover)}] {Body.name}이(가) 걷다가 갇혀 ({position.x:F1},{position.z:F1})로 빠져나와 다시 걷습니다.");
            return true;
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

        /// <summary>목적지에서 이만큼(m) 안쪽인데 <see cref="StallSeconds"/> 동안 <see cref="StallProgress"/>만큼도 가까워지지 않으면 도착으로 본다.</summary>
        private const float StallReach = 1f;
        private const float StallSeconds = 1f;
        private const float StallProgress = 0.05f;

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private NavMeshPath _probePath;

        /// <summary>목적지까지 끝까지 닿는 길은 없고, 닿을 수 있는 가장 가까운 자리가 바로 지금 선 곳인지.
        /// 경로가 잠깐 끊긴 것과 갈 수 있는 끝에 도착한 것을 가른다 — 앞의 경우는 계산하면 길이 다시 나온다.</summary>
        private bool IsAtReachableEnd(Vector3 destination)
        {
            _probePath ??= new NavMeshPath();
            if (!agent.CalculatePath(destination, _probePath) || _probePath.status == NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            Vector3[] corners = _probePath.corners;
            if (corners.Length == 0)
            {
                return false;
            }

            Vector3 delta = corners[corners.Length - 1] - agent.nextPosition;
            delta.y = 0f;
            float reach = agent.stoppingDistance + agent.radius;
            return delta.sqrMagnitude <= reach * reach;
        }

        /// <summary>목적지에 이미 닿았다고 볼 만큼 가까운지. 높이는 무시한다 — 지점이 바닥보다 떠 있어도 같은 자리로 본다.</summary>
        private bool IsNear(Vector3 destination)
        {
            Vector3 delta = destination - agent.nextPosition;
            delta.y = 0f;
            float reach = agent.stoppingDistance + agent.radius;
            return delta.sqrMagnitude <= reach * reach;
        }

        /// <summary>애니메이터와 같은 GameObject에 붙은 <see cref="RootMotionRelay"/>가 OnAnimatorMove에서 불러준다.</summary>
        public void ApplyRootMotion()
        {
            if (!useRootMotion || animator == null)
            {
                return;
            }

            // 탑승 중에는 Agent가 꺼져 있다. 여기서 막지 않으면 앉기 애니메이션의 루트 모션이 손님을 좌석 밖으로 끌고 간다.
            if (!IsReady)
            {
                return;
            }

            // 춤 같은 연출 클립은 제자리에서 보여야 한다. 루트 모션을 받으면 한 동작마다 손님이 조금씩 밀린다.
            // 싸움처럼 몸이 실려야 하는 연출은 루트 모션을 켜고 틀므로 받는다.
            if (_action != null && _action.IsPlaying && !_action.UsesRootMotion)
            {
                return;
            }

            Vector3 delta = animator.deltaPosition;
            _movedSinceCheck += delta.magnitude;

            Transform body = Body;
            Vector3 next = body.position + delta;

            // 높이는 Agent가 잡아 준다. 애니메이션의 위아래 흔들림까지 더하면 경사에서 바닥을 뚫고 내려간다.
            next.y = agent.nextPosition.y;
            body.position = next;

            if (useRootRotation)
            {
                body.rotation *= animator.deltaRotation;
            }

            // Agent를 몸통에 붙여 둬야 남은 거리와 다음 코너를 실제 위치 기준으로 계산한다.
            agent.nextPosition = body.position;

            // Agent는 NavMesh 밖으로 나가지 못해 가장자리에 걸린다. 몸도 그 자리로 되돌린다. 안 그러면 애니메이션이 모는 몸만
            // 세워 둔 차가 파낸 구멍 안으로 계속 걸어 들어가, Resync가 잡아 주는 1m까지 차를 뚫고 서 있게 된다.
            Vector3 constrained = agent.nextPosition;
            body.position = new Vector3(constrained.x, body.position.y, constrained.z);
        }

        /// <summary>몸과 Agent가 어긋났을 때 다시 맞춘다. 어긋나는 길은 둘이다.
        /// ① 몸만 순간이동했다(풀에서 꺼내기, 하차). 이때는 Agent를 몸으로 데려온다. 안 맞추면 옛 자리 기준으로 경로를 그린다.
        /// ② Agent가 밀려났다. 서 있는 손님끼리 겹치면 회피가 Agent만 밀어내고, 루트 모션인 몸은 따라가지 않는다.
        /// 이때 ①처럼 Warp하면 회피가 다시 밀고 Warp가 다시 당기며, Warp할 때마다 경로가 지워져 길이 있는데도 출발하지 못한다.
        /// 그래서 몸이 직전 프레임에서 크게 움직였을 때만 ①로 보고, 아니면 몸을 Agent 쪽으로 부드럽게 옮긴다.</summary>
        private void ResyncAgent(float dt)
        {
            Vector3 body = Body.position;
            // 처음 보는 프레임도 순간이동으로 친다. 풀에서 막 꺼낸 몸이 옛 Agent 자리로 미끄러져 가면 안 된다.
            bool teleported = !_hasLastBody || (body - _lastBody).sqrMagnitude > resyncDistance * resyncDistance;
            _hasLastBody = true;

            if ((agent.nextPosition - body).sqrMagnitude > resyncDistance * resyncDistance)
            {
                // 몸이 차가 파낸 구멍 안이면 Warp해도 Agent는 가장자리로 되돌아간다. 그때도 몸을 Agent 쪽으로 꺼낸다.
                if (teleported && IsOnWalkableGround(body))
                {
                    agent.Warp(body);
                }
                else
                {
                    Vector3 target = agent.nextPosition;
                    Body.position = teleported ? target : Vector3.MoveTowards(body, target, FollowAgentSpeed * dt);
                }
            }

            _lastBody = Body.position;
        }

        /// <summary>밀려난 Agent를 몸이 따라가는 속도(m/s). 걷는 속도쯤이라 미끄러지듯 비켜서는 것처럼 보인다.</summary>
        private const float FollowAgentSpeed = 1.5f;

        private Vector3 _lastBody;
        private bool _hasLastBody;

        /// <summary>몸 위치 바로 아래에 이 Agent가 설 NavMesh가 있는지. 몸은 baseOffset만큼 떠 있어 수평 거리로만 판단한다.</summary>
        private bool IsOnWalkableGround(Vector3 body)
        {
            const float tolerance = 0.3f;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(body, out NavMeshHit hit, agent.baseOffset + tolerance, filter))
            {
                return false;
            }

            Vector3 delta = hit.position - body;
            delta.y = 0f;
            return delta.sqrMagnitude <= tolerance * tolerance;
        }

        /// <summary>애니메이터 상태를 해시로 튼다. 이미 그 상태면 아무것도 하지 않는다.</summary>
        private void PlayClip(HashDataSO clip)
        {
            if (clip == null || clip.HashValue == 0 || _playingClip == clip.HashValue)
            {
                return;
            }

            _playingClip = clip.HashValue;

            if (_renderer != null)
            {
                _renderer.PlayClip(clip.HashValue, 0f, clipCrossFade);
                return;
            }

            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.CrossFadeInFixedTime(clip.HashValue, clipCrossFade, 0, 0f);
            }
        }

        /// <summary>지금 걸을 때 쓸 클립. 걸음걸이 모듈이 켜져 있으면 그쪽 클립이 이긴다.</summary>
        private HashDataSO WalkingClip
        {
            get
            {
                if (_gait != null && _gait.IsActive && _gait.WalkClip != null)
                {
                    return _gait.WalkClip;
                }

                return walkClip;
            }
        }

        /// <summary>가려는 방향을 걸음걸이만큼 비껴 준다. 목적지는 그대로라 비틀거려도 결국 도착한다.
        /// 몸통 회전을 애니메이션에 맡긴 경우(useRootRotation)에는 이 모듈이 방향을 쥐지 않으므로 흔들림도 걸리지 않는다.</summary>
        private Vector3 ApplySway(Vector3 direction)
        {
            if (_gait == null || !_gait.IsActive)
            {
                return direction;
            }

            float angle = _gait.SwayAngle;

            return Mathf.Approximately(angle, 0f)
                ? direction
                : Quaternion.Euler(0f, angle, 0f) * direction;
        }

        private void Face(Vector3 direction, float dt)
        {
            float degrees = turnSpeed > 0f ? turnSpeed : agent.angularSpeed;
            Transform body = Body;

            body.rotation = Quaternion.RotateTowards(
                body.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                degrees * dt);
        }

        /// <summary>갈 길은 있는데 루트 모션이 한 톨도 안 들어오는 상태를 잡아낸다. 증상이 "손님이 제자리에 얼어붙음"으로만 보여서 반드시 남긴다.</summary>
        private void DetectMissingRootMotion(float desiredSpeed, float dt)
        {
            if (!useRootMotion || _stuckWarned)
            {
                return;
            }

            if (desiredSpeed <= 0.01f || _movedSinceCheck > 0.001f)
            {
                _movedSinceCheck = 0f;
                _stuckTime = 0f;
                return;
            }

            _stuckTime += dt;
            if (_stuckTime < 2f)
            {
                return;
            }

            _stuckWarned = true;
            Debug.LogWarning(
                $"[{nameof(NavigationMover)}] {Body.name}이(가) 갈 길은 있는데 2초째 제자리입니다. " +
                $"{nameof(walkClip)}에 루트 모션이 있는 걷기 상태를 꽂았는지, " +
                "그 클립의 Root Transform Position(XZ)에서 Bake Into Pose가 꺼져 있는지 확인하세요. " +
                $"걷기 애니메이션이 아직 없다면 {nameof(useRootMotion)}을 꺼서 Agent가 직접 옮기게 할 수 있습니다.", this);
        }
    }
}
