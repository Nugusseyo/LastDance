using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers;
using DevLib.EventChannelSystem;
using DevLib.SoundSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>시뮬레이션 동안 사운드 호출을 엿듣는 디버그 도구. 비어 있는 SoundClipSo 필드에 "타입.필드" 이름의 무음 테스트 클립을
    /// 넣어 두고, 사운드 채널로 올라오는 재생·정지 이벤트를 세어 어느 지점에서 소리가 났는지 기록한다.
    /// 에셋(CarDataSO 등)에 넣은 테스트 클립은 판이 끝날 때 다시 비운다.</summary>
    public static class CustomerSimSoundTap
    {
        private const string ChannelPath = "Assets/_Works/CJW/Data/Event Channel/SoundChannel.asset";

        private static EventChannelSO _channel;
        private static readonly Dictionary<string, SoundClipSo> TestClips = new();
        private static readonly HashSet<object> Visited = new(new RefComparer());
        private static readonly HashSet<Object> Scanned = new();
        private static readonly List<(object owner, FieldInfo field)> AssetRestores = new();

        private static readonly Dictionary<string, int> PlayCounts = new();
        private static int _loopStarts;
        private static int _loopStops;
        private static int _loopLeaks;
        private static float _followDistMax;
        private static readonly Dictionary<int, Transform> OpenLoops = new();
        private static readonly Dictionary<int, int> StartsPerChannel = new();

        private static readonly Dictionary<AbstractCustomer, float> VandalStart = new();
        private static readonly Dictionary<AbstractCustomer, int> VandalHits = new();
        private static readonly List<string> VandalLines = new();

        private static double _lastScan;
        private static Func<float> _elapsed;
        private static Action<string> _write;

        public static void Begin(Func<float> elapsed, Action<string> write)
        {
            _elapsed = elapsed;
            _write = write;
            Visited.Clear();
            Scanned.Clear();
            AssetRestores.Clear();
            PlayCounts.Clear();
            OpenLoops.Clear();
            StartsPerChannel.Clear();
            VandalStart.Clear();
            VandalHits.Clear();
            VandalLines.Clear();
            _loopStarts = _loopStops = _loopLeaks = 0;
            _followDistMax = 0f;
            _lastScan = 0;

            _channel = AssetDatabase.LoadAssetAtPath<EventChannelSO>(ChannelPath);
            if (_channel == null)
            {
                _write("SOUND 채널 에셋이 없어 사운드를 엿듣지 못합니다.");
                return;
            }

            _channel.AddListener<PlaySoundEvent>(OnPlay);
            _channel.AddListener<StopSoundEvent>(OnStop);
            Scan();
        }

        public static void Tick()
        {
            // 풀에서 새로 꺼낸 손님·차에도 테스트 클립을 넣는다.
            if (EditorApplication.timeSinceStartup - _lastScan > 1.0)
            {
                _lastScan = EditorApplication.timeSinceStartup;
                Scan();
            }

            // 반복 소리를 튼 주인이 꺼졌는데 정지가 안 왔으면 새는 것이다.
            foreach (var pair in OpenLoops.ToList())
            {
                if (pair.Value == null || !pair.Value.gameObject.activeInHierarchy)
                {
                    _loopLeaks++;
                    _write($"SOUND LEAK 채널 {pair.Key}: 주인이 꺼졌는데 반복 소리가 정지되지 않음");
                    OpenLoops.Remove(pair.Key);
                }
            }

            foreach (AbstractCustomer customer in Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None))
            {
                bool vandal = customer.Fsm?.Machine?.Current?.GetType().Name == "VandalizeState";
                bool tracked = VandalStart.TryGetValue(customer, out float start);
                if (vandal && !tracked)
                {
                    VandalStart[customer] = _elapsed();
                    VandalHits[customer] = 0;
                }
                else if (!vandal && tracked)
                {
                    FinishVandal(customer, start, customer.Session != null ? customer.Session.Phase.ToString() : "방문 밖");
                }
            }
        }

        public static void End(StringBuilder summary)
        {
            foreach (var pair in VandalStart.ToList())
            {
                FinishVandal(pair.Key, pair.Value, "판 종료(진행 중)");
            }

            if (_channel != null)
            {
                _channel.RemoveListener<PlaySoundEvent>(OnPlay);
                _channel.RemoveListener<StopSoundEvent>(OnStop);
            }

            // 에셋에 넣은 테스트 클립을 걷어낸다. 남기면 저장될 때 딸려 들어간다.
            foreach ((object owner, FieldInfo field) in AssetRestores)
            {
                field.SetValue(owner, null);
            }

            summary.AppendLine($"사운드 재생 {PlayCounts.Values.Sum()}건 (종류 {PlayCounts.Count})");
            foreach (var pair in PlayCounts.OrderBy(p => p.Key))
            {
                summary.AppendLine($"  [x{pair.Value}] {pair.Key}");
            }

            string perCar = StartsPerChannel.Count == 0 ? "-" : $"차당 평균 {StartsPerChannel.Values.Average():0.0} 최대 {StartsPerChannel.Values.Max()}";
            summary.AppendLine($"반복 소리 시작 {_loopStarts} / 정지 {_loopStops} / 판 끝 재생 중 {OpenLoops.Count} / 새는 소리 {_loopLeaks} / 채널 {StartsPerChannel.Count}개({perCar}) / 따라가기 대상 거리 최대 {_followDistMax:0.00}m");

            summary.AppendLine($"자판기·차 때리기 {VandalLines.Count}건");
            foreach (string line in VandalLines)
            {
                summary.AppendLine("  " + line);
            }
        }

        private static void FinishVandal(AbstractCustomer customer, float start, string endedBy)
        {
            VandalStart.Remove(customer);
            VandalHits.TryGetValue(customer, out int hits);
            VandalHits.Remove(customer);

            string who = customer != null && customer.Data != null ? customer.Data.name : "?";
            string line = $"{who}: 때리기 상태 {_elapsed() - start:0.0}s, 타격 {hits}회, 끝난 이유 {endedBy}";
            VandalLines.Add(line);
            _write("VANDAL " + line);
        }

        private static void OnPlay(PlaySoundEvent evt)
        {
            string name = evt.ClipData != null ? evt.ClipData.name : "(null)";
            PlayCounts[name] = PlayCounts.TryGetValue(name, out int n) ? n + 1 : 1;

            if (evt.ChannelNumber > 0)
            {
                _loopStarts++;
                StartsPerChannel[evt.ChannelNumber] = StartsPerChannel.TryGetValue(evt.ChannelNumber, out int s) ? s + 1 : 1;
                OpenLoops[evt.ChannelNumber] = evt.Follow;
                if (evt.Follow != null)
                {
                    _followDistMax = Mathf.Max(_followDistMax, Vector3.Distance(evt.Follow.position, evt.Position));
                }

                _write($"SOUND LOOP ▶ {name} ch{evt.ChannelNumber} follow={(evt.Follow != null ? evt.Follow.name : "없음")}");
            }
            else if (name == "VandalizeState.hitSound")
            {
                AbstractCustomer nearest = VandalStart.Keys.Where(c => c != null)
                    .OrderBy(c => (c.transform.position - evt.Position).sqrMagnitude).FirstOrDefault();
                if (nearest != null)
                {
                    VandalHits[nearest] = VandalHits.TryGetValue(nearest, out int h) ? h + 1 : 1;
                }
            }
            else
            {
                _write($"SOUND {name} @{evt.Position.x:F1},{evt.Position.z:F1}");
            }
        }

        private static void OnStop(StopSoundEvent evt)
        {
            if (OpenLoops.Remove(evt.ChannelNumber))
            {
                _loopStops++;
                _write($"SOUND LOOP ■ ch{evt.ChannelNumber}");
            }
        }

        private static void Scan()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 차의 방문 단계는 CarDataSO 에셋에 들어 있다. Setup으로 나중에 들어오므로 매번 본다. 에셋이라 판이 끝나면 되돌린다.
                if (behaviour is Car car && car.Data != null && Scanned.Add(car.Data))
                {
                    Fill(car.Data, true);
                }

                if (behaviour == null || !Scanned.Add(behaviour))
                {
                    continue;
                }

                string ns = behaviour.GetType().Namespace ?? "";
                if (!ns.StartsWith("_Works.CJW"))
                {
                    continue;
                }

                Fill(behaviour, false);
            }
        }

        /// <summary>Unity가 직렬화하는 필드를 따라 내려가며 비어 있는 SoundClipSo에 테스트 클립을 넣는다.</summary>
        private static void Fill(object target, bool isAsset)
        {
            if (target == null || !Visited.Add(target))
            {
                return;
            }

            for (Type type = target.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject) && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    bool serialized = (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)))
                                      && !field.IsDefined(typeof(NonSerializedAttribute));
                    if (!serialized)
                    {
                        continue;
                    }

                    if (field.FieldType == typeof(SoundClipSo))
                    {
                        if (field.GetValue(target) as SoundClipSo == null)
                        {
                            field.SetValue(target, GetTestClip($"{type.Name}.{field.Name}", field.Name == "driveSound"));
                            if (isAsset)
                            {
                                AssetRestores.Add((target, field));
                            }
                        }

                        continue;
                    }

                    if (typeof(Object).IsAssignableFrom(field.FieldType) || field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string))
                    {
                        continue;
                    }

                    object value = field.GetValue(target);
                    if (value is System.Collections.IEnumerable list && value is not string)
                    {
                        foreach (object item in list)
                        {
                            if (item != null && item is not Object && !item.GetType().IsPrimitive)
                            {
                                Fill(item, isAsset);
                            }
                        }
                    }
                    else if (value != null && !field.FieldType.IsValueType)
                    {
                        Fill(value, isAsset);
                    }
                }
            }
        }

        private sealed class RefComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        private static SoundClipSo GetTestClip(string name, bool loop)
        {
            if (TestClips.TryGetValue(name, out SoundClipSo clip) && clip != null)
            {
                return clip;
            }

            clip = ScriptableObject.CreateInstance<SoundClipSo>();
            clip.name = name;
            clip.hideFlags = HideFlags.DontSave;
            clip.clip = AudioClip.Create(name, 4410, 1, 44100, false);
            clip.clip.hideFlags = HideFlags.DontSave;
            clip.loop = loop;
            TestClips[name] = clip;
            return clip;
        }
    }
}
