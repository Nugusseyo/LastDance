using System.IO;
using System.Text;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>손님 프리팹마다 CustomerFSMModule의 Phase별 상태 목록을 적는 디버그용 도구. 결과는 Temp/CustomerSim/sequences.txt.</summary>
    public static class CustomerSequenceDump
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";

        [MenuItem("Tools/CJW/Dump Customer Sequences")]
        private static void Dump()
        {
            var sb = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var fsm = prefab.GetComponentInChildren<CustomerFSMModule>(true);
                sb.AppendLine($"== {prefab.name} (variant={PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant})");
                if (fsm == null)
                {
                    sb.AppendLine("  FSM 없음");
                    continue;
                }

                var so = new SerializedObject(fsm);
                SerializedProperty sequences = so.FindProperty("sequences");
                for (int i = 0; i < sequences.arraySize; i++)
                {
                    SerializedProperty seq = sequences.GetArrayElementAtIndex(i);
                    SerializedProperty phase = seq.FindPropertyRelative("Phase");
                    SerializedProperty states = seq.FindPropertyRelative("States");
                    sb.Append($"  [{i}] {phase.enumNames[phase.enumValueIndex]}:");
                    for (int j = 0; j < states.arraySize; j++)
                    {
                        object state = states.GetArrayElementAtIndex(j).managedReferenceValue;
                        sb.Append($" {(state == null ? "null" : state.GetType().Name)}");
                    }

                    sb.AppendLine();
                }
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/sequences.txt", sb.ToString());
        }

        /// <summary>플레이 중 손님마다 지금 도는 상태와 가장 가까운 켜진 말풍선까지의 거리를 적는다. 결과는 Temp/CustomerSim/runtime.txt.</summary>
        [MenuItem("Tools/CJW/Dump Customer Runtime")]
        private static void DumpRuntime()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"playing={EditorApplication.isPlaying} t={Time.time:F1}");

            var bubbles = Object.FindObjectsByType<_Works.JYG._Scripts.UI.SpeechBubble.SpeechBubble>(FindObjectsSortMode.None);
            foreach (var c in Object.FindObjectsByType<_Works.CJW.Scripts.Customers.AbstractCustomer>(FindObjectsSortMode.None))
            {
                float nearest = float.MaxValue;
                string text = "";
                foreach (var b in bubbles)
                {
                    float d = Vector3.Distance(b.transform.position, c.transform.position);
                    if (d < nearest)
                    {
                        nearest = d;
                        text = b.GetComponentInChildren<TMPro.TMP_Text>(true)?.text;
                    }
                }

                string near = nearest < 3f ? $"말풍선 {nearest:F1}m '{text}'" : "말풍선 없음";
                sb.AppendLine($"{c.name}#{c.GetInstanceID()}\t{c.Fsm?.Machine?.Current?.GetType().Name ?? "-"}\t{near}\tpos={c.transform.position}");
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/runtime.txt", sb.ToString());
        }
    }
}
