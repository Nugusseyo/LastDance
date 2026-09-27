using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.MapSystems;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 퇴장 지점까지 빠져나간다. 먼저 차가 향한 쪽으로 곧게 빠져나온 뒤 퇴장 지점으로 튼다.</summary>
    [Serializable]
    public sealed class LeavingState : VisitState
    {
        [Tooltip("앞에 막는 차가 없는데도 이 시간(초) 동안 앞으로 나아가지 못하면 퇴장을 포기하고 방문을 닫는다. " +
                 "앞차가 떠나기를 기다리는 시간은 세지 않는다 — 한 줄로 선 차는 앞차가 빠져야 나갈 수 있다.")]
        [SerializeField, Min(0f)] private float phaseTimeout = 30f;

        [Tooltip("앞차를 기다리는 시간까지 모두 합쳐 퇴장에 쓸 수 있는 최대 시간(초). 없으면 앞차가 영영 안 빠질 때 방문이 닫히지 않는다.")]
        [SerializeField, Min(0f)] private float maxLeaveTime = 180f;

        [Tooltip("이만큼(m) 움직이면 앞으로 나아간 것으로 보고 제자리 시간을 0으로 되돌린다.")]
        [SerializeField, Min(0.1f)] private float progressDistance = 0.5f;

        [Tooltip("막는 차가 없는데 이 시간(초)마다 제자리면 차의 이동을 처음부터 다시 시킨다. 좁은 곳에서 조향이 후진·포기 상태에 " +
                 "걸려 그대로 서 버리는 경우를 푼다. 0이면 다시 시키지 않는다.")]
        [SerializeField, Min(0f)] private float restartInterval = 8f;

        [Tooltip("차 앞 이 거리(m) 안에 다른 차가 있으면 그 차를 기다리는 중으로 본다.")]
        [SerializeField, Min(0f)] private float blockerLookAhead = 4f;

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

        [Header("사운드")]
        [Tooltip("출발할 때 낼 소리(시동 거는 소리).")]
        [SerializeField] private SoundClipSo departSound;

        [Tooltip("앞차에 막혀 기다리는 동안 울릴 경적.")]
        [SerializeField] private SoundClipSo honkSound;

        [Tooltip("앞차에 막혀 이 시간(초)을 기다릴 때마다 경적을 한 번 울린다. 0이면 울리지 않는다.")]
        [SerializeField, Min(0f)] private float honkInterval = 4f;

        /// <summary>정면 → 오른쪽 → 왼쪽 순으로 본다. 오른쪽 먼저인 것은 추월과 같은 규칙이다.</summary>
        private static readonly float[] DepartSides = { 0f, 1f, -1f };

        /// <summary>차 정면과 퇴장 지점 방향이 이 코사인(약 70도) 이상 맞아야 곧게 빠져나온다.</summary>
        private const float MinDepartAlignment = 0.34f;

        public override VisitPhase Phase => VisitPhase.Leaving;

        public override void Enter(VisitContext context)
        {
            context.Car.Sound?.Play(departSound);

            Transform car = context.Car.transform;

            // 정면이 막혔으면(같은 줄 앞자리에 차가 있으면) 옆으로 비켜 빠져나온다.
            // 앞이 NavMesh 밖(벽·건물)이면 빠져나올 지점 없이 곧장 퇴장 지점으로 간다.
            // 내 위치에서 레이를 쏘지 않는다 — 주차 중인 차는 자기 NavMeshObstacle 구멍 안에 있어 늘 막힌다.
            // 차가 퇴장 지점을 등지고 있으면(입구를 가로막고 서 있던 차 등) 앞으로 빠져나오면 오히려 멀어져
            // 좁은 곳에서 전진·후진을 되풀이한다. 그럴 땐 아래 TryFindClearDepart로 줄을 피해 빠져나온다.
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

            // 퇴장 지점을 등진 차가(입구를 가로막고 옆으로 서 있던 차 등) 곧장 퇴장 지점으로 가면 NavMesh 최단 경로가
            // 주차 줄 사이 틈을 대각선으로 가로질러 서 있는 차에 끼인다. 줄에서 떨어진 길로 빠져나올 지점을 먼저 고른다.
            if (!facingExit && TryFindClearDepart(context, out Vector3 clearDepart))
            {
                context.DepartPoint = clearDepart;
                context.Departing = true;
                context.Car.MoveTo(clearDepart);
                return;
            }

            context.Departing = false;
            context.Car.MoveTo(context.ExitPoint);
        }

        /// <summary>빠져나올 방향 후보(차 정면 기준 각도). 곧바로 뒤는 후진이라 뺀다.</summary>
        private static readonly float[] ClearDepartAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };

        /// <summary>정면에서 45도 틀 때마다 더하는 비용(m). 크게 틀수록 좁은 곳에서 전진·후진을 되풀이한다.</summary>
        private const float TurnPenaltyPer45 = 3f;

        /// <summary>빠져나올 직선을 따라 차 NavMesh 위인지 보는 간격(m).</summary>
        private const float DepartProbeStep = 1f;

        /// <summary>점이 차 NavMesh에서 수평으로 이만큼(m) 안에 있어야 NavMesh 위로 본다. 차 기준점은 NavMesh보다 떠 있어 높이는 따지지 않는다.</summary>
        private const float MeshSnap = 0.5f;

        /// <summary>서 있는 차와 둘 여유(m). 차 폭의 절반쯤이다.</summary>
        private const float ParkedCarClearance = 1.5f;

        /// <summary>빠져나가는 길이 주차 자리(차 몸체 길이만큼의 선분)·주유 지점에서 이만큼(m)은 떨어져야 한다.
        /// 지금 빈 자리라도 곧 차가 들어오고, 줄 사이 틈은 차 한 대가 겨우 지나 걸리기 쉽다.</summary>
        private const float CarZoneClearance = 2.5f;

        /// <summary>주차 자리에 선 차 몸체 길이의 절반(m). 자리 지점을 이 길이의 선분으로 본다.</summary>
        private const float SlotHalfLength = 2.3f;

        private readonly NavMeshPath _departPath = new();

        /// <summary>차 정면 기준 여러 방향으로 <see cref="departDistance"/>만큼 빠져나올 지점을 만들어 보고, 거기까지와
        /// 거기서 퇴장 지점까지의 차 NavMesh 경로가 주차 자리·주유 지점·서 있는 차에서 떨어져 있는 것 중 가장 짧은 것을 고른다.
        /// 맵이 없으면 줄을 알 수 없어 고르지 않는다.</summary>
        private bool TryFindClearDepart(VisitContext context, out Vector3 depart)
        {
            depart = default;
            MapDataSo map = context.MapData;
            if (map == null || departDistance <= 0f)
            {
                return false;
            }

            Car car = context.Car;
            Transform t = car.transform;
            Vector3 origin = t.position;
            Vector3 forward = t.forward;
            forward.y = 0f;
            forward.Normalize();

            ICarTrafficSensor self = car.GetModule<ICarTrafficSensor>();
            float bodyRadius = self != null ? self.BoundingRadius : 2.5f;

            float bestScore = float.PositiveInfinity;
            for (int a = 0; a < ClearDepartAngles.Length; a++)
            {
                Vector3 dir = Quaternion.Euler(0f, ClearDepartAngles[a], 0f) * forward;
                Vector3 target = origin + dir * departDistance;

                if (!TrySampleCarMesh(target, out Vector3 point))
                {
                    continue;
                }

                // 빠져나오는 직선이 끊기지 않아야 한다(벽·주유기·다른 차의 구멍). 몸이 걸친 자리는 자기 구멍이라 뺀다.
                bool straight = true;
                for (float d = bodyRadius; d < departDistance; d += DepartProbeStep)
                {
                    if (!TrySampleCarMesh(origin + dir * d, out _))
                    {
                        straight = false;
                        break;
                    }
                }

                if (!straight ||
                    !IsClearOfParkedCars(origin, point, self) ||
                    !IsClearOfCarZones(map, origin, point))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(point, context.ExitPoint, CarNavMesh.Filter, _departPath) ||
                    _departPath.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                Vector3[] corners = _departPath.corners;
                float length = departDistance;
                bool clear = true;
                for (int i = 1; i < corners.Length && clear; i++)
                {
                    length += PlanarDistance(corners[i - 1], corners[i]);
                    clear = IsClearOfParkedCars(corners[i - 1], corners[i], self) &&
                            IsClearOfCarZones(map, corners[i - 1], corners[i]);
                }

                if (!clear)
                {
                    continue;
                }

                float score = length + Mathf.Abs(ClearDepartAngles[a]) / 45f * TurnPenaltyPer45;
                if (score < bestScore)
                {
                    bestScore = score;
                    depart = point;
                }
            }

            return bestScore < float.PositiveInfinity;
        }

        private static bool TrySampleCarMesh(Vector3 position, out Vector3 point)
        {
            if (CarNavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f) &&
                PlanarDistance(hit.position, position) <= MeshSnap)
            {
                point = hit.position;
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>이 속도(m/s)보다 느린 차는 서 있는 것으로 본다.</summary>
        private const float ParkedSpeed = 0.5f;

        /// <summary>from→to를 차 폭만큼 쓸고 지나가는 띠 안에 서 있는 차가 없는지. 움직이는 차와 자기 차는 뺀다.</summary>
        private static bool IsClearOfParkedCars(Vector3 from, Vector3 to, ICarTrafficSensor self)
        {
            const float step = 0.5f;

            Vector3 delta = to - from;
            delta.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / step));

            IReadOnlyList<ICarTrafficSensor> sensors = CarTraffic.Sensors;
            for (int s = 0; s < sensors.Count; s++)
            {
                ICarTrafficSensor sensor = sensors[s];
                if (ReferenceEquals(sensor, self))
                {
                    continue;
                }

                Vector3 velocity = sensor.Velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > ParkedSpeed * ParkedSpeed ||
                    CarTraffic.PlanarDistanceToSegment(sensor.Center, from, to) > ParkedCarClearance + sensor.BoundingRadius)
                {
                    continue;
                }

                for (int i = 0; i <= steps; i++)
                {
                    if (sensor.Overlaps(Vector3.Lerp(from, to, (float)i / steps), ParkedCarClearance))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static readonly MapPointType[] CarZones = { MapPointType.ParkingSlot, MapPointType.OilDispenser };

        /// <summary>from→to 직선이 주차 자리·주유 지점에서 <see cref="CarZoneClearance"/>만큼 떨어져 있는지.
        /// 주차 자리는 거기 선 차 몸체처럼 앞뒤로 긴 선분으로 본다.</summary>
        private static bool IsClearOfCarZones(MapDataSo map, Vector3 from, Vector3 to)
        {
            for (int i = 0; i < CarZones.Length; i++)
            {
                IReadOnlyList<MapPosition> points = map.GetAll(CarZones[i]);
                if (points == null)
                {
                    continue;
                }

                float halfLength = CarZones[i] == MapPointType.ParkingSlot ? SlotHalfLength : 0f;
                for (int j = 0; j < points.Count; j++)
                {
                    if (points[j] == null)
                    {
                        continue;
                    }

                    Vector3 center = points[j].Position;
                    Vector3 axis = points[j].transform.forward * halfLength;
                    if (SegmentDistance(from, to, center - axis, center + axis) < CarZoneClearance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>높이를 무시한 두 선분 사이 거리.</summary>
        private static float SegmentDistance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            if (SegmentsCross(a0, a1, b0, b1))
            {
                return 0f;
            }

            return Mathf.Min(
                Mathf.Min(CarTraffic.PlanarDistanceToSegment(a0, b0, b1), CarTraffic.PlanarDistanceToSegment(a1, b0, b1)),
                Mathf.Min(CarTraffic.PlanarDistanceToSegment(b0, a0, a1), CarTraffic.PlanarDistanceToSegment(b1, a0, a1)));
        }

        private static bool SegmentsCross(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            static float Cross(Vector3 o, Vector3 p, Vector3 q) => (p.x - o.x) * (q.z - o.z) - (p.z - o.z) * (q.x - o.x);

            float d1 = Cross(b0, b1, a0);
            float d2 = Cross(b0, b1, a1);
            float d3 = Cross(a0, a1, b0);
            float d4 = Cross(a0, a1, b1);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            context.PhaseElapsed += dt;
            bool stalled = UpdateStall(context, dt);

            if (context.Departing)
            {
                // 빠져나올 지점에 닿기 전에 목적지를 바꾼다. 닿고 나서 바꾸면 거기서 한 번 서게 된다.
                bool near = PlanarDistance(context.Car.transform.position, context.DepartPoint) <= departSwitchDistance;
                if (near || context.Car.IsArrived || !context.Car.HasCompletePath)
                {
                    context.Departing = false;
                    context.Car.MoveTo(context.ExitPoint);
                }
                else if (stalled)
                {
                    // 빠져나오는 길이 막혀(앞뒤로 차에 갇혀) 한 발짝도 못 나간 경우다. 제한 시간을 여기서도 지켜야
                    // 방문이 영영 닫히지 않아 자리까지 묶이는 일을 막는다.
                    Debug.LogWarning($"[Leaving] {context.Car.name}이(가) 자리에서 빠져나오지 못해 방문을 강제로 종료합니다. 앞뒤 자리가 막혔는지 확인하세요.", context.Car);
                    return VisitPhase.Completed;
                }

                return VisitPhase.Leaving;
            }

            // 퇴장 지점은 차가 사라지는 곳이라 정확히 닿을 필요가 없다. 조금 비껴 지나친 차가
            // 거기에 맞추려고 제자리에서 핸들만 꺾으며 서 있지 않게 반경 안이면 끝낸다.
            if (context.Car.IsArrived || PlanarDistance(context.Car.transform.position, context.ExitPoint) <= exitRadius)
            {
                return VisitPhase.Completed;
            }

            // 경로가 잠깐 끊기는 건 옆에 차가 서거나 떠나며 NavMesh를 다시 깎을 때 흔하다. 곧바로 포기하지 않고
            // 제자리 시간으로만 판단한다 — 끊긴 채 못 움직이면 결국 stalled가 된다.
            if (!stalled)
            {
                return VisitPhase.Leaving;
            }

            // 퇴장 실패는 회복할 방법이 없다. 방문을 닫아 차·손님·자리를 회수하는 편이 낫다.
            Debug.LogWarning(context.PhaseElapsed >= maxLeaveTime
                    ? $"[Leaving] {context.Car.name}이(가) {maxLeaveTime}초 안에 퇴장하지 못해 방문을 강제로 종료합니다."
                    : $"[Leaving] {context.Car.name}이(가) 앞차를 기다리는 것도 아닌데(서로 막은 교착 포함) {phaseTimeout}초 동안 나아가지 못해 방문을 강제로 종료합니다. 퇴장 지점이 NavMesh 위에 있는지 확인하세요.",
                context.Car);

            return VisitPhase.Completed;
        }

        /// <summary>퇴장을 포기해야 하는지. 앞으로 나아가면 제자리 시간을 되돌리고, 앞차에 막혀 있는 동안은 세지 않는다.
        /// 한 줄로 선 차들 중 뒤차는 앞차가 떠나야만 나갈 수 있어서, 고정 시간으로 자르면 멀쩡히 기다리던 차가 끊긴다.</summary>
        private bool UpdateStall(VisitContext context, float dt)
        {
            if (maxLeaveTime > 0f && context.PhaseElapsed >= maxLeaveTime)
            {
                return true;
            }

            Vector3 position = context.Car.transform.position;
            if (context.PhaseElapsed <= dt || PlanarDistance(position, context.LeaveProgressPoint) >= progressDistance)
            {
                context.LeaveProgressPoint = position;
                context.LeaveStallElapsed = 0f;
                context.LeaveHonkElapsed = 0f;
                return false;
            }

            if (IsBlockedAhead(context.Car))
            {
                // 앞차를 기다리는 중이다. 한동안 안 비키면 경적을 울린다.
                context.LeaveHonkElapsed += dt;
                if (honkInterval > 0f && context.LeaveHonkElapsed >= honkInterval)
                {
                    context.LeaveHonkElapsed = 0f;
                    context.Car.Sound?.Play(honkSound);
                }
            }
            else
            {
                context.LeaveHonkElapsed = 0f;
                context.LeaveStallElapsed += dt;

                // 막는 차도 없는데 서 있다. 조향이 좁은 곳에서 굳었을 수 있으니 이동을 처음부터 다시 시킨다.
                if (restartInterval > 0f && context.LeaveStallElapsed >= restartInterval * (context.LeaveRestarts + 1))
                {
                    context.LeaveRestarts++;
                    Vector3 target = context.Departing ? context.DepartPoint : context.ExitPoint;
                    Debug.Log($"[Leaving] {context.Car.name}이(가) {context.LeaveStallElapsed:F0}초째 제자리라 다시 출발시킵니다({context.LeaveRestarts}번째).", context.Car);
                    context.Car.Stop();
                    context.Car.MoveTo(target);
                }
            }

            return phaseTimeout > 0f && context.LeaveStallElapsed >= phaseTimeout;
        }

        /// <summary>다른 차를 기다리는 중인지. 차의 교통 센서가 지금 양보하고 있는 차가 있으면 그렇다 — 센서는 차가 실제로 틀어
        /// 나아갈 쪽을 보므로, 방향을 틀며 나오는 차가 옆 차를 기다리는 것도 잡는다. 센서가 아직 판단하지 않았을 때를 위해
        /// 차 정면 띠 안에 다른 차가 있는지도 본다 — 한 줄로 선 차가 막히는 곳은 대개 앞이다.</summary>
        private bool IsBlockedAhead(Car car)
        {
            ICarTrafficSensor self = car.GetModule<ICarTrafficSensor>();
            if (self?.Blocker != null)
            {
                // 상대도 나를 기다리면 교착이다. 기다려도 풀리지 않으니 기다리는 중으로 보지 않는다 —
                // 그래야 제자리 시간이 쌓여 다시 출발하거나, 끝내 못 가면 방문을 닫아 뒤차들을 풀어 준다.
                return !ReferenceEquals(self.Blocker.Blocker, self);
            }

            if (blockerLookAhead <= 0f)
            {
                return false;
            }

            Vector3 forward = car.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 center = self != null ? self.Center : car.transform.position;
            float halfLength = self != null ? self.BoundingRadius * 0.9f : 3f;
            Vector3 from = center + forward * halfLength;
            Vector3 to = from + forward * blockerLookAhead;

            return !CarTraffic.IsSegmentClear(from, to, 1f, self);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
