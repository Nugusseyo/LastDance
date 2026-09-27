using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.MapSystems;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>플레이 중 VisitDirector의 좌석 추첨(PickForSeat)을 여러 번 불러 주유 손님 비율과 종류별 비율을 잰다.
    /// 추첨이 보는 상태(_fuelTaken·_takenRoles·_spawnBuffer)는 부르기 전에 비우고 끝나면 되돌린다. 결과는 Temp/CustomerSim/spawn_ratio.txt.</summary>
    public static class SpawnRatioProbe
    {
        private const int Samples = 10000;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/CJW/Sample Seat Picks")]
        private static void Sample()
        {
            var director = Object.FindFirstObjectByType<VisitDirector>();
            if (director == null || !EditorApplication.isPlaying)
            {
                Debug.LogError("[SpawnRatioProbe] 플레이 중인 VisitDirector가 필요합니다.");
                return;
            }

            System.Type t = typeof(VisitDirector);
            MethodInfo pick = t.GetMethod("PickForSeat", Flags);
            FieldInfo fuelTaken = t.GetField("_fuelTaken", Flags);
            var roles = (HashSet<CustomerType>)t.GetField("_takenRoles", Flags).GetValue(director);
            var buffer = (List<AbstractCustomer>)t.GetField("_spawnBuffer", Flags).GetValue(director);
            var defaults = (CustomerDataSO[])t.GetField("defaultCustomerDataList", Flags).GetValue(director);
            var carList = (CarDataSO[])t.GetField("carDataList", Flags).GetValue(director);

            bool savedFuel = (bool)fuelTaken.GetValue(director);
            var savedRoles = roles.ToList();
            var savedBuffer = buffer.ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"fuelCustomerChance={t.GetField("fuelCustomerChance", Flags).GetValue(director)}");

            try
            {
                var pools = new List<(string, CustomerDataSO[])> { ("기본 목록(레이싱 차)", defaults) };
                pools.AddRange(carList.Where(c => c != null && c.Customers != null).Select(c => (c.name, c.Customers)));

                foreach (var (name, pool) in pools)
                {
                    foreach (bool afterFuel in new[] { false, true })
                    {
                        var counts = new Dictionary<string, int>();
                        int fuel = 0, none = 0;
                        for (int i = 0; i < Samples; i++)
                        {
                            roles.Clear();
                            buffer.Clear();
                            fuelTaken.SetValue(director, afterFuel);
                            var data = (CustomerDataSO)pick.Invoke(director, new object[] { pool });
                            if (data == null)
                            {
                                none++;
                                continue;
                            }

                            counts[data.name] = counts.TryGetValue(data.name, out int n) ? n + 1 : 1;
                            if (CustomerRoles.WantsFuel(data)) fuel++;
                        }

                        sb.AppendLine($"== {name} / {(afterFuel ? "주유 손님이 이미 탄 뒤" : "첫 좌석")}: 주유 {100f * fuel / Samples:F1}%, 없음 {none}");
                        foreach (var kv in counts.OrderByDescending(k => k.Value))
                        {
                            CustomerDataSO d = pool.First(p => p != null && p.name == kv.Key);
                            sb.AppendLine($"   {kv.Key,-28} {100f * kv.Value / Samples,5:F1}%  {(CustomerRoles.WantsFuel(d) ? "주유" : "")}");
                        }
                    }
                }
            }
            finally
            {
                fuelTaken.SetValue(director, savedFuel);
                roles.Clear();
                foreach (CustomerType r in savedRoles) roles.Add(r);
                buffer.Clear();
                buffer.AddRange(savedBuffer);
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/spawn_ratio.txt", sb.ToString());
        }
    }
}
