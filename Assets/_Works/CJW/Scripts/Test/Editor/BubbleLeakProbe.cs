using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using _Works.JYG._Scripts.UI.SpeechBubble;
using _Works.Shared.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>말풍선이 사라지지 않는 버그를 찾는 디버그용 도구. 플레이 중 떠 있는 말풍선마다 주인(그 말풍선을 접을 수 있는 손님)이 있는지 본다.
    /// (1) 주인 없이 떠 있는 말풍선 — 아무도 접지 못해 제 시간(주유 대사는 최대 240초)을 다 채운다.
    /// (2) 주유를 원한다는 대사("가득이요")를 띄운 채 차에 타고 떠나는 손님.
    /// 대사를 기다리는 손님을 한 번 때려 (1)을 일부러 일으켜 본다. 결과는 Temp/CustomerSim/bubble_leak.txt. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class BubbleLeakProbe
    {
        private const string RunningKey = "CJW.BubbleLeak.Running";
        private const string OutPath = "Temp/CustomerSim/bubble_leak.txt";

        private const float TimeScale = 3f;
        private const float RunSeconds = 300f;

        /// <summary>주인 없이 이만큼(초) 떠 있으면 버려진 말풍선으로 본다. 끝나는 프레임과 겹쳐 한순간 주인이 없을 수 있어 여유를 둔다.</summary>
        private const float OrphanGrace = 1f;

        /// <summary>주유를 원한다는 대사의 index(HumanDB).</summary>
        private const int FuelRequestLine = 1;

        private static readonly FieldInfo EndSpeechField =
            typeof(CustomerContext).GetField("_endSpeech", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class Seen
        {
            public float FirstAt;
            public float OrphanSince = -1f;
            public string LastOwner = "-";
            public string LastState = "-";
            public bool Reported;
        }

        private static readonly Dictionary<SpeechBubble, Seen> Bubbles = new();
        private static readonly HashSet<AbstractCustomer> LeftWithFuelLine = new();
        private static readonly StringBuilder Log = new();
        private static readonly List<string> Errors = new();
        private static int _pass;
        private static int _fail;
        private static float _startTime;

        private static AbstractCustomer _hitTarget;
        private static SpeechBubble _hitBubble;
        private static float _hitAt = -1f;
        private static bool _hitChecked;

        static BubbleLeakProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CJW/Run Bubble Leak Test")]
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
            Bubbles.Clear();
            LeftWithFuelLine.Clear();
            Log.Clear();
            Errors.Clear();
            _pass = 0;
            _fail = 0;
            _hitTarget = null;
            _hitBubble = null;
            _hitAt = -1f;
            _hitChecked = false;
            _startTime = Time.time;
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
            if (now - _startTime > RunSeconds)
            {
                if (!_hitChecked)
                {
                    Write("대사를 기다리는 손님을 찾지 못해 때리기 검사를 건너뜀");
                }

                Finish("완료");
                return;
            }

            AbstractCustomer[] customers = Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None);
            Dictionary<SpeechBubble, AbstractCustomer> owners = OwnersOf(customers);

            WatchBubbles(owners, now);
            WatchLeaving(customers, owners);
            HitTalkingCustomer(customers, owners, now);
        }

        /// <summary>손님 컨텍스트가 쥐고 있는 "말풍선 접기"에서 말풍선을 거꾸로 찾는다. 쥐고 있는 손님이 그 말풍선의 주인이다.</summary>
        private static Dictionary<SpeechBubble, AbstractCustomer> OwnersOf(AbstractCustomer[] customers)
        {
            var owners = new Dictionary<SpeechBubble, AbstractCustomer>();
            foreach (AbstractCustomer c in customers)
            {
                CustomerContext ctx = c.Fsm?.Context;
                if (ctx != null && EndSpeechField?.GetValue(ctx) is Action end && end.Target is SpeechBubble bubble)
                {
                    owners[bubble] = c;
                }
            }

            return owners;
        }

        private static void WatchBubbles(Dictionary<SpeechBubble, AbstractCustomer> owners, float now)
        {
            SpeechBubble[] active = Object.FindObjectsByType<SpeechBubble>(FindObjectsSortMode.None);

            foreach (SpeechBubble bubble in active)
            {
                if (!Bubbles.TryGetValue(bubble, out Seen seen))
                {
                    seen = new Seen { FirstAt = now };
                    Bubbles[bubble] = seen;
                }

                if (owners.TryGetValue(bubble, out AbstractCustomer owner))
                {
                    seen.OrphanSince = -1f;
                    seen.LastOwner = owner.name;
                    seen.LastState = owner.Fsm?.Machine?.Current?.GetType().Name ?? "-";
                    continue;
                }

                if (seen.OrphanSince < 0f)
                {
                    seen.OrphanSince = now;
                }

                if (!seen.Reported && now - seen.OrphanSince > OrphanGrace)
                {
                    seen.Reported = true;
                    string text = bubble.GetComponentInChildren<TMPro.TMP_Text>()?.text;
                    Check(false, $"주인 없는 말풍선 '{text}': 마지막 주인 {seen.LastOwner}(상태 {seen.LastState}), 뜬 지 {now - seen.FirstAt:F1}초");
                }
            }

            // 꺼진 말풍선은 풀로 돌아간 것이다. 다시 나오면 새 말풍선으로 센다.
            foreach (SpeechBubble gone in Bubbles.Keys.Where(b => b == null || !b.isActiveAndEnabled).ToList())
            {
                Bubbles.Remove(gone);
            }
        }

        /// <summary>"가득이요"를 띄운 채 차에 타고 떠나는 손님. 주유를 원한다는 말이 이미 끝났는데 말풍선만 남은 것이다.</summary>
        private static void WatchLeaving(AbstractCustomer[] customers, Dictionary<SpeechBubble, AbstractCustomer> owners)
        {
            foreach (KeyValuePair<SpeechBubble, AbstractCustomer> pair in owners)
            {
                AbstractCustomer c = pair.Value;
                bool leaving = c.Session != null && c.Session.Phase is VisitPhase.Leaving && c.Boarding != null && c.Boarding.IsBoarded;
                if (!leaving || c.Fsm.Context.LineIndex != FuelRequestLine || !LeftWithFuelLine.Add(c))
                {
                    continue;
                }

                Check(false, $"{c.name}이(가) 주유 대사 '{pair.Key.GetComponentInChildren<TMPro.TMP_Text>()?.text}'를 띄운 채 차 타고 떠남");
            }
        }

        /// <summary>대사가 끝나길 기다리는 손님(SpeechState)을 한 번 때린다. 대사 상태가 끊겨도 말풍선은 주인을 잃지 않아야 한다(접히거나 계속 따라다님).</summary>
        private static void HitTalkingCustomer(AbstractCustomer[] customers, Dictionary<SpeechBubble, AbstractCustomer> owners, float now)
        {
            if (_hitChecked)
            {
                return;
            }

            if (_hitTarget == null)
            {
                foreach (KeyValuePair<SpeechBubble, AbstractCustomer> pair in owners)
                {
                    AbstractCustomer c = pair.Value;
                    if (c.Fsm?.Machine?.Current is SpeechState && c.Health != null && c.Health.CanBeHit)
                    {
                        _hitTarget = c;
                        _hitBubble = pair.Key;
                        _hitAt = now;
                        Write($"대사 중인 {c.name}을(를) 때림: 말풍선 '{pair.Key.GetComponentInChildren<TMPro.TMP_Text>()?.text}'");
                        c.Health.TakeHit(new HitInfo(1f, c.transform.forward));
                        return;
                    }
                }

                return;
            }

            if (now - _hitAt < 2f)
            {
                return;
            }

            _hitChecked = true;
            bool closed = _hitBubble == null || !_hitBubble.isActiveAndEnabled;
            bool owned = !closed && owners.TryGetValue(_hitBubble, out AbstractCustomer owner) && owner == _hitTarget;
            Check(closed || owned, $"맞아서 대사가 끊긴 {_hitTarget.name}의 말풍선: 접힘 {closed}, 주인이 계속 쥠 {owned}");
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

            // 주인 없는 말풍선이 한 번도 없었으면 그것도 결과다.
            if (_fail == 0)
            {
                Check(true, "주인 없는 말풍선도, 주유 대사를 띄운 채 떠난 손님도 없음");
            }

            Log.AppendLine();
            Log.AppendLine($"끝: {reason}. 통과 {_pass}, 실패 {_fail}, 에러 로그 {Errors.Count}");
            foreach (IGrouping<string, string> g in Errors.GroupBy(e => e))
            {
                Log.AppendLine($"  에러 x{g.Count()}: {g.Key}");
            }

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
