using System;
using _Works.CJW.Scripts.Cars;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 정차 지점까지 들어온다.</summary>
    [Serializable]
    public sealed class ArrivingState : VisitState
    {
        [Tooltip("정차 지점 앞에 두는 진입점까지의 거리(m). 여길 먼저 찍고 자리 정면으로 곧게 들어와 도착 시 방향이 맞아 있게 한다.")]
        [SerializeField, Min(0f)] private float approachDistance = ParkingApproach.DefaultDistance;

        [Tooltip("진입점을 NavMesh 위에서 찾을 때 허용할 오차(m).")]
        [SerializeField, Min(0f)] private float approachSampleRadius = ParkingApproach.DefaultSampleRadius;

        [Tooltip("진입점에서 자리까지의 직선 위에 이 반경(m) 안으로 차가 있으면 그 방향으로는 들어가지 않는다. 차 반폭에 여유를 더한 값.")]
        [SerializeField, Min(0f)] private float approachClearRadius = ParkingApproach.DefaultClearRadius;

        [Tooltip("켜면 자리 뒤쪽에서 후면 주차로도 들어온다. 끄면 모든 차가 자리 정면 한 방향으로만 들어와 일방통행이 된다. " +
                 "양방향을 허용하면 접근로 하나에서 들어오는 차끼리 마주쳐 멈춘다.")]
        [SerializeField] private bool allowBackIn = ParkingApproach.DefaultAllowBackIn;

        [Tooltip("이 단계에 머물 수 있는 한계 시간(초). 없으면 막힌 세션이 주차 자리를 영영 반납하지 않아 스폰까지 멈춘다.")]
        [SerializeField, Min(0f)] private float phaseTimeout = 45f;

        public override VisitPhase Phase => VisitPhase.Arriving;

        public override void Enter(VisitContext context)
        {
            // 자리 정면으로 approachDistance만큼 물러난 지점을 경유지로 넘긴다.
            // 목적지를 따로 끊어 주지 않으므로 차는 중간에서 멈추지 않고,
            // 마지막 직선 구간을 달리는 동안 방향이 저절로 맞는다.
            //
            // 진입 방향을 하나로 못 박으면, 반대편에서 온 차는 자리에 선 뒤
            // 제자리에서 한 바퀴 돌아야 한다. 전면·후면 주차가 상관없으므로
            // 지금 위치에서 가까운 쪽으로 들어가고, 그때의 방향을 그대로 목표로 삼는다.
            Quaternion forwardIn = ParkingApproach.ForwardIn(context.ArrivalRotation);
            Quaternion backIn = ParkingApproach.BackIn(context.ArrivalRotation);

            Vector3 backApproach = default;
            bool hasForward = ParkingApproach.TryGetPoint(context.ArrivalPoint, forwardIn, approachDistance,
                                                          approachSampleRadius, out Vector3 forwardApproach);
            bool hasBack = allowBackIn &&
                           ParkingApproach.TryGetPoint(context.ArrivalPoint, backIn, approachDistance,
                                                       approachSampleRadius, out backApproach);

            // 마지막 직선은 NavMesh를 보지 않고 추월도 하지 않는다. 그 위에 차가 서 있으면 뒤에서 영영 기다리므로
            // 막히지 않은 쪽이 있으면 거리와 상관없이 그쪽으로 들어간다.
            ICarTrafficSensor self = context.Car.GetModule<ICarTrafficSensor>();
            bool forwardClear = hasForward &&
                                ParkingApproach.IsLegClear(forwardApproach, context.ArrivalPoint, approachClearRadius, self);
            bool backClear = hasBack &&
                             ParkingApproach.IsLegClear(backApproach, context.ArrivalPoint, approachClearRadius, self);

            if (forwardClear != backClear)
            {
                hasForward = forwardClear;
                hasBack = backClear;
            }
            else if (!forwardClear && (hasForward || hasBack))
            {
                Debug.LogWarning($"[Arriving] {context.Car.name}의 자리로 들어가는 직선이 양쪽 다 다른 차에 막혀 있습니다. 가까운 쪽으로 들어가 기다립니다.", context.Car);
            }

            if (hasForward && hasBack)
            {
                Vector3 carPosition = context.Car.transform.position;
                bool preferBack = PlanarSqrDistance(carPosition, backApproach) <
                                  PlanarSqrDistance(carPosition, forwardApproach);

                context.TargetRotation = preferBack ? backIn : forwardIn;
                context.Car.MoveTo(context.ArrivalPoint, preferBack ? backApproach : forwardApproach);
                return;
            }

            if (hasForward)
            {
                context.TargetRotation = forwardIn;
                context.Car.MoveTo(context.ArrivalPoint, forwardApproach);
                return;
            }

            if (hasBack)
            {
                context.TargetRotation = backIn;
                context.Car.MoveTo(context.ArrivalPoint, backApproach);
                return;
            }

            // 양쪽 진입점을 다 못 잡으면 곧장 자리로 간다. 방향은 AlignTo가 마저 맞춘다.
            context.TargetRotation = forwardIn;
            context.Car.MoveTo(context.ArrivalPoint);
        }

        /// <summary>높이를 무시한 거리의 제곱. 어느 쪽이 가까운지만 보면 되므로 제곱근을 쓰지 않는다.</summary>
        private static float PlanarSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            context.PhaseElapsed += dt;

            if (!context.Aligning)
            {
                if (!context.Car.IsArrived)
                {
                    // 경로가 자리에 닿지 않으면 기다려도 달라지지 않는다. 한계 시간을 채우지 않고 넘긴다.
                    bool unreachable = !context.Car.HasCompletePath;

                    if (!unreachable && context.PhaseElapsed < phaseTimeout)
                    {
                        return VisitPhase.Arriving;
                    }

                    Debug.LogWarning(unreachable
                            ? $"[Arriving] {context.Car.name}의 경로가 자리에 닿지 않습니다. 자리와 NavMesh를 확인하세요. 서 있는 자리에서 그대로 진행합니다."
                            : $"[Arriving] {context.Car.name}이(가) {phaseTimeout}초 안에 자리에 들어가지 못해 그대로 진행합니다.",
                        context.Car);
                }

                // NavMesh가 회전을 되돌리지 않도록 먼저 멈춘 뒤에 방향을 맞춘다.
                context.Car.Stop();

                // 안전망. 어떤 이유로든 반대로 도착했다면 여기서 뒤집힌 쪽을 고른다.
                // 덕분에 남는 각도가 항상 90도 이하라 제자리에서 크게 돌 일이 없다.
                context.TargetRotation = NearerFacing(context.Car.transform.rotation, context.TargetRotation);
                context.Aligning = true;
            }

            if (!context.Car.AlignTo(context.TargetRotation, dt))
            {
                return VisitPhase.Arriving;
            }

            return VisitPhase.Unloading;
        }

        /// <summary>현재 방향에서 덜 돌아도 되는 쪽(정방향 / 180도 뒤집힘)을 고른다.</summary>
        private static Quaternion NearerFacing(Quaternion current, Quaternion target)
        {
            Quaternion flipped = target * Quaternion.Euler(0f, 180f, 0f);

            return Quaternion.Angle(current, flipped) < Quaternion.Angle(current, target) ? flipped : target;
        }
    }
}
