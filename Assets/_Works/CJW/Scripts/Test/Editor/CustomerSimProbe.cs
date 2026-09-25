using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Visit;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>손님 방문 흐름을 플레이 모드로 여러 번 돌려 보고 결과를 Temp/CustomerSim에 남기는 디버그용 도구.
    /// 메뉴로 시작하면 정해진 횟수만큼 플레이를 켰다 끄며 자동으로 반복한다. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class CustomerSimProbe
    {
        private const string RunsLeftKey = "CJW.CustomerSim.RunsLeft";
        private const string RunIndexKey = "CJW.CustomerSim.RunIndex";
        private const string OutDir = "Temp/CustomerSim";

        private const int TotalRuns = 5;
        private const float SimSeconds = 240f;
        private const float TimeScale = 3f;
        private const float StuckSeconds = 90f;

        private sealed class Tracked
        {
            public int Id;
            public VisitSession Session;
            public string CarName;
            public float StartTime;
            public float PhaseTime;
            public VisitPhase LastPhase;
            public bool StuckReported;
            public bool Completed;
            public readonly Dictionary<AbstractCustomer, string> CustomerStates = new();
            public readonly List<string> Timeline = new();
            public System.Action<VisitPhase> PhaseHandler;
            public System.Action<VisitSession> CompletedHandler;
        }

        private static readonly List<Tracked> Visits = new();
        private static readonly StringBuilder Log = new();
        private static readonly Dictionary<string, int> Errors = new();
        private static readonly Dictionary<string, int> Warnings = new();
        private static readonly Dictionary<AbstractCustomer, string> _navStates = new();
        private static readonly HashSet<AbstractCustomer> _pocketLogged = new();
        private static VisitDirector _director;
        private static float _startTime;
        private static bool _running;
        private static int _stuckCount;

        static CustomerSimProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Customer Sim x5")]
        private static void StartSimulation()
        {
            Directory.CreateDirectory(OutDir);
            foreach (string file in Directory.GetFiles(OutDir))
            {
                File.Delete(file);
            }

            SessionState.SetInt(RunsLeftKey, TotalRuns);
            SessionState.SetInt(RunIndexKey, 0);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/CJW/Stop Customer Sim")]
        private static void StopSimulation()
        {
            SessionState.SetInt(RunsLeftKey, 0);
            EditorApplication.isPlaying = false;
        }

        private static double _lastBeat;

        /// <summary>에디터 밖에서 진행 여부를 볼 수 있게 몇 초마다 현재 상태를 파일로 남긴다.</summary>
        private static void Heartbeat()
        {
            if (EditorApplication.timeSinceStartup - _lastBeat < 2.0)
            {
                return;
            }

            _lastBeat = EditorApplication.timeSinceStartup;
            File.WriteAllText($"{OutDir}/progress.txt",
                $"run {SessionState.GetInt(RunIndexKey, 0) + 1} elapsed {Elapsed():0.0}/{SimSeconds} frame {Time.frameCount} visits {Visits.Count} completed {Visits.Count(v => v.Completed)} realtime {Time.realtimeSinceStartup:0.0}");
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(RunsLeftKey, 0) > 0)
            {
                BeginRun();
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(RunsLeftKey, 0) > 0)
            {
                // 한 판이 끝났다. 남은 판이 있으면 다음 프레임에 다시 켠다.
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            }
        }

        private static void BeginRun()
        {
            Visits.Clear();
            Log.Clear();
            Errors.Clear();
            Warnings.Clear();
            _navStates.Clear();
            _pocketLogged.Clear();
            _stuckCount = 0;

            _director = Object.FindFirstObjectByType<VisitDirector>();
            if (_director == null)
            {
                Debug.LogError("[CustomerSim] 씬에 VisitDirector가 없습니다. 시뮬레이션을 중단합니다.");
                SessionState.SetInt(RunsLeftKey, 0);
                EditorApplication.isPlaying = false;
                return;
            }

            // 에디터 창이 포커스를 잃으면 플레이 루프가 멈춘다. 설정 에셋은 건드리지 않고 이번 플레이에만 켠다.
            Application.runInBackground = true;
            Time.timeScale = TimeScale;
            _startTime = Time.time;
            _running = true;

            _director.VisitStarted += OnVisitStarted;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;

            Write($"run {SessionState.GetInt(RunIndexKey, 0) + 1}/{TotalRuns} 시작 (timeScale {TimeScale}, {SimSeconds}s)");
        }

        private static void OnVisitStarted(VisitSession session)
        {
            var tracked = new Tracked
            {
                Id = Visits.Count + 1,
                Session = session,
                CarName = session.Car != null ? session.Car.name : "?",
                StartTime = Time.time,
                PhaseTime = Time.time,
                LastPhase = session.Phase
            };

            string customers = string.Join(", ", session.Customers.Select(c => c.Data != null ? c.Data.name : c.name));
            Write($"#{tracked.Id} 방문 시작: {tracked.CarName} / 손님 {session.Customers.Count}명 [{customers}]");
            tracked.Timeline.Add($"{Elapsed():0}s {session.Phase}");

            // VisitSession은 풀로 재사용되므로 끝나면 반드시 구독을 끊는다. 안 끊으면 다음 방문 기록이 이 방문에 섞인다.
            tracked.PhaseHandler = phase => OnPhase(tracked, phase);
            tracked.CompletedHandler = _ => OnCompleted(tracked);
            session.OnStateChanged += tracked.PhaseHandler;
            session.Completed += tracked.CompletedHandler;
            Visits.Add(tracked);
        }

        private static void OnPhase(Tracked tracked, VisitPhase phase)
        {
            if (!_running)
            {
                return;
            }

            tracked.Timeline.Add($"{Elapsed():0}s {phase}");
            tracked.LastPhase = phase;
            tracked.PhaseTime = Time.time;
            tracked.StuckReported = false;
            Write($"#{tracked.Id} → {phase}");
        }

        private static void OnCompleted(Tracked tracked)
        {
            if (!_running || tracked.Completed)
            {
                return;
            }

            tracked.Completed = true;
            tracked.Session.OnStateChanged -= tracked.PhaseHandler;
            tracked.Session.Completed -= tracked.CompletedHandler;
            Write($"#{tracked.Id} 방문 완료 ({Time.time - tracked.StartTime:0}s)");
        }

        private static void Tick()
        {
            if (!_running || !EditorApplication.isPlaying)
            {
                return;
            }

            Heartbeat();

            foreach (Tracked tracked in Visits)
            {
                if (tracked.Completed)
                {
                    continue;
                }

                // 손님별 FSM 상태 변화를 기록한다. 세션이 풀로 돌아가면 목록이 비므로 완료 전까지만 본다.
                foreach (AbstractCustomer customer in tracked.Session.Customers)
                {
                    if (customer == null)
                    {
                        continue;
                    }

                    string state = customer.Fsm?.Machine?.Current?.GetType().Name ?? "-";
                    if (!tracked.CustomerStates.TryGetValue(customer, out string prev) || prev != state)
                    {
                        tracked.CustomerStates[customer] = state;
                        string who = customer.Data != null ? customer.Data.name : customer.name;
                        Write($"#{tracked.Id}   {who}: {state} @{customer.transform.position.x:F1},{customer.transform.position.z:F1}");
                    }

                    // 요구하러 걸어가는 손님의 길찾기 상태가 바뀔 때마다 남긴다. 목적지에 못 닿는 원인을 보려는 것.
                    if (state is "RequestServiceState" or "MoveToNearestPointState" or "VandalizeState" && customer.Agent != null)
                    {
                        NavMeshAgent a = customer.Agent;
                        string nav = a.enabled
                            ? $"on={a.isOnNavMesh} pending={a.pathPending} hasPath={a.hasPath} status={a.pathStatus} dest={a.destination:F1}"
                            : "agent off";
                        if (!_navStates.TryGetValue(customer, out string prevNav) || prevNav != nav)
                        {
                            _navStates[customer] = nav;
                            Write($"#{tracked.Id}   nav {customer.Data?.name}: {nav} pos={customer.transform.position:F1}");

                            // 목적지가 제자리로 잡혔다면 섬에 갇힌 것이다. 8방향으로 가장자리까지 거리를 재 모양을 남긴다.
                            if (a.enabled && a.isOnNavMesh && !a.pathPending && !a.hasPath
                                && new Vector2(a.destination.x - a.nextPosition.x, a.destination.z - a.nextPosition.z).sqrMagnitude < 0.25f && !_pocketLogged.Contains(customer))
                            {
                                _pocketLogged.Add(customer);
                                var filter = new NavMeshQueryFilter { agentTypeID = a.agentTypeID, areaMask = a.areaMask };
                                var parts = new List<string>();
                                for (int d = 0; d < 8; d++)
                                {
                                    Vector3 dir = Quaternion.Euler(0f, d * 45f, 0f) * Vector3.forward;
                                    NavMesh.SamplePosition(a.nextPosition, out NavMeshHit self, 2f, filter);
                                    NavMesh.Raycast(self.position, self.position + dir * 20f, out NavMeshHit edge, filter);
                                    parts.Add($"{d * 45}°:{edge.distance:F1}");
                                }

                                var cars = new List<string>();
                                foreach (var sensor in _Works.CJW.Scripts.Cars.CarTraffic.Sensors)
                                {
                                    if (sensor is Component c && (c.transform.position - a.nextPosition).sqrMagnitude < 100f)
                                    {
                                        cars.Add($"{c.transform.position.x:F1},{c.transform.position.z:F1}");
                                    }
                                }

                                Write($"#{tracked.Id}   POCKET {customer.Data?.name} at {a.nextPosition:F1} → {string.Join(" ", parts)} / 근처 차 [{string.Join(" | ", cars)}]");
                            }
                        }
                    }
                }

                if (!tracked.StuckReported && Time.time - tracked.PhaseTime > StuckSeconds)
                {
                    tracked.StuckReported = true;
                    _stuckCount++;
                    string detail = string.Join(", ", tracked.CustomerStates.Select(kv =>
                        $"{(kv.Key != null && kv.Key.Data != null ? kv.Key.Data.name : "?")}={kv.Value}@{(kv.Key != null ? kv.Key.transform.position.ToString("F1") : "")}"));
                    Write($"#{tracked.Id} ⚠ STUCK: {tracked.LastPhase} 단계에 {StuckSeconds}s 넘게 머묾. 차 {tracked.Session.Car?.transform.position:F1} / {detail}");
                }
            }

            if (Elapsed() >= SimSeconds)
            {
                EndRun();
            }
        }

        private static void EndRun()
        {
            _running = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (_director != null)
            {
                _director.VisitStarted -= OnVisitStarted;
            }

            int runIndex = SessionState.GetInt(RunIndexKey, 0);
            var summary = new StringBuilder();
            summary.AppendLine($"===== RUN {runIndex + 1} 요약 =====");
            summary.AppendLine($"방문 시작 {Visits.Count} / 완료 {Visits.Count(v => v.Completed)} / 진행 중 {Visits.Count(v => !v.Completed)} / STUCK {_stuckCount}");
            foreach (Tracked v in Visits)
            {
                summary.AppendLine($"  #{v.Id} {v.CarName} {(v.Completed ? "완료" : "미완료(" + v.LastPhase + ")")} : {string.Join(" > ", v.Timeline)}");
            }

            summary.AppendLine($"에러 {Errors.Values.Sum()}건 (종류 {Errors.Count})");
            foreach (var e in Errors)
            {
                summary.AppendLine($"  [x{e.Value}] {e.Key}");
            }

            summary.AppendLine($"경고 {Warnings.Values.Sum()}건 (종류 {Warnings.Count})");
            foreach (var w in Warnings)
            {
                summary.AppendLine($"  [x{w.Value}] {w.Key}");
            }

            File.WriteAllText($"{OutDir}/run_{runIndex + 1}.txt", summary + "\n----- 로그 -----\n" + Log);

            SessionState.SetInt(RunIndexKey, runIndex + 1);
            int left = SessionState.GetInt(RunsLeftKey, 0) - 1;
            SessionState.SetInt(RunsLeftKey, left);
            if (left <= 0)
            {
                File.WriteAllText($"{OutDir}/done.txt", "done");
            }

            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (message.StartsWith("[CustomerSim]"))
            {
                return;
            }

            string key = message.Split('\n')[0];
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                string top = stackTrace?.Split('\n').FirstOrDefault(l => l.Contains("_Works") || l.Contains("DevLib")) ?? "";
                key = $"{type}: {key} @ {top.Trim()}";
                Errors[key] = Errors.TryGetValue(key, out int n) ? n + 1 : 1;
                Write($"❌ {key}");
            }
            else if (type == LogType.Warning)
            {
                Warnings[key] = Warnings.TryGetValue(key, out int n) ? n + 1 : 1;
                Write($"⚠ {key}");
            }
        }

        private static float Elapsed() => Time.time - _startTime;

        private static void Write(string line)
        {
            Log.AppendLine($"[{Elapsed(),6:0.0}] {line}");
        }
    }
}
