using System.Collections.Generic;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>달리는 차가 자기 차체와 겹친 사람을 찾아 치임으로 알린다. 물리 충돌을 쓰지 않는다 — 차는 위치를 직접 옮기고
    /// Rigidbody가 없어 충돌 이벤트가 나지 않는다. 차체 크기와 실제 속도는 교통 센서(<see cref="ICarTrafficSensor"/>)의 것을 쓴다.</summary>
    public class CarHitDetectorModule : AbstractModule
    {
        [Tooltip("이 속도(m/s)보다 느리면 치임으로 보지 않는다. 멈춰 선 차에 사람이 스치는 건 치임이 아니다.")]
        [SerializeField, Min(0f)] private float minHitSpeed = 1f;

        [Tooltip("같은 사람을 다시 치임으로 셀 때까지의 시간(초). 차체를 지나가는 몇 프레임 동안 여러 번 세지 않게 한다.")]
        [SerializeField, Min(0f)] private float repeatCooldown = 2f;

        [Header("사운드")]
        [Tooltip("사람을 쳤을 때 차체에서 날 소리(쿵 부딪히는 소리). 치인 쪽의 비명은 손님이 낸다.")]
        [SerializeField] private SoundClipSo hitSound;

        private Car _car;
        private ICarTrafficSensor _sensor;

        /// <summary>최근에 친 대상과 그때 시각.</summary>
        private readonly Dictionary<ICarHittable, float> _recent = new();
        private readonly List<ICarHittable> _expired = new();

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _car = owner as Car;
            _sensor = owner != null ? owner.GetModule<ICarTrafficSensor>() : null;

            if (_sensor == null)
            {
                Debug.LogError($"[{nameof(CarHitDetectorModule)}] {name}에 교통 센서가 없어 치임을 감지하지 못합니다.", this);
            }
        }

        private void OnDisable()
        {
            _recent.Clear();
        }

        // 센서가 LateUpdate에서 속도를 갱신하므로 같은 시점에 본다. 한 프레임 늦은 속도여도 치임 판정에는 충분하다.
        private void LateUpdate()
        {
            if (_sensor == null || _car == null)
            {
                return;
            }

            ForgetOld();

            Vector3 velocity = _sensor.Velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < minHitSpeed * minHitSpeed)
            {
                return;
            }

            IReadOnlyList<ICarHittable> targets = CarHitTargets.Targets;
            Vector3 center = _sensor.Center;
            float reach = _sensor.BoundingRadius;

            // 치임을 알리는 쪽이 목록을 바꿀 수 있으니(쓰러져 등록을 푸는 등) 뒤에서부터 본다.
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (i >= targets.Count)
                {
                    continue;
                }

                ICarHittable target = targets[i];
                if (target == null || !target.CanBeHit || _recent.ContainsKey(target))
                {
                    continue;
                }

                // 멀리 있는 사람은 사각형 검사 전에 거른다.
                Vector3 delta = target.HitPosition - center;
                delta.y = 0f;
                float limit = reach + target.HitRadius;
                if (delta.sqrMagnitude > limit * limit || !_sensor.Overlaps(target.HitPosition, target.HitRadius))
                {
                    continue;
                }

                _recent[target] = Time.time;
                _car.Sound?.Play(hitSound, target.HitPosition);
                CarHitTargets.Raise(new CarHitInfo(_car, target, velocity, target.HitPosition));
            }
        }

        private void ForgetOld()
        {
            if (_recent.Count == 0)
            {
                return;
            }

            _expired.Clear();
            foreach (KeyValuePair<ICarHittable, float> pair in _recent)
            {
                if (Time.time - pair.Value >= repeatCooldown)
                {
                    _expired.Add(pair.Key);
                }
            }

            for (int i = 0; i < _expired.Count; i++)
            {
                _recent.Remove(_expired[i]);
            }
        }
    }
}
