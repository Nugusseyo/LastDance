using _Works.CJW.Scripts.Customers.Ragdoll;
using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers
{
    /// <summary>손님을 차에 치일 수 있는 대상으로 등록하고, 치이면 차가 달리던 방향으로 날려 쓰러뜨린다(<see cref="IRagdoll"/>).
    /// 하던 행동은 끊고, 일어서면 다시 이어 간다. 다른 반응(화냄·방문 종료)은 <see cref="HitByCar"/>를 받아 붙인다.</summary>
    public class CarHitReceiverModule : AbstractModule, ICarHittable
    {
        [Tooltip("몸 반경(m). 0이면 NavMeshAgent의 반경을 쓴다.")]
        [SerializeField, Min(0f)] private float radius;

        [Header("날아감")]
        [Tooltip("차 속도의 몇 배로 날아갈지.")]
        [SerializeField, Min(0f)] private float launchScale = 1.3f;

        [Tooltip("차 속도와 상관없이 더하는 위쪽 속도(m/s).")]
        [SerializeField, Min(0f)] private float launchUp = 2f;

        [Tooltip("차 속도 1m/s마다 더하는 위쪽 속도(m/s). 빠르게 치일수록 높이 뜬다.")]
        [SerializeField, Min(0f)] private float launchUpPerSpeed = 0.4f;

        [Header("사운드")]
        [Tooltip("차에 치였을 때 손님이 낼 소리(비명). 차체에 부딪히는 소리는 차의 CarHitDetectorModule이 낸다.")]
        [SerializeField] private SoundClipSo hitSound;

        private AbstractCustomer _customer;
        private NavMeshAgent _agent;

        /// <summary>이 손님이 차에 치였다.</summary>
        public event Action<CarHitInfo> HitByCar;

        public Vector3 HitPosition => _owner != null ? _owner.transform.position : transform.position;

        public float HitRadius => radius > 0f ? radius : _agent != null ? _agent.radius : 0.3f;

        /// <summary>차에 타 있으면 차와 한 몸이라 치이지 않는다.</summary>
        public bool CanBeHit => isActiveAndEnabled &&
                                (_customer == null || ((_customer.Boarding == null || !_customer.Boarding.IsBoarded) && !_customer.IsKnockedDown));

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _customer = owner as AbstractCustomer;
            _agent = owner != null ? owner.GetComponentInChildren<NavMeshAgent>(true) : null;
        }

        private void OnEnable()
        {
            CarHitTargets.Register(this);
        }

        private void OnDisable()
        {
            CarHitTargets.Unregister(this);
        }

        public void OnHitByCar(CarHitInfo hit)
        {
            _customer?.Sound?.Play(hitSound);

            if (_customer != null && _customer.Ragdoll != null)
            {
                // 행동부터 끊는다. 걷던 상태가 살아 있으면 길찾기가 꺼진 몸을 계속 움직이려 든다.
                _customer.Fsm?.KnockDown();

                Vector3 planar = new(hit.Velocity.x, 0f, hit.Velocity.z);
                Vector3 launch = planar * launchScale + Vector3.up * (launchUp + planar.magnitude * launchUpPerSpeed);
                _customer.Ragdoll.Activate(launch);

                // 차는 Rigidbody 없이 위치만 옮기는 콜라이더라, 겹친 몸을 물리가 터무니없는 속도로 튕겨 낸다.
                // 치인 속도는 이미 날아가는 속도로 줬으니, 쓰러진 동안 달리는 차들과는 부딪히지 않게 한다.
                IReadOnlyList<ICarTrafficSensor> cars = CarTraffic.Sensors;
                for (int i = 0; i < cars.Count; i++)
                {
                    if (cars[i] is Component car)
                    {
                        Car owner = car.GetComponentInParent<Car>();
                        _customer.Ragdoll.IgnoreWhileDown((owner != null ? owner.gameObject : car.gameObject).GetComponentsInChildren<Collider>());
                    }
                }
            }

            HitByCar?.Invoke(hit);
        }
    }
}
