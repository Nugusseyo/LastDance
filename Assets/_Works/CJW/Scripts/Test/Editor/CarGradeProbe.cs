using System.Text;
using _Works.CJW.Scripts.MapSystems;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>평판 구간마다 등급을 여러 번 뽑아 실제 비율이 가중치 ÷ 해금 합과 맞는지 본다. 결과는 한 줄로 찍는다.</summary>
    public static class CarGradeProbe
    {
        private const string TablePath = "Assets/_Works/CJW/Data/Cars/Car Grade Table.asset";
        private const int Rolls = 20000;

        [MenuItem("Tools/JW/Test/Probe Car Grade Pick")]
        public static void Probe()
        {
            var table = AssetDatabase.LoadAssetAtPath<CarGradeTableSO>(TablePath);
            var sb = new StringBuilder("[CarGradeProbe]");

            foreach (int reputation in new[] { 0, 34, 35, 75, 120 })
            {
                var counts = new int[5];
                for (int i = 0; i < Rolls; i++)
                {
                    if (table.TryPickGrade(reputation, _ => true, out CarGrade grade))
                    {
                        counts[(int)grade]++;
                    }
                }

                sb.Append($" | rep{reputation}:");
                for (int g = 1; g < counts.Length; g++)
                {
                    sb.Append($" {(CarGrade)g}={counts[g] * 100f / Rolls:F1}%");
                }
            }

            // 뽑을 차가 없는 등급은 빠지고 나머지끼리 나눈다(평판 120, Super 후보 없음).
            var noSuper = new int[5];
            for (int i = 0; i < Rolls; i++)
            {
                if (table.TryPickGrade(120, g => g != CarGrade.Super, out CarGrade grade))
                {
                    noSuper[(int)grade]++;
                }
            }
            sb.Append($" | rep120 noSuper: Low={noSuper[1] * 100f / Rolls:F1}% Super={noSuper[4]}");

            Debug.Log(sb.ToString());
        }

        /// <summary>열린 씬의 VisitDirector로 randomVisual 차(Circler·Entrance Blocker)의 겉모습을 여러 번 뽑아 어떤 프리팹이 나오는지 한 줄로 찍는다.
        /// 평판을 잠깐 바꿨다가 되돌린다.</summary>
        [MenuItem("Tools/JW/Test/Probe Random Car Visual")]
        public static void ProbeVisual()
        {
            var director = Object.FindAnyObjectByType<Customers.Visit.VisitDirector>();
            var so = new SerializedObject(director);
            var reputation = so.FindProperty("reputation").objectReferenceValue as JYG._Scripts.Data_Container.Money.IntegerDataContainer;
            var method = typeof(Customers.Visit.VisitDirector).GetMethod("PickVisual",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            var sb = new StringBuilder("[CarVisualProbe]");
            int saved = reputation.Value;
            SerializedProperty list = so.FindProperty("carDataList");

            foreach (int rep in new[] { 0, 120 })
            {
                reputation.Value = rep;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var data = list.GetArrayElementAtIndex(i).objectReferenceValue as CarDataSO;
                    if (data == null || !data.RandomVisual)
                    {
                        continue;
                    }

                    var counts = new System.Collections.Generic.SortedDictionary<string, int>();
                    for (int n = 0; n < 2000; n++)
                    {
                        var item = (DevLib.ObjectPool.Runtime.PoolItemSO)method.Invoke(director, new object[] { data });
                        string key = item != null && item.prefab != null ? item.prefab.name : "null";
                        counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                    }

                    sb.Append($" | rep{rep} {data.name}:");
                    foreach (var pair in counts)
                    {
                        sb.Append($" {pair.Key}={pair.Value * 100f / 2000:F0}%");
                    }
                }
            }

            reputation.Value = saved;
            Debug.Log(sb.ToString());
        }
    }
}
