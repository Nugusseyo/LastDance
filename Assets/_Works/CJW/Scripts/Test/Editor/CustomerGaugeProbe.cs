using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.JYG._Scripts.UI.GuestUI;
using _Works.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>손님 머리 위 체력·인내심 게이지를 플레이 모드에서 확인하는 디버그용 도구.
    /// 차에서 내린 손님 하나를 골라 (1) 가득일 때 숨는지 (2) 맞으면 체력바가 줄며 뜨는지 (3) 기다리기 시작하면 인내심이 줄며 뜨고
    /// 끝나면 숨는지 (4) 죽으면 둘 다 숨는지 본다. 방문 중 실제로 인내심이 줄어드는 손님도 기록한다.
    /// 결과는 Temp/CustomerSim/gauge.txt. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class CustomerGaugeProbe
    {
        private const string RunningKey = "CJW.CustomerGauge.Running";
        private const string OutPath = "Temp/CustomerSim/gauge.txt";

        private const float TimeScale = 2f;
        private const float GiveUpSeconds = 600f;
        private const float PatienceSeconds = 10f;

        private enum Step { Pick, CheckFull, Hit, CheckHit, BeginWait, CheckWait, EndWait, CheckEndWait, Kill, CheckDead, WatchNatural, Done }

        private static readonly StringBuilder Log = new();
        private static readonly List<string> Errors = new();
        private static readonly HashSet<int> NaturalSeen = new();
        private static int _pass;
        private static int _fail;
        private static float _startTime;
        private static float _nextAt;
        private static float _waitValue;
        private static Step _step;
        private static bool _oddWalkSeen;
        private static AbstractCustomer _target;
        private static WorldGauge _health;
        private static WorldGauge _patience;

        static CustomerGaugeProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Customer Gauge Test")]
        private static void StartTest()
        {
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
            Log.Clear();
            Errors.Clear();
            NaturalSeen.Clear();
            _pass = 0;
            _fail = 0;
            _step = Step.Pick;
            _oddWalkSeen = false;
            _target = null;
            _startTime = Time.time;
            _nextAt = 0f;
            Time.timeScale = TimeScale;

            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            Write("시작");
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
            if (now - _startTime > GiveUpSeconds)
            {
                Check(_step == Step.WatchNatural, $"{GiveUpSeconds}초 안에 끝남 (멈춘 단계 {_step})");
                Check(_oddWalkSeen, "취한 손님(Odd Walk)의 인내심 게이지가 뜨는 걸 봄");
                Finish("시간 초과");
                return;
            }

            CheckGaugeCounts();
            WatchNaturalPatience();

            if (now < _nextAt)
            {
                return;
            }

            Advance(now);
        }

        /// <summary>모든 손님이 게이지를 딱 두 개(체력·인내심) 가져야 한다. 변형 프리팹에 두 벌이 겹치면 여기서 잡힌다.</summary>
        private static void CheckGaugeCounts()
        {
            foreach (AbstractCustomer c in Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None))
            {
                int id = c.GetInstanceID();
                if (!Counted.Add(id))
                {
                    continue;
                }

                int count = c.GetComponentsInChildren<WorldGauge>(true).Length;
                Check(count == 2 && c.Patience != null, $"{c.name}: 게이지 {count}개, 인내심 모듈 {(c.Patience != null ? "있음" : "없음")}");
            }
        }

        private static readonly HashSet<int> Counted = new();

        /// <summary>방문 흐름이 스스로 인내심을 줄이는지 본다. 주유·요구를 기다리는 손님이 나오면 한 번씩 기록한다.</summary>
        private static void WatchNaturalPatience()
        {
            foreach (AbstractCustomer c in Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None))
            {
                if (c == _target || c.Patience == null || !c.Patience.IsWaiting || c.Patience.Normalized >= 0.97f)
                {
                    continue;
                }

                if (NaturalSeen.Add(c.GetInstanceID()))
                {
                    if (c.name.StartsWith("Odd Walk"))
                    {
                        _oddWalkSeen = true;
                    }

                    WorldGauge gauge = Gauge(c, "PatienceGauge");
                    bool shown = gauge != null && gauge.GetComponent<Canvas>().enabled;
                    Check(shown && Mathf.Abs(gauge.NormalizedValue - c.Patience.Normalized) < 0.05f,
                        $"[방문 중] {c.name} 인내심 {c.Patience.Normalized:F2}, 게이지 {(gauge != null ? gauge.NormalizedValue : -1f):F2} 보임 {shown}");
                }
            }
        }

        private static void Advance(float now)
        {
            switch (_step)
            {
                case Step.Pick:
                    _target = Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None)
                        .FirstOrDefault(c => c.Health != null && c.Health.CanBeHit && c.Session != null);
                    if (_target == null)
                    {
                        _nextAt = now + 0.5f;
                        return;
                    }

                    _health = Gauge(_target, "HealthGauge");
                    _patience = Gauge(_target, "PatienceGauge");
                    Write($"대상 {_target.name}: 체력 {_target.Health.CurrentHealth}/{_target.Health.MaxHealth}");
                    Check(_health != null && _patience != null, "체력·인내심 게이지가 자식에 있음");
                    if (_health == null || _patience == null)
                    {
                        Finish("게이지 없음");
                        return;
                    }

                    Next(Step.CheckFull, now, 0.2f);
                    break;

                case Step.CheckFull:
                    Check(!Shown(_health) && !Shown(_patience),
                        $"가득일 때 숨음 (체력 보임 {Shown(_health)}, 인내심 보임 {Shown(_patience)})");
                    Next(Step.Hit, now, 0f);
                    break;

                case Step.Hit:
                    _target.Health.TakeHit(new HitInfo(10f, _target.transform.forward));
                    Next(Step.CheckHit, now, 0.2f);
                    break;

                case Step.CheckHit:
                {
                    float expected = _target.Health.CurrentHealth / _target.Health.MaxHealth;
                    Check(Shown(_health) && Mathf.Abs(_health.NormalizedValue - expected) < 0.01f,
                        $"맞으면 체력바가 뜸 (게이지 {_health.NormalizedValue:F2}, 체력 {expected:F2}, 보임 {Shown(_health)})");
                    Next(Step.BeginWait, now, 0f);
                    break;
                }

                case Step.BeginWait:
                    // 방문 흐름이 이미 기다리던 중일 수 있다. 끄고 이 도구의 기다림으로 덮어 잰다.
                    _target.Patience.Begin(PatienceSeconds);
                    Next(Step.CheckWait, now, 2f);
                    break;

                case Step.CheckWait:
                    _waitValue = _patience.NormalizedValue;
                    Check(Shown(_patience) && _waitValue < 0.9f && _waitValue > 0.6f,
                        $"기다리면 인내심이 줄며 뜸 (2초 뒤 게이지 {_waitValue:F2}, 보임 {Shown(_patience)}, 색 {_patience.GetComponentsInChildren<UnityEngine.UI.Image>().Last().color})");
                    Next(Step.EndWait, now, 0f);
                    break;

                case Step.EndWait:
                    _target.Patience.End();
                    Next(Step.CheckEndWait, now, 0.2f);
                    break;

                case Step.CheckEndWait:
                    Check(!Shown(_patience), $"기다림이 끝나면 인내심이 숨음 (보임 {Shown(_patience)})");
                    Next(Step.Kill, now, 0f);
                    break;

                case Step.Kill:
                    _target.Patience.Begin(PatienceSeconds);
                    _target.Health.TakeHit(new HitInfo(9999f, _target.transform.forward));
                    Next(Step.CheckDead, now, 0.3f);
                    break;

                case Step.CheckDead:
                    Check(_target.Health.IsDead && !Shown(_health) && !Shown(_patience),
                        $"죽으면 둘 다 숨음 (죽음 {_target.Health.IsDead}, 체력 보임 {Shown(_health)}, 인내심 보임 {Shown(_patience)})");
                    Next(Step.WatchNatural, now, 0f);
                    break;

                case Step.WatchNatural:
                    // 방문 흐름에서 인내심이 줄어드는 손님을 몇 명 볼 때까지 기다린다.
                    // 취한 손님(끝없이 기다리는 주유 손님)도 인내심이 보여야 한다.
                    if (NaturalSeen.Count >= 2 && _oddWalkSeen)
                    {
                        Finish("완료");
                    }

                    break;
            }
        }

        private static void Next(Step step, float now, float delay)
        {
            _step = step;
            _nextAt = now + delay;
        }

        private static WorldGauge Gauge(AbstractCustomer c, string name)
        {
            Transform t = c.transform.Find(name);
            return t != null ? t.GetComponent<WorldGauge>() : null;
        }

        private static bool Shown(WorldGauge gauge) => gauge != null && gauge.GetComponent<Canvas>().enabled;

        private static void Check(bool ok, string message)
        {
            if (ok)
            {
                _pass++;
            }
            else
            {
                _fail++;
            }

            Write($"{(ok ? "PASS" : "FAIL")} {message}");
        }

        private static void Write(string line)
        {
            Log.AppendLine($"[{Time.time:F1}] {line}");
        }

        private static void Finish(string reason)
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                return;
            }

            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Counted.Clear();
            Time.timeScale = 1f;

            Write($"끝 ({reason}) — PASS {_pass}, FAIL {_fail}, 에러 로그 {Errors.Count}");
            foreach (string e in Errors.Distinct().Take(20))
            {
                Write($"  에러: {e}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutPath)!);
            File.WriteAllText(OutPath, Log.ToString());
            Debug.Log($"[CustomerGaugeProbe] {reason} — PASS {_pass}, FAIL {_fail}. {OutPath}");

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
