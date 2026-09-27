using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Sounds;
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

        private const int TargetCount = 3;
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

        /// <summary>주유가 끝난 뒤 차가 출발(Waiting을 벗어남)해야 하는 시간(초).</summary>
        private const float DepartWithin = 17f;

        private enum Step { WaitArrive, IdleWait, Fueling, EarlyRelease, FuelingFull, AfterEnd, Depart, Done }

        private sealed class Target
        {
            public AbstractCustomer Customer;
            public CustomerState State;
            public FuelDoor Door;
            public string Name;
            public Step Step;
            public float StepAt;
            public float NearSince = -1f;
            public int EndedCount;
            public Action OnEnded;
            public int CompletedCount;
            public Action OnCompleted;

            /// <summary>주유기에 닿기 전에 주유를 끝내 보는 대상. 손님이 걸어가는 도중에 주유가 끝나도 놓치지 않는지 본다.</summary>
            public bool Early;

            /// <summary>차에서 내리지 않고 주유를 기다리는 손님(InCarFuelState).</summary>
            public bool InCar;
        }

        private static readonly List<Target> Targets = new();
        private static readonly HashSet<AbstractCustomer> Seen = new();
        private static readonly StringBuilder Log = new();
        private static readonly List<string> Errors = new();
        private static FuelInjector _injector;
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

            // 플레이어의 주입기는 쓰지 않는다. 플레이어가 매 프레임 "키를 누르고 있는지" 보고 주유를 취소하므로
            // 입력을 넣을 수 없는 여기서는 게이지가 차기 전에 끊긴다. 설정(레이어 마스크·시간)만 베낀 따로 된 주입기를 쓴다.
            FuelInjector playerInjector = Object.FindFirstObjectByType<FuelInjector>();
            _injector = new GameObject("[FuelProbe] Injector").AddComponent<FuelInjector>();
            if (playerInjector != null)
            {
                EditorUtility.CopySerialized(playerInjector, _injector);
            }

            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            Write($"시작 {SessionState.GetInt(RunIndexKey, 1)}회차, 주입기: 임시(설정은 {(playerInjector != null ? playerInjector.name : "기본값")})");
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

                CustomerState state = c.Fsm?.Machine?.Current;
                bool inCar = state is InCarFuelState;
                if (!inCar && (state is not OilingState || c.Fsm.Context?.RentedPosition == null))
                {
                    continue;
                }

                // 차 안 손님은 한 명만, 걸어가는 손님은 두 명까지 본다.
                if (inCar ? Targets.Any(x => x.InCar) : Targets.Count(x => !x.InCar) >= 2)
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
                    InCar = inCar,
                    Early = !inCar && Targets.Count(x => !x.InCar) == 1,
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
                Write(inCar
                    ? $"대상 {t.Name}: 차 {c.Session.Car.name}, 차 안에서 주유 기다림"
                    : $"대상 {t.Name}: 차 {c.Session.Car.name}, 주유기 {c.Fsm.Context.RentedPosition.Position}");
            }
        }

        private static void Advance(Target t, float now, float dt)
        {
            AbstractCustomer c = t.Customer;
            CustomerState current = c.Fsm?.Machine?.Current;

            switch (t.Step)
            {
                case Step.WaitArrive:
                    if (t.InCar)
                    {
                        // 차 안에서 2초 기다리게 둔 뒤 주유를 끝낸다. 그동안은 계속 기다려야 한다.
                        if (now - t.StepAt < 2f)
                        {
                            return;
                        }

                        Check(ReferenceEquals(current, t.State), $"{t.Name} 주유 전 차 안에서 계속 기다림: {current?.GetType().Name ?? "-"}");
                        t.Door.NotifyFuelingStarted();
                        t.Door.NotifyFuelingCompleted();
                        Write($"{t.Name}: 차 안 손님 주유 완료");
                        t.Step = Step.AfterEnd;
                        t.StepAt = now;
                        return;
                    }

                    if (t.Early && ReferenceEquals(current, t.State) && !IsWaitingOn(t.Door, c))
                    {
                        // 손님이 아직 주유기로 걸어가는 중이다. 플레이어가 먼저 주유를 끝낸 셈 치고 주유구에 바로 완료를 알린다.
                        t.Door.NotifyFuelingStarted();
                        t.Door.NotifyFuelingCompleted();
                        Write($"{t.Name}: 주유기에 닿기 전에 주유 완료 (주유기까지 {Planar(c.transform.position, c.Fsm.Context.RentedPosition?.Position ?? c.transform.position):F1}m 남음)");
                        t.Step = Step.AfterEnd;
                        t.StepAt = now;
                        return;
                    }

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
                    // 주유 뒤에 말하는 손님(네고)은 아직 말하지 않았다. 이미 말을 꺼낸 손님만 본다.
                    if (c.Fsm.Context.LineIndex > 0)
                    {
                        Check(bubble != null, $"{t.Name} 주유 기다리는 동안 말풍선 보임: {(bubble != null ? bubble.GetComponentInChildren<TMPro.TMP_Text>()?.text : "없음")}");
                    }
                    else
                    {
                        Write($"{t.Name}: 주유 전에 말하지 않는 손님이라 말풍선 검사 건너뜀");
                    }

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
                    CheckFuelSound(t, true);
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
                    CheckFuelSound(t, false);
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
                    CheckFuelSound(t, false);

                    // 주유를 받았으니 요구하던 말풍선("가득이요")은 접혀야 한다. 주유 뒤에 할 대사가 있는 손님은 새 말풍선으로 바뀐다.
                    SpeechBubble left = Object.FindObjectsByType<SpeechBubble>(FindObjectsSortMode.None)
                        .FirstOrDefault(b => b.isActiveAndEnabled && Planar(b.transform.position, c.transform.position) < 1f);
                    bool nextLine = current is SpeechState;
                    Check(left == null || nextLine,
                          $"{t.Name} 주유 끝나면 요구 말풍선 접힘: {(left != null ? $"{(nextLine ? "다음 대사로 바뀜" : "남아 있음")} '{left.GetComponentInChildren<TMPro.TMP_Text>()?.text}'" : "없음")}");
                    t.Step = Step.Depart;
                    t.StepAt = now;
                    return;

                case Step.Depart:
                    VisitPhase phase = c.Session != null ? c.Session.Phase : VisitPhase.None;
                    bool departed = phase != VisitPhase.Waiting;
                    if (!departed && now - t.StepAt < DepartWithin)
                    {
                        return;
                    }

                    Check(departed, $"{t.Name} 주유 끝나고 {now - t.StepAt:F2}s 뒤 차가 출발 단계로: {phase}");
                    Cleanup(t);
                    t.Step = Step.Done;
                    return;
            }
        }

        /// <summary>주유하는 동안 차가 주유 소리를 반복하고, 손을 떼면 끄는지.</summary>
        private static void CheckFuelSound(Target t, bool fueling)
        {
            ISoundEmitter sound = t.Customer.Session?.Car != null ? t.Customer.Session.Car.Sound : null;
            string loop = sound?.CurrentLoop != null ? sound.CurrentLoop.name : "없음";
            bool isFuelLoop = loop == "Refueling";
            Check(sound != null && isFuelLoop == fueling,
                  $"{t.Name} {(fueling ? "주유 중 주유 소리 켜짐" : "주유 멈추면 주유 소리 꺼짐")}: 반복 소리 {loop}");
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
