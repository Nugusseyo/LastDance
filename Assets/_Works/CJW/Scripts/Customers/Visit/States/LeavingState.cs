using System;
using _Works.CJW.Scripts.Cars;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 퇴장 지점까지 빠져나간다. 먼저 차가 향한 쪽으로 곧게 빠져나온 뒤 퇴장 지점으로 튼다.</summary>
    [Serializable]
    public sealed class LeavingState : VisitState
    {
        [Tooltip("퇴장에 쓸 수 있는 한계 시간(초). 없으면 막힌 방문이 Completed에 도달하지 못해 자원이 회수되지 않는다.")]
        [SerializeField, Min(0f)] private float phaseTimeout = 30f;

        [Tooltip("퇴장 지점에서 이 반경(m) 안에 들어오면 다 빠져나간 것으로 본다.")]
        [SerializeField, Min(0f)] private float exitRadius = 6f;

        [Tooltip("자리에서 차가 향한 쪽으로 이만큼(m) 곧게 빠져나온 뒤 퇴장 지점으로 간다. 0이면 곧장 퇴장 지점으로 간다. " +
                 "곧장 가면 NavMesh 최단 경로가 주유기 사이나 다른 줄 옆 좁은 틈을 가로질러 주차된 차에 걸린다.")]
        [SerializeField, Min(0f)] private float departDistance = 7f;

        [Tooltip("빠져나올 지점에 이만큼(m) 가까워지면 멈추지 않고 퇴장 지점으로 목적지를 바꾼다.")]
        [SerializeField, Min(0.5f)] private float departSwitchDistance = 3f;

        [Tooltip("정면이 막혔을 때 옆으로 비켜 빠져나올 거리(m).")]
        [SerializeField, Min(0f)] private float departSideOffset = 3f;

        [Tooltip("빠져나올 지점에서 이 반경(m) 안에 차가 있으면 그 쪽으로는 빠져나오지 않는다.")]
        [SerializeField, Min(0f)] private float departClearRadius = 2.5f;

        /// <summary>정면 → 오른쪽 → 왼쪽 순으로 본다. 오른쪽 먼저인 것은 추월과 같은 규칙이다.</summary>
        private static readonly float[] DepartSides = { 0f, 1f, -1f };

        /// <summary>차 정면과 퇴장 지점 방향이 이 코사인(약 70도) 이상 맞아야 곧게 빠져나온다.</summary>
        private const float MinDepartAlignment = 0.34f;

        public override VisitPhase Phase => VisitPhase.Leaving;

        public override void Enter(VisitContext context)
        {
            Transform car = context.Car.transform;

            // 정면이 막혔으면(같은 줄 앞자리에 차가 있으면) 옆으로 비켜 빠져나온다.
            // 앞이 NavMesh 밖(벽·건물)이면 빠져나올 지점 없이 곧장 퇴장 지점으로 간다.
            // 내 위치에서 레이를 쏘지 않는다 — 주차 중인 차는 자기 NavMeshObstacle 구멍 안에 있어 늘 막힌다.
            // 차가 퇴장 지점을 등지고 있으면(입구를 가로막고 서 있던 차 등) 앞으로 빠져나오면 오히려 멀어져
            // 좁은 곳에서 전진·후진을 되풀이한다. 그럴 땐 곧장 퇴장 지점으로 간다.
            Vector3 toExit = context.ExitPoint - car.position;
            toExit.y = 0f;
            Vector3 facing = car.forward;
            facing.y = 0f;
            bool facingExit = toExit.sqrMagnitude > 1e-4f &&
                              Vector3.Dot(facing.normalized, toExit.normalized) >= MinDepartAlignment;

            if (departDistance > 0f && facingExit)
            {
                for (int i = 0; i < DepartSides.Length; i++)
                {
                    Vector3 depart = car.position + car.forward * departDistance + car.right * (DepartSides[i] * departSideOffset);

                    if (!CarNavMesh.SamplePosition(depart, out NavMeshHit hit, 1f))
                    {
                        continue;
                    }

                    if (!Cars.CarTraffic.IsAreaClear(hit.position, departClearRadius))
                    {
                        continue;
                    }

                    context.DepartPoint = hit.position;
                    context.Departing = true;
                    context.Car.MoveTo(hit.position);
                    return;
                }
            }

            context.Departing = false;
            context.Car.MoveTo(context.ExitPoint);
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            context.PhaseElapsed += dt;

            if (context.Departing)
            {
                // 빠져나올 지점에 닿기 전에 목적지를 바꾼다. 닿고 나서 바꾸면 거기서 한 번 서게 된다.
                bool near = PlanarDistance(context.Car.transform.position, context.DepartPoint) <= departSwitchDistance;
                if (near || context.Car.IsArrived || !context.Car.HasCompletePath)
                {
                    context.Departing = false;
                    context.Car.MoveTo(context.ExitPoint);
                }

                return VisitPhase.Leaving;
            }

            // 퇴장 지점은 차가 사라지는 곳이라 정확히 닿을 필요가 없다. 조금 비껴 지나친 차가
            // 거기에 맞추려고 제자리에서 핸들만 꺾으며 서 있지 않게 반경 안이면 끝낸다.
            if (context.Car.IsArrived || PlanarDistance(context.Car.transform.position, context.ExitPoint) <= exitRadius)
            {
                return VisitPhase.Completed;
            }

            if (context.PhaseElapsed < phaseTimeout && context.Car.HasCompletePath)
            {
                return VisitPhase.Leaving;
            }

            // 퇴장 실패는 회복할 방법이 없다. 방문을 닫아 차·손님·자리를 회수하는 편이 낫다.
            Debug.LogWarning($"[Leaving] {context.Car.name}이(가) 퇴장하지 못해 방문을 강제로 종료합니다. 퇴장 지점이 NavMesh 위에 있는지 확인하세요.", context.Car);

            return VisitPhase.Completed;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
