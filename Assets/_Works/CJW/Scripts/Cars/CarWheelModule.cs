using System;
using _Works.CJW.Scripts.ManagingAgents;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>이동 모듈(<see cref="ICarMoveModule"/>)의 속도만큼 바퀴를 굴리고, 조향 바퀴는 조향각만큼 꺾는다.
    /// 바퀴마다 굴리는 축과 꺾는 축을 따로 둔다 — 모델마다 바퀴 로컬 축 방향이 제각각이라서다.</summary>
    public class CarWheelModule : AbstractModule, ICarWheelModule, IUpdate
    {
        [Serializable]
        public class Wheel
        {
            [Tooltip("돌릴 바퀴. 피벗이 바퀴 중심에 있어야 제자리에서 돈다.")]
            public Transform transform;

            [Tooltip("바퀴 로컬 축 중 차축(좌우) 방향. 이 축으로 굴린다.")]
            public Vector3 spinAxis = Vector3.right;

            [Tooltip("바퀴 로컬 축 중 위쪽 방향. 조향 바퀴는 이 축으로 꺾는다.")]
            public Vector3 steerAxis = Vector3.up;

            [Tooltip("켜면 조향각만큼 꺾는다. 보통 앞바퀴.")]
            public bool steer;

            [Tooltip("바퀴 반지름(m). 0이면 렌더러 크기로 잰다.")]
            public float radius;

            [NonSerialized] public Quaternion baseRotation;
            [NonSerialized] public float angle;
        }

        [SerializeField] private Wheel[] wheels = Array.Empty<Wheel>();

        [Tooltip("조향각을 이 배율로 보여준다. 실제 조향각이 커 보이면 줄인다.")]
        [SerializeField, Range(0f, 1.5f)] private float steerVisualScale = 1f;

        [Tooltip("조향각이 이 속도(도/초)보다 빠르게 바뀌지 않게 부드럽게 따라간다.")]
        [SerializeField, Min(1f)] private float steerFollowSpeed = 360f;

        private ICarMoveModule _move;
        private float _shownSteer;

        /// <summary>Initialize 전에는 baseRotation이 비어 있다. 그때 되돌리면 바퀴가 0 쿼터니언으로 망가진다.</summary>
        private bool _initialized;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _move = owner.GetModule<ICarMoveModule>();

            if (_move == null)
            {
                Debug.LogWarning($"[CarWheel] {name}에 ICarMoveModule이 없어 바퀴를 굴리지 않습니다.", this);
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                Wheel wheel = wheels[i];
                if (wheel?.transform == null)
                {
                    continue;
                }

                wheel.baseRotation = wheel.transform.localRotation;

                if (wheel.radius <= 0f)
                {
                    wheel.radius = MeasureRadius(wheel.transform);
                }
            }

            _initialized = true;
        }

        public void OnUpdate(float dt)
        {
            if (_move == null || dt <= 0f)
            {
                return;
            }

            float speed = _move.Speed;
            _shownSteer = Mathf.MoveTowards(_shownSteer, _move.SteerAngleDeg * steerVisualScale, steerFollowSpeed * dt);

            for (int i = 0; i < wheels.Length; i++)
            {
                Wheel wheel = wheels[i];
                if (wheel?.transform == null)
                {
                    continue;
                }

                // 굴러간 거리 / 반지름 = 돈 각도(라디안).
                wheel.angle = Mathf.Repeat(wheel.angle + speed * dt / wheel.radius * Mathf.Rad2Deg, 360f);

                Quaternion steer = wheel.steer ? Quaternion.AngleAxis(_shownSteer, wheel.steerAxis) : Quaternion.identity;
                Quaternion spin = Quaternion.AngleAxis(wheel.angle, wheel.spinAxis);

                // 꺾은 다음 굴린다. 순서를 바꾸면 꺾인 바퀴가 차축이 아닌 축으로 돈다.
                wheel.transform.localRotation = wheel.baseRotation * steer * spin;
            }
        }

        public void ResetPose()
        {
            _shownSteer = 0f;

            if (!_initialized)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                Wheel wheel = wheels[i];
                if (wheel?.transform == null)
                {
                    continue;
                }

                wheel.angle = 0f;
                wheel.transform.localRotation = wheel.baseRotation;
            }
        }

        /// <summary>바퀴 렌더러의 세로 반 높이. 바퀴가 서 있는 상태(초기화 때)라 세로 크기가 곧 지름이다.</summary>
        private static float MeasureRadius(Transform wheel)
        {
            Renderer r = wheel.GetComponentInChildren<Renderer>();
            return r != null && r.bounds.extents.y > 0.01f ? r.bounds.extents.y : 0.35f;
        }

        private void OnDisable() => ResetPose();
    }
}
