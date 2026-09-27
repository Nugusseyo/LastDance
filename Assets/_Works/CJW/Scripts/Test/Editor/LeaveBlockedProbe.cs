using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Visit;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>앞차가 아직 서 있는데 뒤차가 먼저 떠나는 경우(주유를 먼저 받은 뒤차)를 재현한다. 같은 줄에 두 차가 Waiting이면
    /// 뒤차만 출발시키고, 앞차가 떠나기 전에 뒤차가 빠져나가는지 1초마다 기록한다. 결과는 Temp/CustomerSim/leave_blocked.txt.
    /// 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class LeaveBlockedProbe
    {
        private const string RunningKey = "CJW.LeaveBlocked.Running";
        private const string OutPath = "Temp/CustomerSim/leave_blocked.txt";
        private const float TimeScale = 3f;
        private const float GiveUpSeconds = 600f;

        /// <summary>뒤차가 이 시간(초) 안에 퇴장해야 한다. 앞차의 자동 출발(60초)보다 짧아야 앞차 덕에 풀린 게 아니다.</summary>
        private const float LeaveWithin = 40f;

        private const int TargetCount = 3;

        private sealed class Case
        {
            public VisitSession Rear;
            public VisitSession Front;
            public string Name;
            public float StartedAt;
            public float NextLogAt;
            public bool Done;
            public System.Action<VisitSession> OnCompleted;
            public bool RearCompleted;
        }

        private static readonly List<VisitSession> Sessions = new();
        private static readonly List<Case> Cases = new();
        private static readonly HashSet<VisitSession> Used = new();
        private static readonly StringBuilder Log = new();
        private static VisitDirector _director;
        private static float _startTime;
        private static int _pass;
        private static int _fail;

        static LeaveBlockedProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Leave Blocked Test")]
        private static void StartTest()
        {
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Sessions.Clear();
                Cases.Clear();
                Used.Clear();
                Log.Clear();
                _pass = 0;
                _fail = 0;
                _startTime = Time.time;
                Time.timeScale = TimeScale;
                _director = Object.FindFirstObjectByType<VisitDirector>();
                if (_director != null)
                {
                    _director.VisitStarted += OnVisitStarted;
                }

                EditorApplication.update += Tick;
                Application.logMessageReceived += OnLog;
                Write("시작");
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Finish("플레이 모드가 꺼져 중단");
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("[Leaving]") || condition.StartsWith("[CarSteering]") || type is LogType.Error or LogType.Exception)
            {
                Write("  로그: " + condition.Split('\n')[0]);
            }
        }

        private static void OnVisitStarted(VisitSession session)
        {
            if (!Sessions.Contains(session))
            {
                Sessions.Add(session);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            float now = Time.time;
            if (now - _startTime > GiveUpSeconds)
            {
                Finish("시간 초과");
                return;
            }

            if (Cases.Count(c => !c.Done) == 0 && Cases.Count < TargetCount)
            {
                TryStartCase(now);
            }

            foreach (Case c in Cases.Where(c => !c.Done))
            {
                Advance(c, now);
            }

            if (Cases.Count >= TargetCount && Cases.All(c => c.Done))
            {
                Finish("완료");
            }
        }

        /// <summary>같은 줄(x가 거의 같음)에 Waiting인 두 차가 있으면 뒤차(차 정면 반대쪽에 선 차)만 출발시킨다.</summary>
        private static void TryStartCase(float now)
        {
            List<VisitSession> waiting = Sessions
                .Where(s => s.Phase == VisitPhase.Waiting && s.Car != null && !Used.Contains(s))
                .ToList();

            foreach (VisitSession a in waiting)
            {
                foreach (VisitSession b in waiting)
                {
                    if (a == b)
                    {
                        continue;
                    }

                    Transform ta = a.Car.transform;
                    Transform tb = b.Car.transform;
                    Vector3 local = ta.InverseTransformPoint(tb.position);

                    // b가 a의 정면 12m 안, 좌우 1.5m 안에 있으면 a가 뒤차, b가 앞차다.
                    if (local.z <= 0f || local.z > 12f || Mathf.Abs(local.x) > 1.5f)
                    {
                        continue;
                    }

                    // 뒤에도 차가 서 있는(두 차 사이에 낀) 뒤차만 본다. 물러설 곳이 없는 가장 어려운 경우다.
                    bool sandwiched = CarTraffic.Sensors.Any(sensor =>
                    {
                        Vector3 rel = ta.InverseTransformPoint(sensor.Center);
                        return rel.z < -3f && rel.z > -10f && Mathf.Abs(rel.x) < 1.5f;
                    });
                    if (!sandwiched)
                    {
                        continue;
                    }

                    var c = new Case
                    {
                        Rear = a,
                        Front = b,
                        Name = $"{a.Car.name}(뒤) / {b.Car.name}(앞 {local.z:F1}m)",
                        StartedAt = now,
                        NextLogAt = now,
                    };
                    c.OnCompleted = _ => c.RearCompleted = true;
                    a.Completed += c.OnCompleted;
                    Used.Add(a);
                    Used.Add(b);
                    Cases.Add(c);

                    Write($"사례 {Cases.Count}: {c.Name} — 뒤차만 출발시킴. 뒤차 ({ta.position.x:F1},{ta.position.z:F1}) 앞차 ({tb.position.x:F1},{tb.position.z:F1})");
                    // 뒤차 주변 10m 안의 다른 차를 뒤차 기준(앞 +z, 오른쪽 +x)으로 남긴다. 앞뒤가 다 막혔는지 본다.
                    var around = new List<string>();
                    foreach (ICarTrafficSensor sensor in CarTraffic.Sensors)
                    {
                        Vector3 rel = ta.InverseTransformPoint(sensor.Center);
                        if (rel.magnitude > 0.5f && rel.magnitude < 10f)
                        {
                            around.Add($"({rel.x:F1},{rel.z:F1})");
                        }
                    }

                    Write($"  뒤차 주변 차(뒤차 기준 x=오른쪽, z=앞): {string.Join(" ", around)}");
                    a.RequestDeparture();
                    return;
                }
            }
        }

        private static void Advance(Case c, float now)
        {
            float elapsed = now - c.StartedAt;
            bool frontStill = c.Front.Phase == VisitPhase.Waiting;

            if (c.RearCompleted || c.Rear.Phase is VisitPhase.Completed or VisitPhase.None)
            {
                // 앞차가 먼저 떠나서 풀린 건 고친 게 아니다. 앞차가 서 있는 동안 빠져나가야 통과다.
                Check(frontStill,
                      $"{c.Name}: 뒤차가 {elapsed:F1}s 만에 빠져나감 (그때 앞차 {(frontStill ? "아직 서 있음" : c.Front.Phase.ToString())})");
                End(c);
                return;
            }

            if (now >= c.NextLogAt)
            {
                c.NextLogAt = now + 1f;
                Car car = c.Rear.Car;
                ICarTrafficSensor sensor = car != null ? car.GetModule<ICarTrafficSensor>() : null;
                Vector3 p = car != null ? car.transform.position : Vector3.zero;
                var steer = car != null ? car.GetModule<CarSteeringMoveModule>() : null;
                string blocker = "-";
                if (sensor?.Blocker != null && car != null)
                {
                    Vector3 rel = car.transform.InverseTransformPoint(sensor.Blocker.Center);
                    blocker = $"({rel.x:F1},{rel.z:F1})";
                }

                Write($"  {elapsed,5:F1}s 뒤차 {c.Rear.Phase} ({p.x:F1},{p.z:F1}) 막은 차 {blocker} 후진={steer?.IsReversing} 속도={steer?.Speed:F2} 앞차 {c.Front.Phase}");
            }

            if (!frontStill)
            {
                // 앞차가 자기 시간이 돼 먼저 떠났다. 이후로는 앞차 덕에 풀리므로 이 사례로는 판정하지 않는다.
                Check(false, $"{c.Name}: 앞차가 서 있는 {elapsed:F1}s 동안 못 빠져나감, 앞차가 먼저 떠남({c.Front.Phase})");
                End(c);
            }
            else if (elapsed >= LeaveWithin)
            {
                Check(false, $"{c.Name}: 앞차가 서 있는 동안 {LeaveWithin}초 안에 못 빠져나감 (뒤차 {c.Rear.Phase})");
                End(c);
            }
        }

        private static void End(Case c)
        {
            c.Done = true;
            c.Rear.Completed -= c.OnCompleted;
        }

        private static void Check(bool ok, string what)
        {
            if (ok) _pass++; else _fail++;
            Write($"{(ok ? "[통과]" : "[실패]")} {what}");
        }

        private static void Write(string line)
        {
            Log.AppendLine($"[{Time.time - _startTime,6:F1}s] {line}");
        }

        private static void Finish(string reason)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (_director != null)
            {
                _director.VisitStarted -= OnVisitStarted;
            }

            SessionState.SetBool(RunningKey, false);
            foreach (Case c in Cases.Where(c => !c.Done))
            {
                End(c);
            }

            Log.AppendLine();
            Log.AppendLine($"끝: {reason}. 사례 {Cases.Count}, 통과 {_pass}, 실패 {_fail}");
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);
            File.WriteAllText(OutPath, Log.ToString());
            Time.timeScale = 1f;

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
