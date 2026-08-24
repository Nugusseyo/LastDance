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

        /// <summary>자리 정면으로 들어오는 마지막 구간에 접어들기 전인지.</summary>
        private bool _approaching;

        /// <summary>도착해서 멈춘 뒤, 남은 각도를 마저 맞추는 중인지.</summary>
        private bool _aligning;

        public void Enter(VisitContext context)
        {
            _aligning = false;
            _approaching = false;

            // 자리 정면으로 ApproachDistance만큼 물러난 지점.
            Vector3 forward = context.ArrivalRotation * Vector3.forward;
            Vector3 approach = context.ArrivalPoint - forward * ApproachDistance;

            if (NavMesh.SamplePosition(approach, out NavMeshHit hit, ApproachSampleRadius, NavMesh.AllAreas))
            {
                _approaching = true;
                context.Car.MoveTo(hit.position);
                return;
            }

            // 진입점을 못 잡으면 예전처럼 곧장 자리로 간다. 방향은 AlignTo가 마저 맞춘다.
            context.Car.MoveTo(context.ArrivalPoint);
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            if (_approaching)
            {
                if (!context.Car.IsArrived)
                {
                    return VisitPhase.Arriving;
                }

                // 진입점에 닿았다. 여기서부터 자리까지는 직선이라 달리는 동안 방향이 저절로 맞는다.
                _approaching = false;
                context.Car.MoveTo(context.ArrivalPoint);

                return VisitPhase.Arriving;
            }

            if (!_aligning)
            {
                if (!context.Car.IsArrived)
                {
                    return VisitPhase.Arriving;
                }

                // NavMesh가 회전을 되돌리지 않도록 먼저 멈춘 뒤에 방향을 맞춘다.
                // 진입점을 거쳐 왔다면 남은 각도가 몇 도뿐이라 눈에 띄지 않는다.
                context.Car.Stop();
                _aligning = true;
            }

            if (!context.Car.AlignTo(context.ArrivalRotation, dt))
            {
                return VisitPhase.Arriving;
            }

            return VisitPhase.Unloading;
        }
    }
}
