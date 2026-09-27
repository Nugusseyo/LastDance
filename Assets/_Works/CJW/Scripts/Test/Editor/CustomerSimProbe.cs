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
            public float NextLeaveLog;
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

        [MenuItem("Tools/CJW/Run Customer Sim")]
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

        /// <summary>진단용. 손님 데이터마다 주유를 원하는지(한 차에 하나만 탈 손님인지)를 Temp/CustomerSim/fuel.txt에 남긴다.</summary>
        [MenuItem("Tools/CJW/Dump Fuel Customers")]
        public static void DumpFuelCustomers()
        {
            var sb = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:CustomerDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<_Works.CJW.Scripts.Customers.Data.CustomerDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null)
                {
                    continue;
                }

                sb.AppendLine($"{data.name}\ttype={data.customerType}\t프리팹주유={data.WantsFuel}\t주유손님={_Works.CJW.Scripts.Customers.Data.CustomerRoles.WantsFuel(data)}");
            }

            Directory.CreateDirectory(OutDir);
            File.WriteAllText($"{OutDir}/fuel.txt", sb.ToString());
        }

        /// <summary>진단용. 플레이 중 서 있는 손님 하나를 가까운 차가 +x 방향 6m/s로 친 것처럼 쓰러뜨리고, 그 자리를 남긴다.</summary>
        [MenuItem("Tools/CJW/Debug Knock Down Customer")]
        public static void DebugKnockDown()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var car = Object.FindAnyObjectByType<_Works.CJW.Scripts.Cars.Car>();
            if (car == null)
            {
                return;
            }

            // 카메라로 보기 좋은 앞마당(8, -8)에 가장 가까운 손님을 고른다.
            Vector3 yard = new(8f, 0f, -8f);
            _Works.CJW.Scripts.Cars.ICarHittable best = null;
            float bestDistance = float.MaxValue;
            foreach (var target in _Works.CJW.Scripts.Cars.CarHitTargets.Targets)
            {
                if (!target.CanBeHit)
                {
                    continue;
                }

                Vector3 d = target.HitPosition - yard;
                d.y = 0f;
                if (d.sqrMagnitude < bestDistance)
                {
                    bestDistance = d.sqrMagnitude;
                    best = target;
                }
            }

            if (best == null)
            {
                return;
            }

            Vector3 p = best.HitPosition;
            _Works.CJW.Scripts.Cars.CarHitTargets.Raise(new _Works.CJW.Scripts.Cars.CarHitInfo(car, best, new Vector3(6f, 0f, 0f), p));

            Directory.CreateDirectory(OutDir);
            File.WriteAllText($"{OutDir}/knock.txt", $"{p.x:F2},{p.y:F2},{p.z:F2}");
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
            FightSamples.Clear();
            MeetLogged.Clear();
            FightRoles.Clear();
            LastRole.Clear();
            _roleSwaps = 0;
            _endedSamples = 0;
            FightDrift.Clear();
            ReachThisTurn.Clear();
            ReachPerTurn.Clear();
            BodyGap.Clear();
            WasBoarded.Clear();
            _nextInsideCheck.Clear();
            PunchGaps.Clear();
            _nextPunch.Clear();
            CarColliders.Clear();
            CarLastPos.Clear();
            InsideByState.Clear();
            _insideSamples = 0;
            _standSamples = 0;
            _landings = 0;
            _landingOverlaps = 0;
            Boarding.Clear();
            BoardResults.Clear();
            _nextFightSample.Clear();
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

            // 구독 전에 이미 시작된 방문(첫 틱에 스폰된 차)도 붙잡는다.
            var field = typeof(VisitDirector).GetField("_activeVisits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(_director) is System.Collections.IList active)
            {
                foreach (object visit in active)
                {
                    if (visit.GetType().GetField("Session")?.GetValue(visit) is VisitSession existing)
                    {
                        OnVisitStarted(existing);
                    }
                }
            }
            Application.logMessageReceived += OnLog;
            _hits = 0;
            Knocked.Clear();
            _Works.CJW.Scripts.Cars.CarHitTargets.Hit += OnCarHit;
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

            // 하차를 시작할 때 차가 선 자리를 남긴다. 어느 주차 자리가 실제로 쓰였는지 볼 때 쓴다.
            if (phase == VisitPhase.Unloading && tracked.Session.Car != null)
            {
                Vector3 p = tracked.Session.Car.transform.position;
                Write($"#{tracked.Id} PARKED {p.x:F1},{p.z:F1}");
            }
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

            // 쓰러졌던 손님이 일어서면 날아간 거리와 일어선 자리를 남긴다.
            if (Knocked.Count > 0)
            {
                var done = new List<AbstractCustomer>();
                foreach (KeyValuePair<AbstractCustomer, Vector3> pair in Knocked)
                {
                    if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy)
                    {
                        done.Add(pair.Key);
                        continue;
                    }

                    if (!pair.Key.IsKnockedDown)
                    {
                        Vector3 p = pair.Key.transform.position;
                        Vector3 d = p - pair.Value;
                        d.y = 0f;
                        bool onMesh = pair.Key.Agent != null && pair.Key.Agent.isActiveAndEnabled && pair.Key.Agent.isOnNavMesh;
                        Write($"RECOVER {pair.Key.name} 날아간 거리 {d.magnitude:F1}m @{p.x:F1},{p.z:F1} onNavMesh={onMesh}");
                        done.Add(pair.Key);
                    }
                }

                foreach (AbstractCustomer c in done)
                {
                    Knocked.Remove(c);
                }
            }

            foreach (Tracked tracked in Visits)
            {
                if (tracked.Completed)
                {
                    continue;
                }

                // 퇴장 중인 차가 어디서 막히는지 남긴다.
                if (tracked.LastPhase == VisitPhase.Leaving && tracked.Session.Car != null && Time.time >= tracked.NextLeaveLog)
                {
                    tracked.NextLeaveLog = Time.time + 3f;
                    var car = tracked.Session.Car;
                    Vector3 p = car.transform.position;
                    Write($"#{tracked.Id} LEAVE @{p.x:F1},{p.z:F1} yaw {car.transform.eulerAngles.y:F0} arrived={car.IsArrived} complete={car.HasCompletePath} t={Time.time - tracked.PhaseTime:F0}s {SteeringDump(car)}");
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

                    SampleFight(tracked, customer, state);
                    SampleReach(customer, state);
                    TrackBoarding(tracked, customer, state);
                    TrackLanding(tracked, customer);
                    CheckInsideCar(tracked, customer, state);
                    SamplePunch(tracked, customer, state);

                    // 요구하러 걸어가는 손님의 길찾기 상태가 바뀔 때마다 남긴다. 목적지에 못 닿는 원인을 보려는 것.
                    if (state is "RequestServiceState" or "MoveToNearestPointState" or "VandalizeState" or "StealCarState" or "BoardState" or "MeetUpState" && customer.Agent != null)
                    {
                        NavMeshAgent a = customer.Agent;
                        string nav = a.enabled
                            ? $"on={a.isOnNavMesh} pending={a.pathPending} hasPath={a.hasPath} status={a.pathStatus} dest={a.destination:F1} rem={(a.hasPath ? a.remainingDistance : -1f):F1}"
                            : "agent off";
                        if (!_navStates.TryGetValue(customer, out string prevNav) || prevNav != nav)
                        {
                            _navStates[customer] = nav;
                            Write($"#{tracked.Id}   nav {customer.Data?.name}: {nav} pos={customer.transform.position:F1} next={(a.enabled ? a.nextPosition : Vector3.zero):F1}");

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
            _Works.CJW.Scripts.Cars.CarHitTargets.Hit -= OnCarHit;
            if (_director != null)
            {
                _director.VisitStarted -= OnVisitStarted;
            }

            int runIndex = SessionState.GetInt(RunIndexKey, 0);
            var summary = new StringBuilder();
            summary.AppendLine($"===== RUN {runIndex + 1} 요약 =====");
            summary.AppendLine($"방문 시작 {Visits.Count} / 완료 {Visits.Count(v => v.Completed)} / 진행 중 {Visits.Count(v => !v.Completed)} / STUCK {_stuckCount}");
            summary.AppendLine($"차에 치임 {_hits}건");
            summary.AppendLine(PunchGaps.Count > 0
                ? $"차 때리기 표본 {PunchGaps.Count}개: 차 몸체까지 최소 {PunchGaps.Min():F2} / 평균 {PunchGaps.Average():F2} / 최대 {PunchGaps.Max():F2}m"
                : "차 때리기 표본 0개");
            summary.AppendLine($"차 밖 손님 표본 {_standSamples}개 중 몸 중심이 차 몸체 0.2m 안 {_insideSamples}개 [{string.Join(", ", InsideByState.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))}]");
            summary.AppendLine($"하차 {_landings}명 중 1.2m 안에 다른 사람이 있던 하차 {_landingOverlaps}명");
            if (FightSamples.Count > 0)
            {
                summary.AppendLine($"싸움 동작: {string.Join(", ", FightRoles.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))} / 역할 바뀜 {_roleSwaps}번 / 클립이 끝나 멈춘 표본 {_endedSamples}개 / 자기 자리에서 밀린 거리 평균 {(FightDrift.Count > 0 ? FightDrift.Average() : 0f):F2} 최대 {(FightDrift.Count > 0 ? FightDrift.Max() : 0f):F2}m");
                if (ReachPerTurn.Count > 0)
                {
                    var sorted = ReachPerTurn.OrderBy(v => v).ToList();
                    summary.AppendLine($"공격 {ReachPerTurn.Count}차례: 손이 상대 머리·가슴에 가장 가까이 간 거리 최소 {sorted[0]:F2} / 중앙 {sorted[sorted.Count / 2]:F2} / 평균 {ReachPerTurn.Average():F2} / 최대 {sorted[^1]:F2}m, 0.15m 안에 닿은 차례 {ReachPerTurn.Count(v => v <= 0.15f)}개" +
                                       $" / 공격 중 몸 중심 거리 최소 {BodyGap.Min():F2} 평균 {BodyGap.Average():F2}m");
                }

                summary.AppendLine($"싸움 표본 {FightSamples.Count}개: 거리 최소 {FightSamples.Min(s => s.distance):F2} / 평균 {FightSamples.Average(s => s.distance):F2} / 최대 {FightSamples.Max(s => s.distance):F2}m, " +
                                   $"바라보는 각 평균 {FightSamples.Average(s => (s.angleA + s.angleB) * 0.5f):F0}° / 최대 {FightSamples.Max(s => Mathf.Max(s.angleA, s.angleB)):F0}° (30° 넘는 표본 {FightSamples.Count(s => Mathf.Max(s.angleA, s.angleB) > 30f)}개)");
            }
            else
            {
                summary.AppendLine("싸움 표본 0개");
            }

            if (BoardResults.Count > 0)
            {
                var slow = BoardResults.Where(r => r.seconds >= 15f || r.jump >= 5f).ToList();
                summary.AppendLine($"탑승 {BoardResults.Count}명: 평균 {BoardResults.Average(r => r.seconds):F1}s / 최대 {BoardResults.Max(r => r.seconds):F1}s, 15초 이상 또는 5m 넘게 순간이동 {slow.Count}명" +
                                   (slow.Count > 0 ? " → " + string.Join(", ", slow.Select(r => $"{r.who} {r.seconds:F0}s/{r.jump:F1}m")) : ""));
            }
            summary.AppendLine($"버려진 차 {(_director != null ? _director.AbandonedCarCount : -1)}대 / 남은 훔칠 차 {_Works.CJW.Scripts.Cars.StealableCar.All.Count(c => !c.IsClaimed)}대");
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

        /// <summary>싸움 중인 짝의 거리와 바라보는 각도 표본. 둘 다 제자리에 선 순간만 1초마다 잰다.</summary>
        private static readonly List<(float distance, float angleA, float angleB)> FightSamples = new();
        private static readonly Dictionary<AbstractCustomer, float> _nextFightSample = new();
        private static readonly HashSet<(int, AbstractCustomer)> MeetLogged = new();
        private static readonly Dictionary<string, int> FightRoles = new();
        private static readonly Dictionary<string, string> LastRole = new();
        private static int _roleSwaps;
        private static int _endedSamples;
        private static readonly List<float> FightDrift = new();
        private static readonly int[] AttackStates = { Animator.StringToHash("JAB"), Animator.StringToHash("HOOK"), Animator.StringToHash("BODYJAB") };
        private static readonly int BlockState = Animator.StringToHash("BLOCK");

        /// <summary>Animator가 지금(전환 중이면 넘어가는 쪽) 트는 상태로 공격·방어를 가린다.</summary>
        private static string FightRole(AbstractCustomer customer)
        {
            Animator animator = customer.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return "?";
            }

            AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            // 반복하지 않는 클립이 끝나 마지막 프레임에 멈춰 있으면 표시한다. 싸우다 멈춘 것처럼 보이는 순간이다.
            string ended = !animator.IsInTransition(0) && !info.loop && info.normalizedTime >= 1f ? "(끝남)" : "";
            if (info.shortNameHash == BlockState)
            {
                return "방어" + ended;
            }

            return System.Array.IndexOf(AttackStates, info.shortNameHash) >= 0 ? "공격" + ended : "기타";
        }

        private static void SampleFight(Tracked tracked, AbstractCustomer customer, string state)
        {
            if (state != "MeetUpState")
            {
                return;
            }

            var ctx = customer.Fsm?.Context;
            AbstractCustomer other = ctx?.Partner?.Customer;

            // 짝이 정해진 순간 각자 설 자리를 한 번 남긴다. 걸어가다 갇혀 싸움 표본이 안 잡히는 쌍도 위치는 보인다.
            if (other != null && MeetLogged.Add((tracked.Id, customer)))
            {
                Write($"#{tracked.Id}   MEET {(customer.Data != null ? customer.Data.name : customer.name)} 설 자리 @{ctx.MeetPoint.x:F1},{ctx.MeetPoint.z:F1}");
            }

            if (other == null || other.GetInstanceID() < customer.GetInstanceID())
            {
                return;
            }

            if (other.Fsm?.Machine?.Current?.GetType().Name != "MeetUpState")
            {
                return;
            }

            // 둘 다 싸움 연출을 트는 중일 때만 싸우는 중으로 본다. 루트 모션으로 자리에서 밀려도 표본에서 빠지지 않게 자리 거리는 보지 않는다.
            if (customer.ActionAnimator == null || !customer.ActionAnimator.IsPlaying || other.ActionAnimator == null || !other.ActionAnimator.IsPlaying)
            {
                return;
            }

            if (_nextFightSample.TryGetValue(customer, out float next) && Time.time < next)
            {
                return;
            }

            _nextFightSample[customer] = Time.time + 0.25f;

            Vector3 ab = other.transform.position - customer.transform.position;
            ab.y = 0f;
            float angleA = Vector3.Angle(Vector3.ProjectOnPlane(customer.transform.forward, Vector3.up), ab);
            float angleB = Vector3.Angle(Vector3.ProjectOnPlane(other.transform.forward, Vector3.up), -ab);
            FightSamples.Add((ab.magnitude, angleA, angleB));
            Vector3 driftA = customer.transform.position - ctx.MeetPoint;
            driftA.y = 0f;
            FightDrift.Add(driftA.magnitude);

            // 둘이 지금 무슨 동작인지. 한 명은 공격, 한 명은 방어여야 하고 차례마다 바뀌어야 한다.
            string roleA = FightRole(customer);
            string roleB = FightRole(other);
            string pair = roleA == "공격" && roleB == "방어" || roleA == "방어" && roleB == "공격" ? "번갈아" : $"{roleA}/{roleB}";
            if (roleA.Contains("끝남") || roleB.Contains("끝남"))
            {
                _endedSamples++;
            }
            FightRoles[pair] = FightRoles.TryGetValue(pair, out int rn) ? rn + 1 : 1;
            string key = $"{tracked.Id}:{customer.GetInstanceID()}";
            if (!LastRole.TryGetValue(key, out string prevRole) || prevRole != roleA)
            {
                if (prevRole != null && roleA != prevRole)
                {
                    _roleSwaps++;
                }

                LastRole[key] = roleA;
            }

            Write($"#{tracked.Id}   FIGHT 거리 {ab.magnitude:F2}m 바라보는 각 {angleA:F0}°/{angleB:F0}° 동작 {roleA}/{roleB} @{customer.transform.position.x:F1},{customer.transform.position.z:F1}");
        }

        /// <summary>BoardState에 들어간 시각과 탑승 직전 몸 위치. 탑승하는 순간 걸린 시간과 순간이동 거리를 잰다.</summary>
        private static readonly Dictionary<AbstractCustomer, (float start, Vector3 lastPos, float startDist)> Boarding = new();
        private static readonly List<(float seconds, float jump, string who)> BoardResults = new();

        private static void TrackBoarding(Tracked tracked, AbstractCustomer customer, string state)
        {
            bool boarded = customer.Boarding != null && customer.Boarding.IsBoarded;
            string who = customer.Data != null ? customer.Data.name : customer.name;

            if (!boarded && state == "BoardState")
            {
                if (!Boarding.TryGetValue(customer, out var b))
                {
                    Vector3 car = tracked.Session.Car != null ? tracked.Session.Car.transform.position : customer.transform.position;
                    Vector3 d = car - customer.transform.position;
                    d.y = 0f;
                    Boarding[customer] = (Time.time, customer.transform.position, d.magnitude);
                }
                else
                {
                    Boarding[customer] = (b.start, customer.transform.position, b.startDist);
                }

                return;
            }

            if (boarded && Boarding.TryGetValue(customer, out var done))
            {
                Boarding.Remove(customer);
                Vector3 jump = customer.transform.position - done.lastPos;
                jump.y = 0f;
                float seconds = Time.time - done.start;
                BoardResults.Add((seconds, jump.magnitude, who));
                Write($"#{tracked.Id}   BOARDED {who} {seconds:F1}s 출발 거리(차 중심) {done.startDist:F1}m 순간이동 {jump.magnitude:F1}m 탑승 직전 @{done.lastPos.x:F1},{done.lastPos.z:F1}");
            }
        }

        /// <summary>손님별 직전 프레임의 탑승 여부. 내리는 순간(true → false)을 잡아 그 자리 1.2m 안에 다른 사람이 서 있었는지 센다.</summary>
        private static readonly Dictionary<AbstractCustomer, bool> WasBoarded = new();
        private static int _landings;
        private static int _landingOverlaps;

        private static void TrackLanding(Tracked tracked, AbstractCustomer customer)
        {
            bool boarded = customer.Boarding != null && customer.Boarding.IsBoarded;
            bool had = WasBoarded.TryGetValue(customer, out bool was);
            WasBoarded[customer] = boarded;
            if (!had || !was || boarded || (tracked.LastPhase != VisitPhase.Unloading && tracked.LastPhase != VisitPhase.Waiting && tracked.LastPhase != VisitPhase.Arriving))
            {
                return;
            }

            _landings++;
            Vector3 p = customer.transform.position;
            foreach (var person in _Works.CJW.Scripts.Cars.CarHitTargets.Targets)
            {
                if (person == null || !person.CanBeHit || (person is Component c && c.GetComponentInParent<AbstractCustomer>() == customer))
                {
                    continue;
                }

                Vector3 d = person.HitPosition - p;
                d.y = 0f;
                if (d.magnitude < 1.2f)
                {
                    _landingOverlaps++;
                    Write($"#{tracked.Id}   LANDING OVERLAP {(customer.Data != null ? customer.Data.name : customer.name)} @{p.x:F1},{p.z:F1} 옆 사람 {d.magnitude:F2}m");
                    break;
                }
            }
        }

        /// <summary>차 밖에 선 손님의 몸 중심이 차 콜라이더 안에 들어간 표본. 0.25초마다 한 번씩 본다.</summary>
        private static readonly Dictionary<AbstractCustomer, float> _nextInsideCheck = new();
        private static readonly Dictionary<Component, Collider[]> CarColliders = new();
        private static readonly Dictionary<Component, (Vector3 pos, float time)> CarLastPos = new();
        private static int _insideSamples;
        private static int _standSamples;
        private static readonly Dictionary<string, int> InsideByState = new();

        private static void CheckInsideCar(Tracked tracked, AbstractCustomer customer, string state)
        {
            if (customer.Boarding == null || customer.Boarding.IsBoarded || customer.IsKnockedDown)
            {
                return;
            }

            if (_nextInsideCheck.TryGetValue(customer, out float next) && Time.time < next)
            {
                return;
            }

            _nextInsideCheck[customer] = Time.time + 0.25f;
            _standSamples++;
            Vector3 p = customer.transform.position;

            foreach (var sensor in _Works.CJW.Scripts.Cars.CarTraffic.Sensors)
            {
                if (sensor is not Component sc || sc == null)
                {
                    continue;
                }

                var car = sc.GetComponentInParent<_Works.CJW.Scripts.Cars.Car>();
                if (car == null || (car.transform.position - p).sqrMagnitude > 64f)
                {
                    continue;
                }

                // 레이싱 차에는 물리 콜라이더가 없다. 차 몸체와 거의 같은 NavMeshObstacle 상자로 잰다.
                float gap = _Works.CJW.Scripts.Customers.Visit.CustomerFSM.CarLandings.DistanceToBody(car, p);
                if (gap > 0.2f)
                {
                    continue;
                }

                _insideSamples++;
                bool moving = CarLastPos.TryGetValue(car, out var last) && (car.transform.position - last.pos).magnitude > 0.2f * Mathf.Max(0.05f, Time.time - last.time);
                string key = $"{state}/{(moving ? "움직이는 차" : "서 있는 차")}";
                InsideByState[key] = InsideByState.TryGetValue(key, out int n) ? n + 1 : 1;
                Write($"#{tracked.Id}   INSIDE CAR {(customer.Data != null ? customer.Data.name : customer.name)} {state} 몸체까지 {gap:F2}m @{p.x:F1},{p.z:F1} ← {car.name} {(moving ? "움직이는 중" : "서 있음")} @{car.transform.position.x:F1},{car.transform.position.z:F1}");
                goto done;
            }

            done:
            foreach (var sensor in _Works.CJW.Scripts.Cars.CarTraffic.Sensors)
            {
                // 0.25초에 한 번만 갱신해야 움직임이 보인다. 매 검사마다 갱신하면 같은 프레임 값끼리 비교하게 된다.
                if (sensor is Component sc2 && sc2 != null && sc2.GetComponentInParent<_Works.CJW.Scripts.Cars.Car>() is { } car2
                    && (!CarLastPos.TryGetValue(car2, out var prev) || Time.time - prev.time >= 0.25f))
                {
                    CarLastPos[car2] = (car2.transform.position, Time.time);
                }
            }
        }

        /// <summary>차를 때리는 동작 중인 손님과 가장 가까운 남의 차 몸체 사이 거리. 0.5초마다 잰다.</summary>
        private static readonly List<float> PunchGaps = new();
        private static readonly Dictionary<AbstractCustomer, float> _nextPunch = new();

        private static void SamplePunch(Tracked tracked, AbstractCustomer customer, string state)
        {
            if (state != "VandalizeState" || customer.ActionAnimator == null || !customer.ActionAnimator.IsPlaying)
            {
                return;
            }

            if (_nextPunch.TryGetValue(customer, out float next) && Time.time < next)
            {
                return;
            }

            _nextPunch[customer] = Time.time + 0.5f;
            Vector3 p = customer.transform.position;
            float best = float.PositiveInfinity;
            foreach (var sensor in _Works.CJW.Scripts.Cars.CarTraffic.Sensors)
            {
                if (sensor is Component sc && sc != null && sc.GetComponentInParent<_Works.CJW.Scripts.Cars.Car>() is { } car && car != tracked.Session.Car)
                {
                    best = Mathf.Min(best, _Works.CJW.Scripts.Customers.Visit.CustomerFSM.CarLandings.DistanceToBody(car, p));
                }
            }

            // 자판기를 때리는 손님은 근처에 남의 차가 없어 무한대가 나온다. 차 때리기만 센다.
            if (best < 5f)
            {
                PunchGaps.Add(best);
                Write($"#{tracked.Id}   PUNCH {(customer.Data != null ? customer.Data.name : customer.name)} 차 몸체까지 {best:F2}m @{p.x:F1},{p.z:F1}");
            }
        }

        /// <summary>공격 차례마다 공격하는 손(양손 중 가까운 쪽)이 상대 머리·가슴에 가장 가까이 간 거리. 매 프레임 보고 차례가 끝나면 남긴다.</summary>
        private static readonly Dictionary<AbstractCustomer, float> ReachThisTurn = new();
        private static readonly List<float> ReachPerTurn = new();
        private static readonly List<float> BodyGap = new();

        private static void SampleReach(AbstractCustomer customer, string state)
        {
            bool attacking = state == "MeetUpState" && FightRole(customer) == "공격";
            AbstractCustomer other = customer.Fsm?.Context?.Partner?.Customer;

            if (!attacking || other == null)
            {
                // 공격 차례가 끝났다. 이번 차례에 가장 가까이 간 값을 남긴다.
                if (ReachThisTurn.TryGetValue(customer, out float best))
                {
                    ReachPerTurn.Add(best);
                    ReachThisTurn.Remove(customer);
                }

                return;
            }

            Animator me = customer.GetComponentInChildren<Animator>();
            Animator them = other.GetComponentInChildren<Animator>();
            if (me == null || them == null || !me.isHuman || !them.isHuman)
            {
                return;
            }

            float nearest = float.PositiveInfinity;
            foreach (HumanBodyBones hand in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
            {
                Transform h = me.GetBoneTransform(hand);
                if (h == null)
                {
                    continue;
                }

                foreach (HumanBodyBones target in new[] { HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
                {
                    Transform t = them.GetBoneTransform(target);
                    if (t != null)
                    {
                        nearest = Mathf.Min(nearest, Vector3.Distance(h.position, t.position));
                    }
                }
            }

            if (float.IsInfinity(nearest))
            {
                return;
            }

            ReachThisTurn[customer] = ReachThisTurn.TryGetValue(customer, out float prev) ? Mathf.Min(prev, nearest) : nearest;

            Vector3 gap = other.transform.position - customer.transform.position;
            gap.y = 0f;
            BodyGap.Add(gap.magnitude);
        }

        private static int _hits;

        /// <summary>치여 쓰러진 손님과 치인 자리. 일어설 때 얼마나 날아갔는지 잰다.</summary>
        private static readonly Dictionary<AbstractCustomer, Vector3> Knocked = new();

        /// <summary>차에 치일 때마다 누가 어떤 상태에서 어느 차에 얼마나 빠르게 치였는지 남긴다.</summary>
        private static void OnCarHit(_Works.CJW.Scripts.Cars.CarHitInfo hit)
        {
            _hits++;
            string who = hit.Target is Component c ? c.GetComponentInParent<AbstractCustomer>()?.name ?? c.name : "?";
            string state = "";
            if (hit.Target is Component tc && tc.GetComponentInParent<AbstractCustomer>() is AbstractCustomer customer)
            {
                foreach (Tracked t in Visits)
                {
                    if (t.CustomerStates.TryGetValue(customer, out string st))
                    {
                        state = $" #{t.Id} {st}";
                        break;
                    }
                }
            }

            string carVisit = "";
            foreach (Tracked t in Visits)
            {
                if (!t.Completed && t.Session.Car == hit.Car)
                {
                    carVisit = $" (차 #{t.Id} {t.LastPhase})";
                    break;
                }
            }

            if (hit.Target is Component kc && kc.GetComponentInParent<AbstractCustomer>() is AbstractCustomer knocked && !Knocked.ContainsKey(knocked))
            {
                Knocked[knocked] = hit.Point;
            }

            Write($"HIT {who}{state} ← {hit.Car.name}{carVisit} {hit.Speed:F1}m/s @{hit.Point.x:F1},{hit.Point.z:F1}");
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
            else if (type == LogType.Log && (key.Contains("반대편(") || key.Contains("빠져나와") || key.Contains("다시 출발")))
            {
                Write(key);
            }
            else if (type == LogType.Warning)
            {
                Warnings[key] = Warnings.TryGetValue(key, out int n) ? n + 1 : 1;
                Write($"⚠ {key}");
            }
        }

        /// <summary>차 조향 모듈의 내부 상태를 한 줄로. 퇴장 중 멈춘 원인을 보려는 진단용이다.</summary>
        private static string SteeringDump(_Works.CJW.Scripts.Cars.Car car)
        {
            var module = car.GetComponentInChildren<_Works.CJW.Scripts.Cars.CarSteeringMoveModule>();
            if (module == null)
            {
                return "";
            }

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            string[] names = { "_hasDestination", "_pendingFrames", "_speed", "_reversing", "_backoffRemaining", "_blockedTime", "_giveUp", "_retryCount", "_remaining", "_endDistance" };
            var parts = new List<string>();
            foreach (string n in names)
            {
                object v = typeof(_Works.CJW.Scripts.Cars.CarSteeringMoveModule).GetField(n, flags)?.GetValue(module);
                parts.Add($"{n.TrimStart('_')}={(v is float f ? f.ToString("F1") : v)}");
            }

            return string.Join(" ", parts);
        }

        private static float Elapsed() => Time.time - _startTime;

        private static void Write(string line)
        {
            Log.AppendLine($"[{Elapsed(),6:0.0}] {line}");
        }
    }
}
