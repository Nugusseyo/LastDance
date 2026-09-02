using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 정차 지점까지 들어온다.</summary>
    public sealed class ArrivingState : IVisitState
    {
        /// <summary>
        /// 정차 지점 앞에 두는 진입점까지의 거리(m).
        /// 차는 여기를 먼저 찍고 자리 정면으로 곧게 들어오므로, 도착했을 때 이미 방향이 맞아 있다.
        /// 자리 앞이 좁아 진입점이 NavMesh 밖으로 나가면 이 단계는 통째로 건너뛴다.
        /// </summary>
        private const float ApproachDistance = 9f;

        /// <summary>진입점을 NavMesh 위에서 찾을 때 허용할 오차(m).</summary>
        private const float ApproachSampleRadius = 2f;

        public VisitPhase Phase => VisitPhase.Arriving;

        /// <summary>도착해서 멈춘 뒤, 남은 각도를 마저 맞추는 중인지.</summary>
                private bool _aligning;

        /// <summary>
        /// 이번 주차에서 실제로 맞출 방향. 자리 회전 그대로일 수도, 180도 뒤집힌 것일 수도 있다.
        /// 전면·후면 주차를 둘 다 허용하므로 어느 쪽이든 자리에 나란히 서기만 하면 된다.
        /// </summary>
        private Quaternion _targetRotation = Quaternion.identity;

        public void Enter(VisitContext context)
        {
            _aligning = false;

            // 자리 정면으로 ApproachDistance만큼 물러난 지점을 경유지로 넘긴다.
            // 목적지를 따로 끊어 주지 않으므로 차는 중간에서 멈추지 않고,
            // 마지막 직선 구간을 달리는 동안 방향이 저절로 맞는다.
            //
            // 진입 방향을 하나로 못 박으면, 반대편에서 온 차는 자리에 선 뒤
            // 제자리에서 한 바퀴 돌아야 한다. 전면·후면 주차가 상관없으므로
            // 지금 위치에서 가까운 쪽으로 들어가고, 그때의 방향을 그대로 목표로 삼는다.
            Quaternion forwardIn = context.ArrivalRotation;
            Quaternion backIn = context.ArrivalRotation * Quaternion.Euler(0f, 180f, 0f);

            bool hasForward = TryGetApproachPoint(context.ArrivalPoint, forwardIn, out Vector3 forwardApproach);
            bool hasBack = TryGetApproachPoint(context.ArrivalPoint, backIn, out Vector3 backApproach);

            if (hasForward && hasBack)
            {
                Vector3 carPosition = context.Car.transform.position;
                bool preferBack = PlanarSqrDistance(carPosition, backApproach) <
                                  PlanarSqrDistance(carPosition, forwardApproach);

                _targetRotation = preferBack ? backIn : forwardIn;
                context.Car.MoveTo(context.ArrivalPoint, preferBack ? backApproach : forwardApproach);
                return;
            }

            if (hasForward)
            {
                _targetRotation = forwardIn;
                context.Car.MoveTo(context.ArrivalPoint, forwardApproach);
                return;
            }

            if (hasBack)
            {
                _targetRotation = backIn;
                context.Car.MoveTo(context.ArrivalPoint, backApproach);
                return;
            }

            // 양쪽 진입점을 다 못 잡으면 곧장 자리로 간다. 방향은 AlignTo가 마저 맞춘다.
            _targetRotation = forwardIn;
            context.Car.MoveTo(context.ArrivalPoint);
        }

        /// <summary>자리에서 rotation 정면으로 물러난 진입점을 NavMesh 위에서 찾는다.</summary>
        private static bool TryGetApproachPoint(Vector3 arrivalPoint, Quaternion rotation, out Vector3 point)
        {
            Vector3 candidate = arrivalPoint - rotation * Vector3.forward * ApproachDistance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, ApproachSampleRadius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            point = candidate;
            return false;
        }

        /// <summary>높이를 무시한 거리의 제곱. 어느 쪽이 가까운지만 보면 되므로 제곱근을 쓰지 않는다.</summary>
        private static float PlanarSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            if (!_aligning)
            {
                if (!context.Car.IsArrived)
                {
                    return VisitPhase.Arriving;
                }

                // NavMesh가 회전을 되돌리지 않도록 먼저 멈춘 뒤에 방향을 맞춘다.
                context.Car.Stop();

                // 안전망. 어떤 이유로든 반대로 도착했다면 여기서 뒤집힌 쪽을 고른다.
                // 덕분에 남는 각도가 항상 90도 이하라 제자리에서 크게 돌 일이 없다.
                _targetRotation = NearerFacing(context.Car.transform.rotation, _targetRotation);
                _aligning = true;
            }

            if (!context.Car.AlignTo(_targetRotation, dt))
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
