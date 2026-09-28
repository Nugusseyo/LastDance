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

            // 같은 줄 앞자리에 차가 아직 서 있다(주유를 먼저 받은 뒤차가 먼저 떠나는 경우). 곧장 퇴장 지점으로 가면
            // 앞차를 곧 움직일 차로 보고 그 뒤에 선 채 앞차가 떠날 때까지 기다린다. 줄 옆 빈 곳으로 먼저 비켜 나온다.
            if (TryEscapeParkedBlocker(context))
            {
                return;
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

        /// <summary>앞을 막은 차가 서 있다고 볼 거리(m). 차 앞 범퍼에서 이 안에 선 차가 있으면 곧게 빠져나갈 수 없다.</summary>
        private const float ParkedBlockerLookAhead = 6f;

        /// <summary>앞차 반경에 더해 옆으로 이만큼(m) 안이면 앞을 막은 것으로 본다. 내 차 폭의 절반쯤.</summary>
        private const float ParkedBlockerSideMargin = 1.5f;

        /// <summary>줄 옆으로 비켜 나올 지점의 옆 거리(m) 후보.</summary>
        private static readonly float[] EscapeSideDistances = { 4.5f, 5.5f, 6.5f };


        /// <summary>줄 옆으로 비켜 나올 지점의 앞뒤 위치(m) 후보. 모두 차 뒤쪽이다 — 앞차와 범퍼 사이가 한두 m뿐이라
        /// 앞으로 틀면 곧바로 앞차에 걸려 멈춘다. 뒤쪽 옆을 겨누면 이동 모듈이 먼저 후진해 각을 벌린 뒤 빠져나온다.</summary>
        private static readonly float[] EscapeForwardOffsets = { -3.5f, -5f, -2f };

        /// <summary>후진으로 물러날 수 있는지 볼 거리(m). 뒤에도 차가 서 있으면 앞뒤로 갇혀 비켜 나올 수 없다.</summary>
        private const float EscapeReverseCheck = 5f;

        /// <summary>옆으로 6m보다 덜 비키는 만큼 1m마다 더하는 비용(m).</summary>
        private const float EscapeTightPenalty = 6f;

        /// <summary>서 있는 앞차에 막혀 있으면 빠져나갈 계획을 세워 출발시킨다. 막혀 있지 않으면 false.
        /// 앞차와 붙어 있으면 먼저 곧게 물러나 사이를 벌리고, 줄 옆 빈 곳이 있으면 그리로, 없으면 곧장 퇴장 지점으로 간다.</summary>
        private bool TryEscapeParkedBlocker(VisitContext context)
        {
            if (!IsBlockedByParkedCar(context.Car, out float frontGap, out ICarTrafficSensor blocker))
            {
                return false;
            }

            // 앞차와 붙어 있으면 핸들을 다 꺾어도 앞차 옆으로 돌 각이 안 나온다. 먼저 곧게 물러나 사이를 벌린다.
            float needed = PassGap - frontGap;
            float backOff = CanReverse(context.Car, out float room) ? Mathf.Clamp(needed, 0f, room) : 0f;

            // 뒤에도 차가 서 있어 거의 못 물러선다(두 차 사이에 낀 차). 제자리에서 앞뒤로 오가며 열린 옆쪽으로 차를 튼 뒤 빠져나간다.
            if (needed > 0.5f && backOff < Mathf.Min(needed, MinUsefulBackOff) && TryChooseOpenSide(context.Car, out Vector3 turnTo))
            {
                context.Departing = false;
                context.Car.MoveTo(context.ExitPoint);
                context.Car.TurnInPlace(turnTo);
                Debug.Log($"[Leaving] {context.Car.name}이(가) 앞뒤 차 사이에 끼어(앞 {frontGap:F1}m, 물러설 수 있는 거리 {room:F1}m) 제자리에서 옆으로 틀어 나갑니다.", context.Car);
                return true;
            }

            if (TryFindSideEscape(context, out Vector3 escape))
            {
                context.DepartPoint = escape;
                context.Departing = true;
                context.Car.MoveTo(escape);
            }
            else if (TryFindPassLane(context.Car, blocker, context.ExitPoint, out Vector3 passFrom, out Vector3 passTo))
            {
                // 앞차 옆 틈으로 곧장 퇴장 지점을 겨누면 NavMesh 경로가 앞차 모서리에 바짝 붙어 꺾이고, 조향이 그 모서리를
                // 더 깎아 교통 센서가 앞차를 막는 차로 잡고 선다. 앞차 옆을 나란히 지나는 직선을 따라가게 한다.
                context.DepartPoint = passTo;
                context.Departing = true;
                context.Car.MoveTo(passTo, passFrom);
                Debug.Log($"[Leaving] {context.Car.name}이(가) 앞차 옆으로 나란히 지나 빠져나갑니다. ({passFrom.x:F1},{passFrom.z:F1}) → ({passTo.x:F1},{passTo.z:F1})", context.Car);
            }
            else
            {
                context.Departing = false;
                context.Car.MoveTo(context.ExitPoint);
            }

            if (backOff > 0.5f)
            {
                context.Car.BackOff(backOff);
                Debug.Log($"[Leaving] {context.Car.name}이(가) 앞차와 {frontGap:F1}m 붙어 있어 {backOff:F1}m 물러났다가 나갑니다.", context.Car);
            }

            return true;
        }

        /// <summary>이보다 적게밖에 못 물러서면 물러서기로는 앞차 옆으로 돌 각이 안 나온다고 보고 제자리 회전을 한다(m).</summary>
        private const float MinUsefulBackOff = 2.5f;

        /// <summary>제자리에서 틀 때 옆쪽에서 앞으로 기울일 각도(도). 90도면 옆으로 곧게, 작을수록 앞쪽으로 비스듬히.</summary>
        private const float TurnOutAngle = 65f;

        /// <summary>차 좌우 중 빠져나갈 수 있는 쪽을 고른다. 옆으로 2.5~4.5m 지점이 차 NavMesh 위이고 비어 있으며 내 자리에서 끊기지 않고 닿는 곳이
        /// 많은 쪽. 고른 쪽으로 <see cref="TurnOutAngle"/>만큼 튼 방향을 돌려준다.</summary>
        private static bool TryChooseOpenSide(Car car, out Vector3 direction)
        {
            direction = default;
            Transform t = car.transform;
            Vector3 forward = t.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 origin = t.position;
            if (CarNavMesh.SamplePosition(origin, out NavMeshHit ground, 2f))
            {
                origin = ground.position;
            }

            int bestScore = 0;
            int bestSide = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                int score = 0;
                for (float lateral = 2.5f; lateral <= 4.6f; lateral += 1f)
                {
                    for (float ahead = -1f; ahead <= 1.1f; ahead += 2f)
                    {
                        Vector3 candidate = origin + right * (side * lateral) + forward * ahead;
                        if (!TrySampleCarMesh(candidate, out Vector3 point) || !CarTraffic.IsAreaClear(point, 1.2f))
                        {
                            continue;
                        }

                        // 내 자리는 서 있던 동안 내 구멍이 나 있을 수 있다. 거기서 끊긴 건 넘긴다.
                        if (CarNavMesh.Raycast(point, origin, out NavMeshHit hit) && PlanarDistance(hit.position, origin) > 2.2f)
                        {
                            continue;
                        }

                        score++;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSide = side;
                }
            }

            if (bestSide == 0)
            {
                return false;
            }

            direction = Quaternion.AngleAxis(bestSide * TurnOutAngle, Vector3.up) * forward;
            return true;
        }

        /// <summary>서 있는 앞차에 막혀 제자리인 채 이 시간(초)이 지나면 빠져나갈 계획을 다시 세운다.
        /// 물러서다 뒤로 들어오는 차에 막히는 등 첫 계획이 틀어져도, 그 차가 지나가면 다시 물러서 빠져나간다.</summary>
        private const float EscapeRetryInterval = 4f;

        /// <summary>앞차 중심까지 이만큼(m)은 떨어져야 앞차 옆으로 돌아 나갈 각이 나온다. 8m 간격 줄에서 1m만 물러서면 앞차 옆구리에 다시 걸렸다.</summary>
        private const float PassGap = 11f;

        /// <summary>물러설 때 뒤에 비어 있어야 하는 최대 거리(m).</summary>
        private const float MaxBackOff = 6f;

        /// <summary>뒤로 얼마나 물러설 수 있는지. 뒤 범퍼 뒤로 차가 있으면 그 앞까지만.</summary>
        private static bool CanReverse(Car car, out float room)
        {
            ICarTrafficSensor self = car.GetModule<ICarTrafficSensor>();
            Transform t = car.transform;
            Vector3 forward = t.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 rearBumper = (self != null ? self.Center : t.position) - forward * (self != null ? self.BoundingRadius * 0.9f : 2.5f);

            for (room = MaxBackOff; room >= 1f; room -= 1f)
            {
                // 물러선 뒤에도 뒤차와 1m는 떨어져 있어야 한다.
                if (CarTraffic.IsSegmentClear(rearBumper, rearBumper - forward * (room + 1f), 1.2f, self) &&
                    TrySampleCarMesh(t.position - forward * room, out _))
                {
                    return true;
                }
            }

            room = 0f;
            return false;
        }

        /// <summary>차 정면 바로 앞에 서 있는 차가 있는지. 움직이는 차는 곧 비키니 뺀다. <paramref name="gap"/>은 앞차 중심까지의 앞쪽 거리(m).</summary>
        private static bool IsBlockedByParkedCar(Car car, out float gap, out ICarTrafficSensor blocker)
        {
            gap = float.PositiveInfinity;
            blocker = null;
            ICarTrafficSensor self = car.GetModule<ICarTrafficSensor>();
            Transform t = car.transform;
            Vector3 forward = t.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 center = self != null ? self.Center : t.position;
            float halfLength = self != null ? self.BoundingRadius * 0.9f : 3f;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            IReadOnlyList<ICarTrafficSensor> sensors = CarTraffic.Sensors;
            for (int i = 0; i < sensors.Count; i++)
            {
                ICarTrafficSensor other = sensors[i];
                if (ReferenceEquals(other, self))
                {
                    continue;
                }

                Vector3 velocity = other.Velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > ParkedSpeed * ParkedSpeed)
                {
                    continue;
                }

                // 정면만 보지 않는다. 비켜 나오다 조금 틀어진 차는 앞차가 비스듬히 앞에 걸려 선다.
                // 내 차 폭과 앞차 반경을 합친 띠 안에 앞차 중심이 들면 막힌 것으로 본다.
                Vector3 rel = other.Center - center;
                rel.y = 0f;
                float ahead = Vector3.Dot(rel, forward);
                float side = Mathf.Abs(Vector3.Dot(rel, right));
                if (ahead > 0f && ahead <= halfLength + ParkedBlockerLookAhead + other.BoundingRadius &&
                    side <= other.BoundingRadius + ParkedBlockerSideMargin && ahead < gap)
                {
                    gap = ahead;
                    blocker = other;
                }
            }

            return !float.IsPositiveInfinity(gap);
        }

        /// <summary>앞차 옆을 지날 때 내 차체와 앞차 차체 사이에 둘 여유(m). 교통 센서의 차로 여유(0.3m)보다 커야 지나는 동안 앞차를 막는 차로 잡지 않는다.</summary>
        private const float PassSideClearance = 0.7f;

        /// <summary>앞차 뒤 범퍼보다 이만큼(m) 뒤에서 나란한 직선에 올라탄다. 그 전에 차 머리를 직선 방향으로 맞춘다.</summary>
        private const float PassLeadIn = 1.5f;

        /// <summary>앞차 앞 범퍼를 지나 이만큼(m) 더 가서 직선을 끝낸다. 퇴장 지점으로 목적지를 바꾸는 거리(<see cref="departSwitchDistance"/>)보다 커야
        /// 목적지를 바꿀 때 뒤 범퍼가 앞차를 벗어나 있다.</summary>
        private const float PassLeadOut = 4.5f;

        /// <summary>서 있는 앞차(<paramref name="blocker"/>) 옆을 나란히 지나는 직선(<paramref name="from"/> → <paramref name="to"/>)을 찾는다.
        /// 직선은 앞차 차체 방향을 따르고, 앞차 옆면에서 내 차 폭의 절반 + <see cref="PassSideClearance"/>만큼 떨어진다.
        /// 직선 전체가 차 NavMesh 위로 끊기지 않고 다른 서 있는 차에 걸리지 않아야 한다. 양쪽이 다 되면 퇴장 지점에 가까운 쪽.</summary>
        private bool TryFindPassLane(Car car, ICarTrafficSensor blocker, Vector3 exitPoint, out Vector3 from, out Vector3 to)
        {
            from = to = default;
            if (blocker == null)
            {
                return false;
            }

            ICarTrafficSensor self = car.GetModule<ICarTrafficSensor>();
            Transform t = car.transform;

            // 앞차 차체 축을 내가 가는 쪽으로 맞춘다. 서로 마주 보고 서 있어도 내 진행 방향 기준으로 앞·뒤 범퍼를 잰다.
            Vector3[] corners = new Vector3[4];
            blocker.GetCorners(corners);
            Vector3 blockerCenter = blocker.Center;
            // GetCorners 순서: 0 앞오른쪽, 1 뒤오른쪽, 2 뒤왼쪽, 3 앞왼쪽.
            Vector3 axis = (corners[0] + corners[3]) * 0.5f - (corners[1] + corners[2]) * 0.5f;
            axis.y = 0f;
            Vector3 myForward = t.forward;
            myForward.y = 0f;
            if (axis.sqrMagnitude < 1e-4f || myForward.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            Vector3 forward = axis.normalized;
            if (Vector3.Dot(forward, myForward) < 0f)
            {
                forward = -forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float front = float.NegativeInfinity, rear = float.PositiveInfinity;
            float maxSide = float.NegativeInfinity, minSide = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = corners[i] - blockerCenter;
                float z = Vector3.Dot(local, forward);
                float x = Vector3.Dot(local, right);
                front = Mathf.Max(front, z);
                rear = Mathf.Min(rear, z);
                maxSide = Mathf.Max(maxSide, x);
                minSide = Mathf.Min(minSide, x);
            }

            // 내 차 크기. 센서가 없으면 흔한 승용차 크기로 어림한다.
            float myHalfWidth = 1f, myHalfLength = 2.3f;
            if (self != null)
            {
                self.GetCorners(corners);
                Vector3 myCenter = self.Center;
                Vector3 myRight = Vector3.Cross(Vector3.up, myForward.normalized);
                myHalfWidth = 0f;
                myHalfLength = 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = corners[i] - myCenter;
                    myHalfWidth = Mathf.Max(myHalfWidth, Mathf.Abs(Vector3.Dot(local, myRight)));
                    myHalfLength = Mathf.Max(myHalfLength, Mathf.Abs(Vector3.Dot(local, myForward.normalized)));
                }
            }

            // 차 기준점(피벗)은 떠 있다. 직선은 바닥 높이에서 긋는다.
            Vector3 ground = blockerCenter;
            if (CarNavMesh.SamplePosition(blockerCenter, out NavMeshHit groundHit, 3f))
            {
                ground.y = groundHit.position.y;
            }

            float best = float.PositiveInfinity;
            for (int s = 1; s >= -1; s -= 2)
            {
                float lateral = s > 0
                    ? maxSide + myHalfWidth + PassSideClearance
                    : minSide - myHalfWidth - PassSideClearance;

                Vector3 laneFrom = ground + right * lateral + forward * (rear - myHalfLength - PassLeadIn);
                Vector3 laneTo = ground + right * lateral + forward * (front + myHalfLength + PassLeadOut);

                if (!TrySampleCarMesh(laneFrom, out Vector3 a) || !TrySampleCarMesh(laneTo, out Vector3 b))
                {
                    continue;
                }

                // 차 NavMesh는 차 폭만큼 벽·주유기에서 깎여 있어, 중심선이 끊기지 않으면 차체도 지나간다.
                if (CarNavMesh.Raycast(a, b, out _))
                {
                    continue;
                }

                if (!IsClearOfParkedCars(a, b, self, blocker))
                {
                    continue;
                }

                float score = PlanarDistance(t.position, a) + PlanarDistance(b, exitPoint);
                if (score < best)
                {
                    best = score;
                    from = a;
                    to = b;
                }
            }

            return best < float.PositiveInfinity;
        }

        /// <summary>줄 옆(차 좌우)으로 비켜 나올 지점을 찾는다. 그 지점이 차 NavMesh 위이고 비어 있으며, 내 자리에서 끊기지 않고 닿고,
        /// 거기서 퇴장 지점까지 서 있는 차를 스치지 않는 길이 있어야 한다. 그중 퇴장까지 가장 짧은 곳을 고른다.</summary>
        private bool TryFindSideEscape(VisitContext context, out Vector3 escape)
        {
            escape = default;
            Car car = context.Car;
            Transform t = car.transform;
            ICarTrafficSensor self = car.GetModule<ICarTrafficSensor>();
            float bodyHalfWidth = 1.2f;

            Vector3 origin = t.position;
            if (CarNavMesh.SamplePosition(origin, out NavMeshHit ground, 2f))
            {
                origin = ground.position;
            }

            Vector3 forward = t.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // 뒤에도 차가 서 있으면 앞뒤로 갇혔다. 물러설 수 없으니 비켜 나올 수도 없다.
            Vector3 rearBumper = (self != null ? self.Center : t.position) - forward * (self != null ? self.BoundingRadius * 0.9f : 2.5f);
            if (!CarTraffic.IsSegmentClear(rearBumper, rearBumper - forward * EscapeReverseCheck, bodyHalfWidth, self))
            {
                Debug.Log($"[Leaving] {car.name}이(가) 앞뒤로 막혀 줄 옆으로 비켜 나올 수 없어 앞차를 기다립니다.", car);
                return false;
            }

            float best = float.PositiveInfinity;
            int noMesh = 0, notClear = 0, cut = 0, parkedNear = 0, noPath = 0, pathBlocked = 0;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int d = 0; d < EscapeSideDistances.Length; d++)
                {
                    for (int f = 0; f < EscapeForwardOffsets.Length; f++)
                    {
                        Vector3 candidate = origin + right * (s * EscapeSideDistances[d]) + forward * EscapeForwardOffsets[f];

                        if (!TrySampleCarMesh(candidate, out Vector3 point))
                        {
                            noMesh++;
                            continue;
                        }

                        if (!CarTraffic.IsAreaClear(point, departClearRadius))
                        {
                            notClear++;
                            continue;
                        }

                        // 비켜 나오는 길이 끊기면(주유기 섬·벽) 안 된다. 내 자리는 내 구멍이라 거기서 끊긴 건 넘긴다.
                        if (CarNavMesh.Raycast(point, origin, out NavMeshHit hit) &&
                            PlanarDistance(hit.position, origin) > bodyHalfWidth + 1f)
                        {
                            cut++;
                            continue;
                        }

                        if (!IsClearOfParkedCars(origin, point, self))
                        {
                            parkedNear++;
                            continue;
                        }

                        if (!NavMesh.CalculatePath(point, context.ExitPoint, CarNavMesh.Filter, _departPath) ||
                            _departPath.status != NavMeshPathStatus.PathComplete)
                        {
                            noPath++;
                            continue;
                        }

                        Vector3[] corners = _departPath.corners;
                        float length = PlanarDistance(origin, point);
                        bool clear = true;
                        for (int i = 1; i < corners.Length && clear; i++)
                        {
                            length += PlanarDistance(corners[i - 1], corners[i]);
                            clear = IsClearOfParkedCars(corners[i - 1], corners[i], self);
                        }

                        if (!clear)
                        {
                            pathBlocked++;
                            continue;
                        }

                        // 바짝 붙은 옆 지점은 차가 틀 공간이 모자라 제자리에서 못 빠져나온다. 멀찍이 비키는 쪽을 먼저 고른다.
                        float lateral = Mathf.Abs(Vector3.Dot(point - origin, right));
                        float score = length + (lateral > 1f ? Mathf.Max(0f, 6f - lateral) * EscapeTightPenalty : 0f);
                        if (score < best)
                        {
                            best = score;
                            escape = point;
                        }
                    }
                }
            }

            if (best < float.PositiveInfinity)
            {
                Debug.Log($"[Leaving] {car.name}이(가) 앞차에 막혀 줄 옆({escape.x:F1},{escape.z:F1})으로 비켜 나옵니다.", car);
                return true;
            }

            Debug.Log($"[Leaving] {car.name}이(가) 앞차에 막혔는데 줄 옆으로 비켜 나올 곳이 없어 곧장 퇴장 지점으로 갑니다. " +
                      $"(탈락: NavMesh 밖 {noMesh}, 차가 있음 {notClear}, 길 끊김 {cut}, 서 있는 차 옆 {parkedNear}, 퇴장 길 없음 {noPath}, 퇴장 길에 차 {pathBlocked})", car);
            return false;
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
        private static bool IsClearOfParkedCars(Vector3 from, Vector3 to, ICarTrafficSensor self, ICarTrafficSensor ignore = null)
        {
            const float step = 0.5f;

            Vector3 delta = to - from;
            delta.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / step));

            IReadOnlyList<ICarTrafficSensor> sensors = CarTraffic.Sensors;
            for (int s = 0; s < sensors.Count; s++)
            {
                ICarTrafficSensor sensor = sensors[s];
                if (ReferenceEquals(sensor, self) || ReferenceEquals(sensor, ignore))
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
                // 차 뒤쪽 목표(후진해 줄에서 빠지는 중)는 거의 닿을 때까지 바꾸지 않는다. 일찍 바꾸면 아직 줄 안이라 앞차 쪽 길을 다시 탄다.
                Transform carTransform = context.Car.transform;
                Vector3 toDepart = context.DepartPoint - carTransform.position;
                bool behind = Vector3.Dot(toDepart, carTransform.forward) < 0f &&
                              Mathf.Abs(Vector3.Dot(toDepart, carTransform.right)) < 1.5f;
                float switchDistance = behind ? 1f : departSwitchDistance;
                bool near = PlanarDistance(carTransform.position, context.DepartPoint) <= switchDistance;
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
                context.LeaveEscapeElapsed = 0f;
                return false;
            }

            if (context.Car.IsManeuvering)
            {
                // 제자리에서 도는 중이다. 앞으로 나아가지 않아도 막힌 게 아니니 아무것도 세지 않는다.
                context.LeaveStallElapsed = 0f;
                context.LeaveEscapeElapsed = 0f;
            }
            else if (IsBlockedAhead(context.Car))
            {
                // 앞차가 서 있는 차면 기다려도 안 비킨다(아직 자기 볼일 중). 잠시 뒤 빠져나갈 계획을 다시 세운다.
                context.LeaveEscapeElapsed += dt;
                if (context.LeaveEscapeElapsed >= EscapeRetryInterval)
                {
                    context.LeaveEscapeElapsed = 0f;
                    TryEscapeParkedBlocker(context);
                }

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
                context.LeaveEscapeElapsed = 0f;
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
