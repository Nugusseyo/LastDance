using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Visit;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>플레이 중 멈춘 차를 살펴보려고 지금 씬의 차 상태를 Temp/CustomerSim/cars.txt에 쓴다.
    /// 방문 단계·퇴장 계획, 교통 센서가 막힌 차, 에이전트 경로, 이동 모듈 내부 값(bool·float 필드)을 적는다.</summary>
    public static class CarStateDump
    {
        private const string OutPath = "Temp/CustomerSim/cars.txt";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem("Tools/CJW/Dump Car States")]
        private static void Dump()
        {
            var sb = new StringBuilder($"time={Time.time:0.0} playing={Application.isPlaying}\n");

            foreach (VisitDirector director in Object.FindObjectsByType<VisitDirector>(FindObjectsSortMode.None))
            {
                if (director.GetType().GetField("_activeVisits", Flags)?.GetValue(director) is not IList visits)
                {
                    continue;
                }

                sb.AppendLine($"== visits ({visits.Count})");
                foreach (object visit in visits)
                {
                    var session = visit.GetType().GetField("Session")?.GetValue(visit) as VisitSession;
                    object slot = visit.GetType().GetField("Slot")?.GetValue(visit);
                    if (session == null)
                    {
                        continue;
                    }

                    object ctx = typeof(VisitSession).GetField("_context", Flags)?.GetValue(session);
                    sb.AppendLine($"{(session.Car != null ? session.Car.name : "?")} phase={session.Phase} slot={(slot as Component)?.name} {Fields(ctx, "PhaseElapsed", "Departing", "DepartPoint", "LeaveStallElapsed", "LeaveEscapeElapsed", "LeaveRestarts", "ExitPoint")}");
                }
            }

            sb.AppendLine("== cars");
            foreach (Car car in Object.FindObjectsByType<Car>(FindObjectsSortMode.None))
            {
                Transform t = car.transform;
                sb.AppendLine($"# {car.name} pos=({t.position.x:0.00},{t.position.z:0.00}) yaw={t.eulerAngles.y:0} arrived={car.IsArrived} maneuvering={car.IsManeuvering} completePath={car.HasCompletePath}");

                ICarTrafficSensor sensor = car.GetModule<ICarTrafficSensor>();
                if (sensor != null)
                {
                    var corners = new Vector3[4];
                    sensor.GetCorners(corners);
                    string blocker = sensor.Blocker is Component b ? b.GetComponentInParent<Car>()?.name : (sensor.Blocker != null ? "?" : "none");
                    sb.AppendLine($"  sensor center=({sensor.Center.x:0.00},{sensor.Center.z:0.00}) radius={sensor.BoundingRadius:0.00} vel={sensor.Velocity.magnitude:0.00} blocker={blocker} " +
                                  $"corners=({corners[0].x:0.0},{corners[0].z:0.0}) ({corners[1].x:0.0},{corners[1].z:0.0}) ({corners[2].x:0.0},{corners[2].z:0.0}) ({corners[3].x:0.0},{corners[3].z:0.0})");
                }

                NavMeshAgent agent = car.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    var path = new StringBuilder();
                    if (agent.hasPath)
                    {
                        foreach (Vector3 c in agent.path.corners)
                        {
                            path.Append($"({c.x:0.0},{c.z:0.0}) ");
                        }
                    }

                    sb.AppendLine($"  agent onMesh={agent.isOnNavMesh} dest=({agent.destination.x:0.0},{agent.destination.z:0.0}) status={agent.pathStatus} pending={agent.pathPending} path={path}");
                }

                NavMeshObstacle obstacle = car.GetComponentInChildren<NavMeshObstacle>(true);
                if (obstacle != null)
                {
                    sb.AppendLine($"  obstacle carving={obstacle.carving} enabled={obstacle.enabled}");
                }

                var move = car.GetModule<ICarMoveModule>() as Object;
                if (move != null)
                {
                    sb.AppendLine($"  move {Fields(move, null)}");
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[CarStateDump] {OutPath} 작성");
        }

        /// <summary>names가 있으면 그 필드만, 없으면 bool·float·int·Vector3 필드를 모두 적는다.</summary>
        private static string Fields(object target, params string[] names)
        {
            if (target == null)
            {
                return "";
            }

            var sb = new StringBuilder();
            foreach (FieldInfo field in target.GetType().GetFields(Flags))
            {
                if (names != null && System.Array.IndexOf(names, field.Name) < 0)
                {
                    continue;
                }

                if (names == null && field.FieldType != typeof(bool) && field.FieldType != typeof(float) &&
                    field.FieldType != typeof(int) && field.FieldType != typeof(Vector3))
                {
                    continue;
                }

                object value = field.GetValue(target);
                sb.Append(value is Vector3 v ? $"{field.Name}=({v.x:0.0},{v.z:0.0}) " : value is float f ? $"{field.Name}={f:0.##} " : $"{field.Name}={value} ");
            }

            return sb.ToString();
        }
    }
}
