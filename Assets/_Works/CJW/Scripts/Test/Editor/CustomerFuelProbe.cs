using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using _Works.JJH._02_Scripts.Agents.Players.Grabs;
using _Works.JJH._02_Scripts.Objects;
using _Works.JYG._Scripts.UI.SpeechBubble;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>플레이어 주유를 플레이 모드에서 확인하는 디버그용 도구. 입력을 넣을 수 없어 플레이어의 <see cref="FuelInjector"/>를 직접 부른다.
    /// 주유기에 도착해 기다리는 손님을 골라 (1) 주유 전에는 계속 기다리는지 (2) 차 옆에서 쏜 레이가 주유구에 걸리는지
    /// (3) 주유 중에도 기다리는지 (4) 주유가 끝나면 다음 행동으로 넘어가고 구독·자리를 정리하는지 본다.
    /// 결과는 Temp/CustomerSim/fuel_N.txt. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class CustomerFuelProbe
    {
        private const string RunningKey = "CJW.CustomerFuel.Running";
        private const string RunIndexKey = "CJW.CustomerFuel.RunIndex";
        private const string OutDir = "Temp/CustomerSim";

        private const int TargetCount = 2;
        private const float TimeScale = 3f;
        private const float GiveUpSeconds = 400f;

        /// <summary>주유기 자리에서 이만큼(m) 안에 이 시간(초) 머물면 도착해 기다리는 것으로 본다.</summary>
        private const float ArrivedDistance = 2f;
        private const float ArrivedHold = 1f;

        /// <summary>주유 전에 가만히 두고 기다리는지 보는 시간(초).</summary>
        private const float IdleCheck = 3f;
        private const float FuelSeconds = 5f;

        /// <summary>게이지가 차기 전에 손을 떼는 시점(초).</summary>
        private const float EarlyReleaseSeconds = 1f;
        /// <summary>주유가 끝난 뒤 다음 행동으로 넘어가야 하는 시간(초).</summary>
        private const float ReleaseWithin = 2f;

        private enum Step { WaitArrive, IdleWait, Fueling, EarlyRelease, FuelingFull, AfterEnd, Done }

        private sealed class Target
        {
            public AbstractCustomer Customer;
            public OilingState State;
            public FuelDoor Door;
            public string Name;
            public Step Step;
            public float StepAt;
            public float NearSince = -1f;
            public int EndedCount;
            public Action OnEnded;
            public int CompletedCount;
            public Action OnCompleted;
        }

        private static readonly List<Target> Targets = new();
        private static readonly HashSet<AbstractCustomer> Seen = new();
        private static readonly StringBuilder Log = new();
        private static readonly List<string> Errors = new();
        private static FuelInjector _injector;
        private static bool _injectorCreated;
        private static int _pass;
        private static int _fail;
        private static float _startTime;
        private static float _lastTick;

        private static readonly FieldInfo EndedField =
            typeof(FuelDoor).GetField(nameof(FuelDoor.OnFuelingCompleted), BindingFlags.Instance | BindingFlags.NonPublic);

        static CustomerFuelProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Customer Fuel Test")]
        private static void StartTest()
        {
            SessionState.SetInt(RunIndexKey, SessionState.GetInt(RunIndexKey, 0) + 1);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Begin();
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(RunningKey, false))
            {
                Finish("플레이 모드가 꺼져 중단");
            }
        }

        private static void Begin()
        {
            Targets.Clear();
            Seen.Clear();
            Log.Clear();
            Errors.Clear();
            _pass = 0;
            _fail = 0;
            _startTime = Time.time;
            _lastTick = Time.time;
            Time.timeScale = TimeScale;

            _injector = Object.FindFirstObjectByType<FuelInjector>();
            _injectorCreated = _injector == null;
            if (_injectorCreated)
            {
                _injector = new GameObject("[FuelProbe] Injector").AddComponent<FuelInjector>();
            }

            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            Write($"시작 {SessionState.GetInt(RunIndexKey, 1)}회차, 주입기: {(_injectorCreated ? "씬에 없어 임시로 만듦" : _injector.name)}");
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                Errors.Add(condition.Split('\n')[0]);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            float now = Time.time;
            float dt = now - _lastTick;
            _lastTick = now;

            if (now - _startTime > GiveUpSeconds)
            {
                Check(false, $"{GiveUpSeconds}초 안에 끝나지 않음");
                Finish("시간 초과");
                return;
            }

            PickTargets();

            foreach (Target t in Targets)
            {
                Advance(t, now, dt);
            }

            if (Targets.Count >= TargetCount && Targets.All(t => t.Step == Step.Done))
            {
                Finish("완료");
            }
        }

        /// <summary>OilingState에 들어간 손님을 고른다. 주유기 자리를 빌린 손님만 — 대기열로 간 손님은 주유구 앞에 서지 않는다.</summary>
        private static void PickTargets()
        {
            if (Targets.Count >= TargetCount || _injector.IsFueling)
            {
                return;
            }

            foreach (AbstractCustomer c in Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None))
            {
                if (Targets.Count >= TargetCount || Seen.Contains(c))
                {
                    continue;
                }

                if (c.Fsm?.Machine?.Current is not OilingState state || c.Fsm.Context?.RentedPosition == null)
                {
                    continue;
                }

                Seen.Add(c);
                var t = new Target
                {
                    Customer = c,
                    State = state,
                    Door = c.Session?.Car != null ? c.Session.Car.GetComponentInChildren<FuelDoor>(true) : null,
                    Name = $"{c.name}#{c.GetInstanceID()}",
                    Step = Step.WaitArrive,
                    StepAt = Time.time,
                };

                if (t.Door == null)
                {
                    Check(false, $"{t.Name}의 차({c.Session?.Car?.name ?? "없음"})에 FuelDoor가 없음");
                    t.Step = Step.Done;
                    Targets.Add(t);
                    continue;
                }

                t.OnEnded = () => t.EndedCount++;
                t.Door.OnFuelingEnded += t.OnEnded;
                t.OnCompleted = () => t.CompletedCount++;
                t.Door.OnFuelingCompleted += t.OnCompleted;
                Targets.Add(t);
                Write($"대상 {t.Name}: 차 {c.Session.Car.name}, 주유기 {c.Fsm.Context.RentedPosition.Position}");
            }
        }

        private static void Advance(Target t, float now, float dt)
        {
            AbstractCustomer c = t.Customer;
            CustomerState current = c.Fsm?.Machine?.Current;

            switch (t.Step)
            {
                case Step.WaitArrive:
                    if (!ReferenceEquals(current, t.State))
                    {
                        Write($"{t.Name}: 도착 전에 OilingState를 벗어남({current?.GetType().Name ?? "-"}) — 이동 실패로 보고 제외");
                        t.Step = Step.Done;
                        return;
                    }

                    // 도착했는지는 주유구 구독으로 판단한다. 구독이 걸렸다는 건 MoveAndWait가 Done으로 끝났다는 뜻이다.
                    if (!IsWaitingOn(t.Door, c))
                    {
                        return;
                    }

                    if (t.NearSince < 0f)
                    {
                        t.NearSince = now;
                    }

                    if (now - t.NearSince < ArrivedHold)
                    {
                        return;
                    }

                    Vector3 spot = c.Fsm.Context.RentedPosition?.Position ?? c.transform.position;
                    float dist = Planar(c.transform.position, spot);
                    Write($"{t.Name}: 주유기 도착({dist:F2}m), 대기 시작 (출발 후 {now - t.StepAt:F1}s)");
                    Check(dist <= ArrivedDistance, $"{t.Name} 주유기 자리 근처에서 기다림: {dist:F2}m");

                    // 주유를 기다리는 동안 머리 위에 요구 말풍선("가득이요")이 떠 있어야 한다.
                    SpeechBubble bubble = Object.FindObjectsByType<SpeechBubble>(FindObjectsSortMode.None)
                        .FirstOrDefault(b => b.isActiveAndEnabled && Planar(b.transform.position, c.transform.position) < 1f);
                    Check(bubble != null, $"{t.Name} 주유 기다리는 동안 말풍선 보임: {(bubble != null ? bubble.GetComponentInChildren<TMPro.TMP_Text>()?.text : "없음")}");

                    t.Step = Step.IdleWait;
                    t.StepAt = now;
                    return;

                case Step.IdleWait:
                    if (now - t.StepAt < IdleCheck)
                    {
                        return;
                    }

                    Check(ReferenceEquals(current, t.State) && !t.Door.IsFueling,
                          $"{t.Name} 주유 전 {IdleCheck}초 동안 계속 기다림: 상태 {current?.GetType().Name ?? "-"}");

                    CheckRaycast(t);

                    if (_injector.IsFueling)
                    {
                        // 주입기는 하나라 다른 손님 주유가 끝날 때까지 미룬다.
                        return;
                    }

                    bool started = _injector.TryStartFueling(t.Door);
                    Check(started && t.Door.IsFueling && _injector.IsFueling,
                          $"{t.Name} 주유 시작: TryStartFueling={started}, door.IsFueling={t.Door.IsFueling}");
                    t.Step = Step.Fueling;
                    t.StepAt = now;
                    return;

                case Step.Fueling:
                    // 게이지가 차기 전에 손을 뗀다. 주유를 마친 게 아니므로 손님은 계속 기다려야 한다.
                    _injector.TickFueling(dt);
                    if (now - t.StepAt < EarlyReleaseSeconds)
                    {
                        return;
                    }

                    _injector.CancelFueling();
                    Check(!t.Door.IsFueling && t.CompletedCount == 0,
                          $"{t.Name} 중간에 손 뗌: door.IsFueling={t.Door.IsFueling}, 완료 {t.CompletedCount}회");
                    t.Step = Step.EarlyRelease;
                    t.StepAt = now;
                    return;

                case Step.EarlyRelease:
                    if (now - t.StepAt < 1f)
                    {
                        return;
                    }

                    Check(ReferenceEquals(current, t.State), $"{t.Name} 중간에 손 떼도 계속 기다림: {current?.GetType().Name ?? "-"}");
                    bool restarted = _injector.TryStartFueling(t.Door);
                    Check(restarted, $"{t.Name} 주유 다시 시작: TryStartFueling={restarted}");
                    t.Step = Step.FuelingFull;
                    t.StepAt = now;
                    return;

                case Step.FuelingFull:
                    // 게이지가 다 차면 주입기가 스스로 끝낸다. 손을 떼지 않는다.
                    _injector.TickFueling(dt);
                    if (_injector.IsFueling)
                    {
                        if (now - t.StepAt > FuelSeconds + 3f)
                        {
                            Check(false, $"{t.Name} {now - t.StepAt:F1}s 동안 게이지가 차도 주유가 끝나지 않음");
                            _injector.CancelFueling();
                            t.Step = Step.Done;
                        }

                        return;
                    }

                    Check(!t.Door.IsFueling && t.CompletedCount == 1,
                          $"{t.Name} 게이지 다 차서 주유 완료({now - t.StepAt:F1}s): door.IsFueling={t.Door.IsFueling}, 완료 {t.CompletedCount}회");
                    t.Step = Step.AfterEnd;
                    t.StepAt = now;
                    return;

                case Step.AfterEnd:
                    bool moved = !ReferenceEquals(current, t.State);
                    if (!moved && now - t.StepAt < ReleaseWithin)
                    {
                        return;
                    }

                    Check(moved, $"{t.Name} 주유 끝나고 {now - t.StepAt:F2}s 뒤 다음 행동으로: {current?.GetType().Name ?? "-"}");
                    Check(!IsWaitingOn(t.Door, c), $"{t.Name} 주유구 구독 해제");
                    Check(c.Fsm?.Context?.RentedPosition == null, $"{t.Name} 주유기 자리 반납");
                    Cleanup(t);
                    t.Step = Step.Done;
                    return;
            }
        }

        /// <summary>플레이어가 차 왼쪽 옆 1.5m, 눈높이 근처에서 주유구를 보고 쏜 레이가 FuelDoor 레이어 마스크로 이 주유구에 걸리는지.</summary>
        private static void CheckRaycast(Target t)
        {
            Transform car = t.Customer.Session?.Car != null ? t.Customer.Session.Car.transform : null;
            if (car == null)
            {
                return;
            }

            Vector3 door = t.Door.transform.position;
            Vector3 eye = door - car.right * 1.5f + Vector3.up * 0.6f;
            bool hit = Physics.Raycast(eye, (door - eye).normalized, out RaycastHit info, 3f, _injector.FuelDoorLayerMask);
            FuelDoor found = hit ? info.collider.GetComponent<FuelDoor>() : null;
            Check(found == t.Door, $"{t.Name} 차 옆에서 쏜 레이가 주유구에 걸림: {(hit ? info.collider.name : "안 맞음")}, 마스크 {_injector.FuelDoorLayerMask.value}");
        }

        /// <summary>이 손님이 주유구의 OnFuelingCompleted를 구독 중인지. 로컬 함수로 구독하므로 대상 객체의 타입 이름으로 본다.</summary>
        private static bool IsWaitingOn(FuelDoor door, AbstractCustomer c)
        {
            if (EndedField?.GetValue(door) is not Delegate d)
            {
                return false;
            }

            return d.GetInvocationList().Any(x => x.Target != null && x.Target.GetType().DeclaringType == typeof(OilingState)
                                                  || x.Method.DeclaringType?.DeclaringType == typeof(OilingState)
                                                  || x.Method.DeclaringType == typeof(OilingState));
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void Cleanup(Target t)
        {
            if (t.Door != null && t.OnEnded != null)
            {
                t.Door.OnFuelingEnded -= t.OnEnded;
                t.OnEnded = null;
            }

            if (t.Door != null && t.OnCompleted != null)
            {
                t.Door.OnFuelingCompleted -= t.OnCompleted;
                t.OnCompleted = null;
            }
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
            SessionState.SetBool(RunningKey, false);

            if (_injector != null && _injector.IsFueling)
            {
                _injector.CancelFueling();
            }

            foreach (Target t in Targets)
            {
                if (t.Step != Step.Done)
                {
                    Write($"미완료: {t.Name} 단계 {t.Step}");
                }

                Cleanup(t);
            }

            Log.AppendLine();
            Log.AppendLine($"끝: {reason}. 대상 {Targets.Count}명, 통과 {_pass}, 실패 {_fail}, 에러 로그 {Errors.Count}");
            foreach (IGrouping<string, string> g in Errors.GroupBy(e => e))
            {
                Log.AppendLine($"  에러 x{g.Count()}: {g.Key}");
            }

            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, $"fuel_{SessionState.GetInt(RunIndexKey, 1)}.txt"), Log.ToString());

            Time.timeScale = 1f;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
