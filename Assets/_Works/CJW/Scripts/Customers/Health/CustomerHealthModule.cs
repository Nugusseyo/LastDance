using System;
using _Works.CJW.Scripts.Customers.Ragdoll;
using _Works.Shared.Combat;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using DevLib.ObjectPool.Runtime;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Health
{
    /// <summary>손님이 플레이어에게 맞는 창구. 맞으면 체력을 깎고, 체력이 바닥나면 죽는다 —
    /// 방문에서 빠지고, 때린 방향으로 날아가 래그돌로 쓰러진 뒤(<see cref="IRagdoll"/>), 잠시 후 풀로 돌아가 사라진다.
    /// 반격·도망은 프리팹의 CustomerFSMModule에 전투·도망 상태가 꽂혀 있을 때만 한다.</summary>
    public class CustomerHealthModule : AbstractModule, ICustomerHealth
    {
        [Header("체력")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;

        [Tooltip("한 번 맞은 뒤 이 시간(초) 동안은 다시 맞지 않는다. 한 번 휘두른 주먹이 몸의 여러 콜라이더에 겹쳐 여러 번 들어가는 걸 막는다.")]
        [SerializeField, Min(0f)] private float invincibleTime = 0.2f;

        [Header("죽음")]
        [Tooltip("죽을 때 때린 세기와 상관없이 더하는 위쪽 세기(m/s). 아래 충격량 배율이 곱해진다.")]
        [SerializeField, Min(0f)] private float deathLaunchUp = 1.5f;

        [Tooltip("죽을 때 맞은 자리에서 가장 가까운 뼈에 주는 충격량 = (때린 방향 × 세기 + 위쪽) × 이 값(N·s per m/s). " +
                 "맞은 부위가 먼저 튕기고 나머지 몸은 관절에 끌려간다.")]
        [SerializeField, Min(0f)] private float deathImpulse = 20f;

        [Tooltip("맞은 자리의 높이(m). 공격이 어디에 맞았는지 알려 주지 않아서 이 높이의 몸 앞면을 맞은 자리로 본다.")]
        [SerializeField, Min(0f)] private float hitHeight = 1.3f;

        [Tooltip("쓰러진 뒤 이 시간(초)이 지나면 사라진다.")]
        [SerializeField, Min(0f)] private float despawnDelay = 3f;

        [Tooltip("죽은 손님을 돌려보낼 풀.")]
        [SerializeField] private PoolManagerSO poolManager;

        [Header("반응")]
        [Tooltip("죽지 않을 만큼 맞았을 때 트는 피격 클립(Animator 상태 이름).")]
        [SerializeField] private HashDataSO hitClip;

        [Tooltip("피격 클립을 틀고 움찔하는 시간(초). 이 동안은 하던 행동을 멈춘다.")]
        [SerializeField, Min(0f)] private float hitDuration = 0.6f;

        [Tooltip("켜면 움찔한 뒤 때린 쪽에게 덤빈다. 전투 상태가 없는 손님은 대신 도망친다. 둘 다 없으면 하던 행동으로 돌아간다.")]
        [SerializeField] private bool fightBack = true;

        [Header("사운드")]
        [Tooltip("맞았지만 아직 살아 있을 때 낼 소리(아픈 신음).")]
        [SerializeField] private SoundClipSo damagedSound;

        [Tooltip("체력이 바닥나 쓰러질 때 낼 소리(마지막 비명).")]
        [SerializeField] private SoundClipSo deathSound;

        public float MaxHealth => maxHealth;

        public float CurrentHealth { get; private set; }

        public bool IsDead { get; private set; }

        public bool CanBeHit => isActiveAndEnabled && !IsDead &&
                                (_customer == null || ((_customer.Boarding == null || !_customer.Boarding.IsBoarded) && !_customer.IsKnockedDown));

        public event Action<HitInfo> Damaged;
        public event Action<HitInfo> Died;

        private AbstractCustomer _customer;
        private float _invincibleUntil;

        /// <summary>몇 번째 삶인지. 풀에서 다시 꺼내질 때마다 오른다. 앞 삶에 걸어 둔 사라짐 예약이 새 손님을 치우지 않게 한다.</summary>
        private int _life;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _customer = owner as AbstractCustomer;
            ResetHealth();
        }

        public void ResetHealth()
        {
            _life++;
            IsDead = false;
            CurrentHealth = maxHealth;
            _invincibleUntil = 0f;
        }

        public void TakeHit(HitInfo hit)
        {
            if (!CanBeHit || Time.time < _invincibleUntil || hit.Damage <= 0f)
            {
                return;
            }

            _invincibleUntil = Time.time + invincibleTime;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - hit.Damage);

            Damaged?.Invoke(hit);

            // 연출을 먼저 튼다. 래그돌이 켜지기 전이어야 가슴 뼈 자리에서 피가 튀고, 몸이 물든 채로 쓰러진다.
            _customer?.HitFeedback?.Play(hit, CurrentHealth <= 0f);

            if (CurrentHealth <= 0f)
            {
                _customer?.Sound?.Play(deathSound);
                Die(hit);
                return;
            }

            _customer?.Sound?.Play(damagedSound);
            React(hit);
        }

        private void Die(HitInfo hit)
        {
            IsDead = true;

            if (_customer != null)
            {
                // 머리 위 대사도 같이 접는다. 안 그러면 쓰러진 몸 위에 요구하던 말이 끝까지 떠 있다.
                _customer.Fsm?.Context?.EndSpeech();

                // 방문에서 먼저 뺀다. 남겨 두면 차가 죽은 손님이 타기를 영영 기다리고, 방문을 닫을 때 풀에 한 번 더 넣는다.
                if (_customer.Session == null || !_customer.Session.Remove(_customer))
                {
                    _customer.Fsm?.Stop();
                }

                IRagdoll ragdoll = _customer.Ragdoll;
                if (ragdoll != null)
                {
                    Vector3 planar = new(hit.Direction.x, 0f, hit.Direction.z);
                    planar = planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.zero;

                    // 때린 쪽 몸 앞면을 맞은 자리로 본다. 그 자리에서 가장 가까운 뼈(대개 가슴)가 먼저 튕겨 나간다.
                    Vector3 hitPoint = _customer.transform.position + Vector3.up * hitHeight - planar * 0.3f;
                    Vector3 impulse = (planar * hit.Force + Vector3.up * deathLaunchUp) * deathImpulse;
                    ragdoll.ActivateAt(hitPoint, impulse, stayDown: true);
                }
                else
                {
                    // 쓰러질 몸이 없으면 선 채로 기다렸다 사라진다. 길찾기만 멈춰 둔다.
                    _customer.Mover?.Stop();
                }
            }

            Died?.Invoke(hit);

            DespawnLater(_life).Forget();
        }

        private async UniTaskVoid DespawnLater(int life)
        {
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(despawnDelay), cancellationToken: destroyCancellationToken)
                                          .SuppressCancellationThrow();

            // 기다리는 사이 풀에서 다시 꺼내졌으면 이미 다른 손님이다. 건드리지 않는다.
            if (cancelled || life != _life || !IsDead || _customer == null)
            {
                return;
            }

            _customer.transform.SetParent(null, true);

            if (poolManager != null)
            {
                poolManager.Push(_customer);
            }
            else
            {
                // 풀이 없으면 반납할 곳이 없다. 끄기라도 해서 치우고, 빠진 설정은 크게 남긴다.
                Debug.LogError($"[{nameof(CustomerHealthModule)}] {_customer.name}에 풀(PoolManagerSO)이 지정되지 않아 반납하지 못하고 끄기만 합니다.", this);
                _customer.gameObject.SetActive(false);
            }
        }

        private void React(HitInfo hit)
        {
            if (_customer == null || _customer.Fsm == null)
            {
                return;
            }

            // 움찔한 뒤 덤빌지(전투 상태가 없으면 도망) 하던 걸 이어 할지는 FSM이 전투·도망 상태를 보고 고른다.
            // 때린 사람이 있으면 그쪽을, 없으면(던진 물건) 날아온 쪽을 본다.
            Transform attacker = hit.Attacker != null ? hit.Attacker.transform : null;
            Vector3 hitFrom = attacker != null ? attacker.position : _customer.transform.position - hit.Direction;
            _customer.Fsm.TakeHit(hitClip, hitDuration, hitFrom, attacker, fightBack);
        }
    }
}
