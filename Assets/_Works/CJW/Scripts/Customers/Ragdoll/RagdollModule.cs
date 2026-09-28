using System;
using System.Collections.Generic;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Ragdoll
{
    /// <summary>휴머노이드 visual을 래그돌로 쓰러뜨린다. 뼈의 강체·콜라이더·관절은 프리팹에 미리 구워 둔 것을 쓴다
    /// (Tools/JW/Customers/Bake Customer Ragdoll). 평소에는 강체가 kinematic이고 콜라이더가 꺼져 있어 이동·길찾기·차 감지에 끼어들지 않고,
    /// 쓰러질 때 kinematic과 콜라이더만 뒤집는다. 관절을 쓰러지는 순간에 새로 만들면 걷던 자세가 기준이 돼 관절이 뼈를 잡아당기며
    /// 메시가 찢어졌다 — 미리 만든 관절은 기준 자세가 늘 같아 그런 일이 없다.</summary>
    public class RagdollModule : AbstractModule, IRagdoll
    {
        [Header("몸 (굽기 메뉴가 씀)")]
        [Tooltip("몸 전체 질량(kg). 굽기 메뉴가 뼈마다 나눠 준다. 바꾸면 다시 구워야 한다.")]
        [SerializeField, Min(1f)] private float totalMass = 60f;

        [Tooltip("팔다리 굵기 = 뼈 길이 × 이 값. 바꾸면 다시 구워야 한다.")]
        [SerializeField, Range(0.05f, 0.5f)] private float limbThickness = 0.22f;

        [Header("쓰러짐")]
        [Tooltip("쓰러진 뒤 이 시간(초)이 지나면 스스로 일어선다. 0이면 누가 Recover를 부를 때까지 누워 있다.")]
        [SerializeField, Min(0f)] private float recoverAfter = 4f;

        [Tooltip("일어설 때 본체를 NavMesh 위로 붙일 수 있는 거리(m).")]
        [SerializeField, Min(0.5f)] private float recoverSnapRadius = 3f;

        [Tooltip("가까이에 NavMesh가 없을 때 다시 찾아볼 거리(m). 여기서도 없으면 쓰러지기 시작한 자리에서 일어선다.")]
        [SerializeField, Min(0.5f)] private float recoverFarRadius = 15f;

        [Header("안전장치")]
        [Tooltip("뼈 하나가 낼 수 있는 최대 속도(m/s). 차는 Rigidbody 없이 움직여, 차체가 몸을 파고들면 물리가 몸을 터무니없이 빠르게 밀어낸다.")]
        [SerializeField, Min(1f)] private float maxBoneSpeed = 14f;

        [Tooltip("파고든 콜라이더에서 밀려날 때의 최대 속도(m/s).")]
        [SerializeField, Min(0.1f)] private float maxDepenetration = 2f;

        [Tooltip("쓰러지는 순간 몸을 이만큼(m) 들어 올린다. 발 콜라이더가 두께 없는 바닥 아래로 살짝 겹친 채 켜지면 물리가 아래로 밀어내 바닥을 뚫는다.")]
        [SerializeField, Min(0f)] private float liftOnActivate = 0.25f;

        [Tooltip("쓰러진 자리보다 이만큼(m) 아래로 떨어지면 바닥을 뚫은 것으로 보고 곧바로 일으킨다.")]
        [SerializeField, Min(0.5f)] private float fallThroughDepth = 3f;

        public float TotalMass => totalMass;

        public float LimbThickness => limbThickness;

        public bool IsActive { get; private set; }


        public event Action Recovered;

        private readonly List<Rigidbody> _bodies = new();
        private readonly List<Collider> _colliders = new();

        private Animator _animator;
        private Transform _hips;
        private NavMeshAgent _agent;
        private Collider _rootCollider;
        private float _recoverAt;
        private bool _built;

        /// <summary>쓰러지기 시작한 자리. 몸이 NavMesh 밖 멀리 떨어지면 여기서 일어선다.</summary>
        private Vector3 _knockedFrom;

        /// <summary>이번에 바닥을 뚫었는지. 그럴 땐 떨어진 곳이 아니라 쓰러진 자리에서 일어선다.</summary>
        private bool _fellThrough;

        /// <summary>저절로 일어서지 않는지. 죽은 몸이 시간이 지나 벌떡 일어나면 안 된다.</summary>
        private bool _stayDown;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _animator = owner.GetComponentInChildren<Animator>(true);
            _agent = owner.GetComponent<NavMeshAgent>();
            _rootCollider = owner.GetComponent<Collider>();

            if (_animator == null || !_animator.isHuman)
            {
                Debug.LogError($"[{nameof(RagdollModule)}] {owner.name}에 휴머노이드 Animator가 없어 래그돌을 만들지 못합니다.", this);
                return;
            }

            Collect();
        }

        // 물리가 한 번 튀면 다음 스텝에서 되돌릴 길이 없다. 매 물리 스텝마다 속도를 묶어 둔다.
        private void FixedUpdate()
        {
            if (!IsActive)
            {
                return;
            }

            float max = maxBoneSpeed * maxBoneSpeed;
            for (int i = 0; i < _bodies.Count; i++)
            {
                Rigidbody body = _bodies[i];
                if (body.linearVelocity.sqrMagnitude > max)
                {
                    body.linearVelocity = body.linearVelocity.normalized * maxBoneSpeed;
                }
            }
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            // 바닥을 뚫고 떨어지고 있다. 기다려 봐야 더 멀어질 뿐이니 쓰러진 자리에서 바로 일으킨다.
            if (!_stayDown && _hips.position.y < _knockedFrom.y - fallThroughDepth)
            {
                Debug.LogWarning($"[{nameof(RagdollModule)}] {_owner.name}의 몸이 바닥 아래로 떨어져 쓰러진 자리에서 일으킵니다.", this);
                _fellThrough = true;
                Recover();
                return;
            }

            if (!_stayDown && recoverAfter > 0f && Time.time >= _recoverAt)
            {
                Recover();
            }
        }

        // 풀로 돌아가며 꺼질 때 쓰러진 채로 남으면 다음 손님이 누운 채 나온다.
        private void OnDisable()
        {
            if (IsActive)
            {
                SetPhysics(false);
                IsActive = false;
            }

            _stayDown = false;
        }

        public void Activate(Vector3 launchVelocity, bool stayDown = false)
        {
            if (!Enable(stayDown))
            {
                return;
            }

            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].linearVelocity += launchVelocity;
            }
        }

        public void ActivateAt(Vector3 hitPoint, Vector3 impulse, bool stayDown = false)
        {
            if (!Enable(stayDown))
            {
                return;
            }

            Rigidbody target = ClosestBody(hitPoint);
            if (target != null)
            {
                target.AddForce(impulse, ForceMode.Impulse);
            }
        }

        private Rigidbody ClosestBody(Vector3 point)
        {
            Rigidbody closest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _bodies.Count; i++)
            {
                float distance = (_bodies[i].position - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    closest = _bodies[i];
                }
            }

            return closest;
        }

        /// <summary>물리로 넘긴다. 이미 쓰러져 있으면 일어설 시간만 미룬다. 구운 래그돌이 없으면 false.</summary>
        private bool Enable(bool stayDown)
        {
            if (!_built)
            {
                return false;
            }

            // 한 번 눕힌 몸은 다시 맞아도 그대로 누워 있어야 한다. 켜기만 하고 끄는 건 Recover가 한다.
            _stayDown |= stayDown;

            if (!IsActive)
            {
                IsActive = true;
                _knockedFrom = _owner.transform.position;

                // 길찾기와 애니메이션이 몸을 붙잡지 않게 먼저 끈다. 켜 둔 채 물리를 켜면 매 프레임 포즈를 되돌린다.
                if (_agent != null)
                {
                    _agent.enabled = false;
                }

                if (_rootCollider != null)
                {
                    _rootCollider.enabled = false;
                }

                _animator.enabled = false;
                _fellThrough = false;

                // 물리를 켜기 전에 들어 올린다. 켠 뒤에 옮기면 관절이 순간이동으로 늘어나 튄다.
                _hips.position += Vector3.up * liftOnActivate;

                // 꺼져 있던 동안 애니메이션이 뼈를 옮겨도 강체의 물리 위치는 따라오지 않는다. 그대로 켜면
                // 강체가 만들어졌던 자리(풀에서 처음 생긴 곳)로 순간이동한다. 지금 뼈 자리로 먼저 맞춘다.
                Physics.SyncTransforms();
                for (int i = 0; i < _bodies.Count; i++)
                {
                    Transform bone = _bodies[i].transform;
                    _bodies[i].position = bone.position;
                    _bodies[i].rotation = bone.rotation;
                }

                SetPhysics(true);
            }

            _recoverAt = Time.time + recoverAfter;
            return true;
        }

        private readonly List<Collider> _ignored = new();

        public void IgnoreWhileDown(Collider[] colliders)
        {
            if (!IsActive || colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider other = colliders[i];
                if (other == null || _ignored.Contains(other))
                {
                    continue;
                }

                _ignored.Add(other);
                for (int j = 0; j < _colliders.Count; j++)
                {
                    Physics.IgnoreCollision(_colliders[j], other, true);
                }
            }
        }

        public void Recover()
        {
            if (!IsActive)
            {
                return;
            }

            // 몸이 떨어진 곳으로 본체를 옮긴다. 뼈는 visual 자식이라 본체를 먼저 옮기면 몸이 같이 끌려가므로,
            // 엉덩이 월드 위치를 먼저 읽어 두고 물리를 끈 다음 옮긴다.
            Vector3 landed = _fellThrough ? _knockedFrom : _hips.position;
            SetPhysics(false);

            Transform root = _owner.transform;
            Vector3 target = new(landed.x, root.position.y, landed.z);
            bool onNavMesh = false;
            Vector3 navPosition = landed;
            if (_agent != null)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };

                // 떨어진 곳 가까이 → 조금 멀리 → 쓰러지기 시작한 자리 순으로 NavMesh를 찾는다.
                if (NavMesh.SamplePosition(landed, out NavMeshHit hit, recoverSnapRadius, filter) ||
                    NavMesh.SamplePosition(landed, out hit, recoverFarRadius, filter) ||
                    NavMesh.SamplePosition(_knockedFrom, out hit, recoverSnapRadius, filter))
                {
                    // 본체는 Agent의 baseOffset만큼 바닥보다 떠 있어야 한다. 바닥 높이 그대로 두면 Agent가 NavMesh를 못 찾는다.
                    target = hit.position + Vector3.up * _agent.baseOffset;
                    navPosition = hit.position;
                    onNavMesh = true;
                }
                else
                {
                    Debug.LogWarning($"[{nameof(RagdollModule)}] {_owner.name}이(가) 일어설 NavMesh를 찾지 못했습니다. 떨어진 곳 {landed}", this);
                }

                if (onNavMesh && (new Vector3(hit.position.x - _knockedFrom.x, 0f, hit.position.z - _knockedFrom.z)).sqrMagnitude < 0.01f &&
                    (new Vector3(landed.x - _knockedFrom.x, 0f, landed.z - _knockedFrom.z)).sqrMagnitude > recoverFarRadius * recoverFarRadius)
                {
                    Debug.LogWarning($"[{nameof(RagdollModule)}] {_owner.name}의 몸이 너무 멀리({landed}) 떨어져 쓰러진 자리에서 일어섭니다.", this);
                }
            }

            root.position = target;

            _animator.enabled = true;
            _animator.Rebind();

            if (_rootCollider != null)
            {
                _rootCollider.enabled = true;
            }

            if (_agent != null)
            {
                _agent.enabled = true;
                if (onNavMesh && _agent.isOnNavMesh)
                {
                    _agent.Warp(navPosition);
                }
            }

            IsActive = false;
            _stayDown = false;
            Recovered?.Invoke();
        }

        private void SetPhysics(bool on)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                Rigidbody body = _bodies[i];
                if (!on && !body.isKinematic)
                {
                    // kinematic으로 바꾸기 전에 속도를 비운다. kinematic 강체의 속도는 설정할 수 없다.
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                // kinematic 강체는 Speculative만 받는다. 켤 때만 두께 없는 바닥도 놓치지 않는 Continuous로 바꾼다.
                if (on)
                {
                    body.isKinematic = false;
                    body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                }
                else
                {
                    // 서 있을 땐 애니메이션이 뼈를 옮긴다. 보간을 켜 두면 물리가 지난 스텝 자세로 뼈를 되돌려 떨린다.
                    body.interpolation = RigidbodyInterpolation.None;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    body.isKinematic = true;
                }

                body.detectCollisions = on;
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                _colliders[i].enabled = on;
            }

            if (!on)
            {
                // 무시하던 상대와의 충돌을 되돌린다. 다음에 쓰러질 때는 그때의 상대만 뺀다.
                for (int i = 0; i < _ignored.Count; i++)
                {
                    if (_ignored[i] == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < _colliders.Count; j++)
                    {
                        Physics.IgnoreCollision(_colliders[j], _ignored[i], false);
                    }
                }

                _ignored.Clear();
                return;
            }

            // 몸끼리 부딪히면 관절로 묶인 뼈들이 서로 밀어내며 튄다. 같은 몸의 콜라이더끼리는 충돌을 끈다.
            // 콜라이더를 껐다 켜면 이 설정이 풀리므로 켤 때마다 다시 건다.
            for (int i = 0; i < _colliders.Count; i++)
            {
                for (int j = i + 1; j < _colliders.Count; j++)
                {
                    Physics.IgnoreCollision(_colliders[i], _colliders[j], true);
                }
            }
        }

        /// <summary>프리팹에 구워 둔 뼈 강체와 그 콜라이더를 모은다. 머리카락이 들고 있는 뼈대 사본에는 강체가 없어 섞이지 않는다.</summary>
        private void Collect()
        {
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _animator.GetComponentsInChildren(true, _bodies);

            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].maxDepenetrationVelocity = maxDepenetration;
                _colliders.AddRange(_bodies[i].GetComponents<Collider>());
            }

            if (_hips == null || _bodies.Count == 0)
            {
                Debug.LogError($"[{nameof(RagdollModule)}] {_owner.name}에 구운 래그돌이 없습니다. Tools/JW/Customers/Bake Customer Ragdoll을 눌러 주세요.", this);
                return;
            }

            _built = true;
            SetPhysics(false);
        }
    }
}
