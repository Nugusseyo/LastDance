using System.Collections.Generic;
using System.IO;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Interaction;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>새 정상 손님(Car Talker·Vending Visitor)을 플레이 중 90초 동안 지켜보는 디버그용 도구.
    /// 상태가 바뀔 때마다 위치·애니메이션·가장 가까운 다른 차까지 거리를 적고, 자판기(VandalTarget)가 맞은 양이 늘었는지 본다.
    /// 결과는 Temp/CustomerSim/normal.txt. 확인이 끝나면 지워도 된다.</summary>
    public static class NormalCustomerProbe
    {
        private const float Duration = 90f;
        private static readonly Dictionary<AbstractCustomer, string> LastState = new();
        private static readonly StringBuilder Log = new();
        private static readonly Dictionary<VandalTarget, float> StartTaken = new();
        private static float _start;

        [MenuItem("Tools/CJW/Record New Normal Customers")]
        private static void Begin()
        {
            LastState.Clear();
            Log.Clear();
            StartTaken.Clear();
            // 메뉴가 불린 시점의 Time.time은 게임 시간이 아닐 수 있다. 첫 틱에서 잡는다.
            _start = -1f;
            foreach (VandalTarget v in Object.FindObjectsByType<VandalTarget>(FindObjectsSortMode.None))
            {
                StartTaken[v] = v.Taken;
            }

            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_start < 0f)
            {
                _start = Time.time;
            }

            if (!EditorApplication.isPlaying || Time.time - _start > Duration)
            {
                Finish();
                return;
            }

            foreach (AbstractCustomer c in Object.FindObjectsByType<AbstractCustomer>(FindObjectsSortMode.None))
            {
                if (!c.name.Contains("Talker") && !c.name.Contains("Visitor"))
                {
                    continue;
                }

                string state = c.Fsm?.Machine?.Current?.GetType().Name ?? "-";
                Animator animator = c.GetComponentInChildren<Animator>();
                AnimatorClipInfo[] infos = animator != null ? animator.GetCurrentAnimatorClipInfo(0) : new AnimatorClipInfo[0];
                string clip = infos.Length > 0 && infos[0].clip != null ? infos[0].clip.name : "-";
                string key = state + "/" + clip;
                if (LastState.TryGetValue(c, out string last) && last == key)
                {
                    continue;
                }

                LastState[c] = key;
                Log.AppendLine($"[{Time.time - _start,5:F1}s] {c.name}#{c.GetInstanceID()} → {state}\t애니={clip}\t가장 가까운 다른 차 {NearestOtherCar(c):F1}m\tpos={c.transform.position}");
            }
        }

        private static float NearestOtherCar(AbstractCustomer c)
        {
            GameObject own = c.Session?.Car != null ? c.Session.Car.gameObject : null;
            float best = float.MaxValue;
            foreach (var car in Object.FindObjectsByType<_Works.CJW.Scripts.Cars.Car>(FindObjectsSortMode.None))
            {
                if (car.gameObject == own) continue;
                best = Mathf.Min(best, Vector3.Distance(car.transform.position, c.transform.position));
            }

            return best;
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            foreach (KeyValuePair<VandalTarget, float> pair in StartTaken)
            {
                if (pair.Key != null)
                {
                    Log.AppendLine($"피격 {pair.Key.name}: {pair.Value} → {pair.Key.Taken}");
                }
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/normal.txt", Log.ToString());
        }
    }
}
