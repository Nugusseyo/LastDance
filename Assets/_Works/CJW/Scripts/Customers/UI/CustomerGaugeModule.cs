using _Works.CJW.Scripts.Customers.Health;
using _Works.CJW.Scripts.Customers.Patience;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.JYG._Scripts.UI.GuestUI;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.UI
{
    /// <summary>손님 머리 위에 체력과 인내심을 띄운다. 값은 <see cref="ICustomerHealth"/>·<see cref="ICustomerPatience"/>에서 읽어 오기만 하고,
    /// 보이고 숨는 건 게이지가 정한다 — 가득 찬 게이지(다치지 않음·기다리지 않음)는 스스로 숨는다.
    /// 누구도 이 모듈을 부르지 않는 끝단의 화면 표시라 따로 인터페이스를 두지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class CustomerGaugeModule : AbstractModule, IUpdate
    {
        [Tooltip("체력을 보여줄 게이지. 비워 두면 체력을 띄우지 않는다.")]
        [SerializeField] private WorldGauge healthGauge;

        [Tooltip("인내심을 보여줄 게이지. 비워 두면 인내심을 띄우지 않는다.")]
        [SerializeField] private WorldGauge patienceGauge;

        private AbstractCustomer _customer;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _customer = owner as AbstractCustomer;
        }

        public void OnUpdate(float dt)
        {
            if (_customer == null)
            {
                return;
            }

            ICustomerHealth health = _customer.Health;
            ICustomerPatience patience = _customer.Patience;

            // 죽은 손님은 게이지를 모두 접는다. 안 그러면 쓰러진 몸 위에 빈 체력바와 멈춘 인내심이 남는다.
            bool dead = health != null && health.IsDead;

            if (healthGauge != null)
            {
                healthGauge.NormalizedValue = dead || health == null || health.MaxHealth <= 0f
                    ? 1f
                    : health.CurrentHealth / health.MaxHealth;
            }

            if (patienceGauge != null)
            {
                patienceGauge.NormalizedValue = dead || patience == null ? 1f : patience.Normalized;
            }
        }
    }
}
