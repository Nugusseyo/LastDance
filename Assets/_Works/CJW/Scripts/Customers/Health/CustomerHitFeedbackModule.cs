using System.Collections.Generic;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.Shared.Combat;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Health
{
    /// <summary>손님이 맞은 순간의 손맛. 전용 피격 모션이 없어도 맞은 게 느껴지도록 여러 겹을 한꺼번에 얹는다.
    /// ① 게임 전체가 아주 잠깐 멈칫(히트스톱) ② 때린 쪽 화면이 튕김 ③ 맞은 자리에서 피가 튐 ④ 몸이 붉게 번쩍임
    /// ⑤ 상체가 맞은 방향으로 확 젖혀졌다 출렁이며 돌아옴 ⑥ 맞은 방향으로 조금 밀려남.
    /// 죽는 타격이면 몸은 래그돌이 날리므로 ⑤⑥은 건너뛰고 ①②③을 크게 한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CustomerHitFeedbackModule : AbstractModule, ICustomerHitFeedback, IUpdate
    {
        [Header("멈칫 (히트스톱)")]
        [Tooltip("맞은 순간 게임 전체를 이 시간(실제 초)만큼 거의 멈춘다. 타격이 몸에 박히는 느낌을 만든다.")]
        [SerializeField, Min(0f)] private float hitStop = 0.07f;

        [Tooltip("쓰러지는 타격의 멈칫 시간(실제 초).")]
        [SerializeField, Min(0f)] private float killStop = 0.16f;

        [Tooltip("멈칫하는 동안의 시간 배율. 0에 가까울수록 딱 멈춘다.")]
        [SerializeField, Range(0f, 1f)] private float hitStopScale = 0.05f;

        [Header("때린 쪽 화면 반동")]
        [Tooltip("맞힐 때 때린 쪽 카메라가 튕기는 각도(도).")]
        [SerializeField, Min(0f)] private float cameraKick = 1.8f;

        [Tooltip("쓰러뜨릴 때 카메라가 튕기는 각도(도).")]
        [SerializeField, Min(0f)] private float killCameraKick = 4f;

        [Header("피")]
        [Tooltip("맞은 자리에서 튈 피 파티클. 손님마다 하나씩 만들어 두고 맞을 때마다 뿜는다. 비우면 피가 튀지 않는다.")]
        [SerializeField] private ParticleSystem bloodPrefab;

        [Tooltip("한 대에 튀는 핏방울 수. 자식 파티클(피 안개)은 이 수의 1/4만큼 뿜는다.")]
        [SerializeField, Min(0)] private int bloodCount = 16;

        [Tooltip("쓰러지는 타격에서 핏방울 수에 곱하는 값.")]
        [SerializeField, Min(1f)] private float killBloodMultiplier = 2.5f;

        [Header("번쩍임")]
        [Tooltip("맞은 순간 몸이 물드는 색. 이 색에서 원래 색으로 빠르게 돌아온다.")]
        [SerializeField] private Color flashColor = new(1f, 0.25f, 0.25f, 1f);

        [Tooltip("번쩍임이 원래 색으로 돌아오는 시간(실제 초).")]
        [SerializeField, Min(0.01f)] private float flashTime = 0.16f;

        [Header("몸 젖힘")]
        [Tooltip("맞은 순간 상체가 맞은 방향으로 젖혀지는 각도(도). 척추·가슴·머리에 나눠 준다.")]
        [SerializeField, Range(0f, 60f)] private float joltAngle = 28f;

        [Tooltip("상체가 비틀리는 최대 각도(도). 매번 좌우를 무작위로 골라 같은 동작이 반복돼 보이지 않게 한다.")]
        [SerializeField, Range(0f, 30f)] private float joltTwist = 10f;

        [Tooltip("젖혀진 상체가 출렁이며 제자리로 돌아오는 시간(초).")]
        [SerializeField, Min(0.05f)] private float joltTime = 0.45f;

        [Header("밀려남")]
        [Tooltip("때린 세기(m/s) 1당 밀려나는 거리(m).")]
        [SerializeField, Min(0f)] private float knockbackPerForce = 0.1f;

        [Tooltip("한 대에 밀려나는 최대 거리(m).")]
        [SerializeField, Min(0f)] private float maxKnockback = 0.8f;

        [Tooltip("밀려나는 데 걸리는 시간(초). 처음에 확 밀리고 끝에서 멈춘다.")]
        [SerializeField, Min(0.01f)] private float knockbackTime = 0.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>젖힘을 나눠 받는 뼈와 몫. 아래 뼈가 먼저 와야 위 뼈가 그 위에 더 젖혀진다.</summary>
        private static readonly (HumanBodyBones Bone, float Share)[] JoltBones =
        {
            (HumanBodyBones.Spine, 0.3f),
            (HumanBodyBones.Chest, 0.25f),
            (HumanBodyBones.UpperChest, 0.1f),
            (HumanBodyBones.Head, 0.35f),
        };

        private AbstractCustomer _customer;
        private Animator _animator;

        // ---------- 몸 젖힘 ----------
        private readonly List<Transform> _joltBones = new();
        private readonly List<float> _joltShares = new();

        /// <summary>애니메이션이 준 뼈 자세. 이번 프레임에 애니메이터가 뼈를 새로 쓰지 않았으면(화면 밖 컬링 등) 이걸 기준으로 다시 젖힌다.
        /// 그러지 않으면 지난 프레임에 젖힌 자세 위에 또 젖혀 몸이 계속 꺾인다.</summary>
        private readonly List<Quaternion> _joltBase = new();

        /// <summary>지난 프레임에 이 모듈이 써 넣은 뼈 자세.</summary>
        private readonly List<Quaternion> _joltWritten = new();

        private float _joltElapsed = float.MaxValue;
        private Vector3 _joltAxis;
        private float _joltTwistSign;
        private bool _joltWrote;

        // ---------- 밀려남 ----------
        private Vector3 _knockDirection;
        private float _knockDistance;
        private float _knockElapsed = float.MaxValue;
        private float _knockDone;

        // ---------- 번쩍임 ----------
        private readonly struct FlashSlot
        {
            public readonly Renderer Renderer;
            public readonly int Index;
            public readonly int Property;
            public readonly Color Original;

            public FlashSlot(Renderer renderer, int index, int property, Color original)
            {
                Renderer = renderer;
                Index = index;
                Property = property;
                Original = original;
            }
        }

        private readonly List<FlashSlot> _flashSlots = new();
        private readonly List<Material> _materials = new();
        private MaterialPropertyBlock _block;
        private float _flashLeft;

        // ---------- 피 ----------
        private ParticleSystem[] _blood;
        private Transform _bloodRoot;
        private Transform _chest;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _customer = owner as AbstractCustomer;
            _animator = owner.GetComponentInChildren<Animator>(true);
            _block = new MaterialPropertyBlock();

            if (_animator == null || !_animator.isHuman)
            {
                // 뼈를 모르면 젖힘과 피 위치만 빠진다. 나머지 연출은 그대로 한다.
                Debug.LogWarning($"[{nameof(CustomerHitFeedbackModule)}] {owner.name}에 휴머노이드 Animator가 없어 몸 젖힘을 건너뜁니다.", this);
                return;
            }

            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest) ?? _animator.GetBoneTransform(HumanBodyBones.Spine);

            for (int i = 0; i < JoltBones.Length; i++)
            {
                Transform bone = _animator.GetBoneTransform(JoltBones[i].Bone);
                if (bone == null)
                {
                    // 윗가슴 뼈가 없는 캐릭터가 많다. 빠진 몫은 바로 앞 뼈에 얹어 전체 젖힘 각도를 지킨다.
                    if (_joltShares.Count > 0)
                    {
                        _joltShares[^1] += JoltBones[i].Share;
                    }

                    continue;
                }

                _joltBones.Add(bone);
                _joltShares.Add(JoltBones[i].Share);
                _joltBase.Add(Quaternion.identity);
                _joltWritten.Add(Quaternion.identity);
            }
        }

        public void Play(HitInfo hit, bool lethal)
        {
            Vector3 direction = new(hit.Direction.x, 0f, hit.Direction.z);
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : -_owner.transform.forward;

            HitStop.Freeze(lethal ? killStop : hitStop, hitStopScale);
            CameraKick.Kick(hit.Attacker, lethal ? killCameraKick : cameraKick);
            EmitBlood(direction, lethal);
            StartFlash();

            if (lethal)
            {
                // 곧 래그돌이 몸을 넘겨받는다. 뼈를 건드리거나 본체를 옮기면 물리와 싸운다.
                StopJolt();
                _knockElapsed = float.MaxValue;
                return;
            }

            StartJolt(direction);
            StartKnockback(direction, hit.Force);
        }

        public void Cancel()
        {
            StopJolt();
            _knockElapsed = float.MaxValue;
            ClearFlash();

            if (_blood != null)
            {
                for (int i = 0; i < _blood.Length; i++)
                {
                    _blood[i].Clear();
                }
            }
        }

        // 풀로 돌아가며 꺼질 때 붉은 몸이 남지 않게 한다.
        private void OnDisable()
        {
            Cancel();
        }

        public void OnUpdate(float dt)
        {
            UpdateKnockback(dt);
        }

        // 애니메이터가 뼈를 쓴 뒤에 젖혀야 덮이지 않는다.
        private void LateUpdate()
        {
            UpdateJolt(Time.deltaTime);

            // 멈칫하는 동안에도 번쩍임은 보여야 한다. 실제 시간으로 돌린다.
            UpdateFlash(Time.unscaledDeltaTime);
        }

        // ---------------- 피 ----------------

        private void EmitBlood(Vector3 direction, bool lethal)
        {
            if (bloodPrefab == null || bloodCount <= 0)
            {
                return;
            }

            if (_blood == null)
            {
                ParticleSystem instance = Instantiate(bloodPrefab, _owner.transform);
                _bloodRoot = instance.transform;
                _blood = instance.GetComponentsInChildren<ParticleSystem>(true);
            }

            // 가슴 앞, 때린 쪽 면에서 맞은 방향으로 뿜는다. 조금 위로 들어 올려야 바닥에 바로 박히지 않고 흩날린다.
            Vector3 center = _chest != null ? _chest.position : _owner.transform.position + Vector3.up * 1.3f;
            _bloodRoot.SetPositionAndRotation(center - direction * 0.15f,
                                              Quaternion.LookRotation(direction + Vector3.up * 0.35f, Vector3.up));

            int count = Mathf.RoundToInt(bloodCount * (lethal ? killBloodMultiplier : 1f));
            for (int i = 0; i < _blood.Length; i++)
            {
                _blood[i].Emit(i == 0 ? count : Mathf.Max(1, count / 4));
            }
        }

        // ---------------- 번쩍임 ----------------

        private void StartFlash()
        {
            // 번쩍이는 중에 또 맞으면 이미 물든 색을 원래 색으로 잘못 읽는다. 처음 번쩍일 때만 원래 색을 모은다.
            if (_flashLeft <= 0f)
            {
                CollectFlashSlots();
            }

            _flashLeft = flashTime;
        }

        private void CollectFlashSlots()
        {
            _flashSlots.Clear();
            if (_animator == null)
            {
                return;
            }

            // 몸(visual) 아래의 메시만 물들인다. 머리 위 게이지·말풍선 같은 UI까지 붉어지면 안 된다.
            Renderer[] renderers = _animator.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer is not SkinnedMeshRenderer && renderer is not MeshRenderer)
                {
                    continue;
                }

                renderer.GetSharedMaterials(_materials);
                for (int m = 0; m < _materials.Count; m++)
                {
                    Material material = _materials[m];
                    if (material == null)
                    {
                        continue;
                    }

                    int property = material.HasProperty(BaseColorId) ? BaseColorId : material.HasProperty(ColorId) ? ColorId : 0;
                    if (property != 0)
                    {
                        _flashSlots.Add(new FlashSlot(renderer, m, property, material.GetColor(property)));
                    }
                }
            }
        }

        private void UpdateFlash(float dt)
        {
            if (_flashLeft <= 0f)
            {
                return;
            }

            _flashLeft -= dt;
            if (_flashLeft <= 0f)
            {
                ClearFlash();
                return;
            }

            // 맞은 순간 확 물들고 빠르게 빠진다.
            float k = _flashLeft / flashTime;
            k *= k;

            for (int i = 0; i < _flashSlots.Count; i++)
            {
                FlashSlot slot = _flashSlots[i];
                if (slot.Renderer == null)
                {
                    continue;
                }

                Color color = Color.Lerp(slot.Original, flashColor, k);
                color.a = slot.Original.a;

                _block.Clear();
                _block.SetColor(slot.Property, color);
                slot.Renderer.SetPropertyBlock(_block, slot.Index);
            }
        }

        private void ClearFlash()
        {
            _flashLeft = 0f;

            // 블록을 남기면 SRP Batcher에서 빠져 매 프레임 따로 그려진다. 끝나면 비운다.
            for (int i = 0; i < _flashSlots.Count; i++)
            {
                if (_flashSlots[i].Renderer != null)
                {
                    _flashSlots[i].Renderer.SetPropertyBlock(null, _flashSlots[i].Index);
                }
            }

            _flashSlots.Clear();
        }

        // ---------------- 몸 젖힘 ----------------

        private void StartJolt(Vector3 direction)
        {
            if (_joltBones.Count == 0)
            {
                return;
            }

            // 위쪽 축을 이 축으로 돌리면 머리가 맞은 방향으로 넘어간다. 앞에서 맞으면 뒤로, 뒤에서 맞으면 앞으로 꺾인다.
            _joltAxis = Vector3.Cross(Vector3.up, direction);
            _joltTwistSign = Random.value < 0.5f ? -1f : 1f;
            _joltElapsed = 0f;
        }

        private void StopJolt()
        {
            _joltElapsed = float.MaxValue;
            _joltWrote = false;
        }

        private void UpdateJolt(float dt)
        {
            if (_joltElapsed >= joltTime)
            {
                return;
            }

            // 쓰러졌거나 애니메이터가 꺼졌으면 뼈는 물리 몫이다.
            if (_animator == null || !_animator.enabled || (_customer != null && _customer.IsKnockedDown))
            {
                StopJolt();
                return;
            }

            _joltElapsed += dt;
            float u = Mathf.Clamp01(_joltElapsed / joltTime);

            // 맞은 순간 최대로 꺾였다가, 한 번 반대쪽으로 살짝 넘어가며 제자리로 돌아온다.
            float weight = (1f - u) * (1f - u) * Mathf.Cos(u * Mathf.PI * 1.5f);

            for (int i = 0; i < _joltBones.Count; i++)
            {
                Transform bone = _joltBones[i];

                // 애니메이터가 이번 프레임에 뼈를 새로 썼으면 그게 기준이다. 아니면(컬링 등) 지난 기준 위에 다시 얹는다.
                Quaternion local = bone.localRotation;
                if (_joltWrote && local == _joltWritten[i])
                {
                    local = _joltBase[i];
                }

                _joltBase[i] = local;
                bone.localRotation = local;

                float share = _joltShares[i] * weight;
                Quaternion jolt = Quaternion.AngleAxis(joltAngle * share, _joltAxis) *
                                  Quaternion.AngleAxis(joltTwist * _joltTwistSign * share, Vector3.up);
                bone.rotation = jolt * bone.rotation;

                _joltWritten[i] = bone.localRotation;
            }

            _joltWrote = true;

            if (u >= 1f)
            {
                // 끝나는 프레임은 애니메이션 자세 그대로 돌려놓는다. 애니메이터가 한동안 뼈를 안 쓰면 마지막 젖힘이 굳는다.
                for (int i = 0; i < _joltBones.Count; i++)
                {
                    _joltBones[i].localRotation = _joltBase[i];
                }

                StopJolt();
            }
        }

        // ---------------- 밀려남 ----------------

        private void StartKnockback(Vector3 direction, float force)
        {
            _knockDistance = Mathf.Min(force * knockbackPerForce, maxKnockback);
            if (_knockDistance <= 0f)
            {
                return;
            }

            _knockDirection = direction;
            _knockElapsed = 0f;
            _knockDone = 0f;
        }

        private void UpdateKnockback(float dt)
        {
            if (_knockElapsed >= knockbackTime)
            {
                return;
            }

            if (_customer != null && (_customer.IsKnockedDown || (_customer.Boarding != null && _customer.Boarding.IsBoarded)))
            {
                _knockElapsed = float.MaxValue;
                return;
            }

            _knockElapsed += dt;
            float x = Mathf.Clamp01(_knockElapsed / knockbackTime);

            // 처음에 확 밀리고 끝에서 멈춘다.
            float eased = 1f - (1f - x) * (1f - x) * (1f - x);
            Move(_knockDirection * (_knockDistance * (eased - _knockDone)));
            _knockDone = eased;
        }

        private void Move(Vector3 step)
        {
            Transform body = _owner.transform;
            NavMeshAgent agent = _customer != null ? _customer.Agent : null;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // Agent로 옮겨야 벽·차를 뚫고 밀리지 않는다. 이동 모듈은 Agent가 몸을 직접 옮기지 않게 해 두므로
                // Agent가 멈춘 자리로 몸을 맞춘다. 높이는 몸 그대로 둔다(Agent는 바닥, 몸은 baseOffset만큼 위).
                agent.Move(step);
                Vector3 moved = agent.nextPosition;
                body.position = new Vector3(moved.x, body.position.y, moved.z);
                return;
            }

            body.position += step;
        }
    }
}
