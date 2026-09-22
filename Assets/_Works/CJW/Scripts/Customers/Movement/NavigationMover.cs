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
                agent = owner.GetComponentInChildren<NavMeshAgent>(true);
            }

            // 애니메이터는 렌더러 모듈이 들고 있다. 모듈이 없는 프리팹만 계층에서 직접 찾는다.
            if (animator == null)
            {
                animator = _renderer?.Animator;
            }

            if (animator == null && owner != null)
            {
                animator = owner.GetComponentInChildren<Animator>(true);
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
                ResyncAgent();
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

            MoveTo(destination);

            // SetDestination 직후 같은 프레임에는 pathPending이 아직 false이고
            // remainingDistance가 0이라 IsArrived가 곧바로 true가 된다. 한 프레임 양보한다.
            await UniTask.NextFrame(ct);
            await UniTask.WaitWhile(() => IsReady && agent.pathPending, cancellationToken: ct);

            if (!IsReady || !agent.hasPath)
            {
                return MoveResult.Blocked;
            }

            float deadline = timeout > 0f ? Time.time + timeout : float.MaxValue;

            try
            {
                while (!IsArrived)
                {
                    // 걷는 도중에 태워졌거나 NavMesh 밖으로 밀려났다. 영영 도착하지 못하므로 여기서 끊는다.
                    if (!IsReady)
                    {
                        return MoveResult.Blocked;
                    }

                    if (Time.time > deadline)
                    {
                        return MoveResult.Timeout;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                return MoveResult.Done;
            }
            finally
            {
                // 취소로 끊겨도 여기는 반드시 지난다. 빼먹으면 손님이 선 자리에서 걷는 애니메이션을 계속 돌린다.
                Stop();
            }
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
        }

        /// <summary>몸통만 순간이동(풀에서 꺼내기, 하차)했을 때 Agent를 데려온다. 안 맞추면 Agent가 옛 자리를 기준으로 경로를 그려 엉뚱한 쪽으로 걷는다.</summary>
        private void ResyncAgent()
        {
            Vector3 body = Body.position;

            if ((agent.nextPosition - body).sqrMagnitude <= resyncDistance * resyncDistance)
            {
                return;
            }

            agent.Warp(body);
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
