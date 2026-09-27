using System;
using System.Collections.Generic;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Ragdoll
{
    /// <summary>휴머노이드 visual을 래그돌로 만든다. 뼈에 붙일 강체·콜라이더·관절은 처음 초기화할 때 Animator의 휴머노이드 뼈로
    /// 직접 만든다 — visual을 어떤 캐릭터로 갈아 끼워도 프리팹마다 래그돌을 다시 짤 필요가 없다.
    /// 평소에는 뼈 강체가 kinematic이고 콜라이더가 꺼져 있어 이동·길찾기·차 감지에 끼어들지 않는다.</summary>
    public class RagdollModule : AbstractModule, IRagdoll
    {
        [Header("몸")]
        [Tooltip("몸 전체 질량(kg). 뼈 길이에 비례해 나눈다.")]
        [SerializeField, Min(1f)] private float totalMass = 60f;

        [Tooltip("팔다리 굵기 = 뼈 길이 × 이 값.")]
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

        public bool IsActive { get; private set; }


        public event Action Recovered;

        private readonly List<Rigidbody> _bodies = new();
        private readonly List<Collider> _colliders = new();

        /// <summary>관절 하나를 만들 설정. 관절은 만들어지는 순간의 자세를 기준으로 각도를 재므로, 미리 만들어 두면
        /// 걷던 자세로 쓰러질 때 이미 한계를 넘은 관절이 한순간에 몸을 끌어당겨 튕겨 낸다. 그래서 쓰러질 때 만든다.</summary>
        private readonly struct JointSpec
        {
            public readonly Rigidbody Body;
            public readonly Rigidbody Parent;
            public readonly float Twist;
            public readonly float Swing1;
            public readonly float Swing2;

            public JointSpec(Rigidbody body, Rigidbody parent, float twist, float swing1, float swing2)
            {
                Body = body;
                Parent = parent;
                Twist = twist;
                Swing1 = swing1;
                Swing2 = swing2;
            }
        }

        private readonly List<JointSpec> _jointSpecs = new();
        private readonly List<CharacterJoint> _joints = new();

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

            Build();
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
            if (!_built)
            {
                return;
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

            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].linearVelocity += launchVelocity;
            }
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
            if (on)
            {
                CreateJoints();
            }
            else
            {
                DestroyJoints();
            }

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
                }
                else
                {
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

        // ---------------- 휴머노이드 뼈로 래그돌 만들기 ----------------

        private void Build()
        {
            _hips = Bone(HumanBodyBones.Hips);
            Transform spine = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
            Transform head = Bone(HumanBodyBones.Head);
            if (_hips == null || spine == null || head == null)
            {
                Debug.LogError($"[{nameof(RagdollModule)}] {_owner.name}의 휴머노이드 뼈(엉덩이·척추·머리)를 찾지 못해 래그돌을 만들지 못합니다.", this);
                return;
            }

            float height = Vector3.Distance(_hips.position, head.position) * 2.2f;

            Rigidbody hipsBody = AddBody(_hips, null, 0.2f);
            AddBox(_hips, spine, height * 0.09f);

            Rigidbody spineBody = AddBody(spine, hipsBody, 0.2f);
            AddCapsule(spine, head, height * 0.09f);
            _jointSpecs.Add(new JointSpec(spineBody, hipsBody, 20f, 20f, 15f));

            Rigidbody headBody = AddBody(head, spineBody, 0.08f);
            AddSphere(head, height * 0.06f);
            _jointSpecs.Add(new JointSpec(headBody, spineBody, 30f, 30f, 25f));

            Limb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, spineBody, 0.035f, 0.025f, false);
            Limb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, spineBody, 0.035f, 0.025f, false);
            Limb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, hipsBody, 0.1f, 0.06f, true);
            Limb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, hipsBody, 0.1f, 0.06f, true);

            _built = true;
            SetPhysics(false);
        }

        private void Limb(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Rigidbody parent,
            float upperMass, float lowerMass, bool leg)
        {
            Transform u = Bone(upper), l = Bone(lower), e = Bone(end);
            if (u == null || l == null)
            {
                return;
            }

            Rigidbody upperBody = AddBody(u, parent, upperMass);
            AddCapsule(u, l, Vector3.Distance(u.position, l.position) * limbThickness * 0.5f);
            _jointSpecs.Add(new JointSpec(upperBody, parent, leg ? 30f : 40f, leg ? 40f : 60f, leg ? 20f : 40f));

            Rigidbody lowerBody = AddBody(l, upperBody, lowerMass);
            Vector3 tip = e != null ? e.position : l.position + (l.position - u.position) * 0.9f;
            AddCapsule(l, tip, Vector3.Distance(l.position, tip) * limbThickness * 0.45f);

            // 쓰러지는 순간의 자세가 기준이라 한쪽으로만 굽는 한계는 둘 수 없다. 좌우로 고르게, 굽는 축만 넉넉히 준다.
            _jointSpecs.Add(new JointSpec(lowerBody, upperBody, 60f, 10f, 5f));
        }

        private Transform Bone(HumanBodyBones bone) => _animator.GetBoneTransform(bone);

        private Rigidbody AddBody(Transform bone, Rigidbody parent, float massShare)
        {
            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = totalMass * massShare;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxDepenetrationVelocity = maxDepenetration;
            body.isKinematic = true;
            _bodies.Add(body);
            return body;
        }

        /// <summary>지금 자세를 기준으로 관절을 만든다.</summary>
        private void CreateJoints()
        {
            for (int i = 0; i < _jointSpecs.Count; i++)
            {
                JointSpec spec = _jointSpecs[i];
                CharacterJoint joint = spec.Body.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = spec.Parent;
                joint.enablePreprocessing = false;
                joint.enableProjection = true;
                joint.lowTwistLimit = new SoftJointLimit { limit = -spec.Twist };
                joint.highTwistLimit = new SoftJointLimit { limit = spec.Twist };
                joint.swing1Limit = new SoftJointLimit { limit = spec.Swing1 };
                joint.swing2Limit = new SoftJointLimit { limit = spec.Swing2 };
                _joints.Add(joint);
            }
        }

        private void DestroyJoints()
        {
            for (int i = 0; i < _joints.Count; i++)
            {
                if (_joints[i] != null)
                {
                    DestroyImmediate(_joints[i]);
                }
            }

            _joints.Clear();
        }

        /// <summary>뼈에서 다음 뼈(또는 끝점)까지 이어지는 캡슐. 뼈의 로컬 축 중 그 방향과 가장 가까운 축으로 세운다.</summary>
        private void AddCapsule(Transform bone, Transform next, float radius) => AddCapsule(bone, next.position, radius);

        private void AddCapsule(Transform bone, Vector3 end, float radius)
        {
            Vector3 local = bone.InverseTransformPoint(end);
            CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = LargestAxis(local);

            float scale = AxisScale(bone, capsule.direction);
            float length = local.magnitude;
            capsule.center = local * 0.5f;
            capsule.radius = Mathf.Max(radius / scale, 0.01f);
            capsule.height = length + capsule.radius;
            _colliders.Add(capsule);
        }

        private void AddBox(Transform bone, Transform next, float halfWidth)
        {
            Vector3 local = bone.InverseTransformPoint(next.position);
            BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
            int axis = LargestAxis(local);
            float w = halfWidth * 2f / AxisScale(bone, axis == 0 ? 1 : 0);
            Vector3 size = new(w, w, w);
            size[axis] = Mathf.Abs(local[axis]);
            box.size = size;
            box.center = local * 0.5f;
            _colliders.Add(box);
        }

        private void AddSphere(Transform bone, float radius)
        {
            SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius / AxisScale(bone, 1);
            sphere.center = new Vector3(0f, sphere.radius, 0f);
            _colliders.Add(sphere);
        }

        private static int LargestAxis(Vector3 v)
        {
            Vector3 a = new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }

        /// <summary>뼈 로컬 축 하나가 월드에서 몇 m인지. 모델이 스케일돼 있어도 콜라이더 크기를 월드 기준으로 맞춘다.</summary>
        private static float AxisScale(Transform bone, int axis)
        {
            Vector3 unit = Vector3.zero;
            unit[axis] = 1f;
            float s = bone.TransformVector(unit).magnitude;
            return s > 1e-5f ? s : 1f;
        }
    }
}
