using System.IO;
using System.Linq;
using System.Text;
using _Works.CJW.Scripts.MapSystems;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>진단용. 열린 씬의 맵 지점(주차 자리·주유기 등)의 종류·위치·정면을 Temp/CustomerSim/map_points.txt에 남긴다.</summary>
    public static class MapPointDump
    {
        [MenuItem("Tools/CJW/Dump Map Points")]
        private static void Dump()
        {
            var sb = new StringBuilder();
            foreach (MapPosition p in Object.FindObjectsByType<MapPosition>(FindObjectsSortMode.None)
                         .OrderBy(p => p.Type).ThenBy(p => p.transform.position.x).ThenBy(p => p.transform.position.z))
            {
                Vector3 pos = p.transform.position;
                Vector3 fwd = p.transform.forward;
                sb.AppendLine($"{p.Type}\t{p.name}\tpos=({pos.x:F1},{pos.z:F1})\tfwd=({fwd.x:F2},{fwd.z:F2})\trentable={p is RentableMapPosition}");
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/map_points.txt", sb.ToString());
        }
    }
}
