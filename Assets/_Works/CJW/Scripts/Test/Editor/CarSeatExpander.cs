using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>Car1~8 프리팹에 뒷좌석을 더해 4인승으로 만드는 일회성 도구. 앞좌석을 앞바퀴와 뒷바퀴 사이 중간쯤 뒤로 복제한다.
    /// 이미 4석 이상이면 건드리지 않는다. 확인이 끝나면 지워도 된다.</summary>
    public static class CarSeatExpander
    {
        private const string Folder = "Assets/_Works/CJW/Prefabs/Cars";
        private const int TargetSeats = 4;

        [MenuItem("Tools/CJW/Expand Car Seats To 4")]
        private static void Expand()
        {
            var report = new StringBuilder();

            for (int n = 1; n <= 8; n++)
            {
                string path = $"{Folder}/Car{n}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    var car = root.GetComponent<_Works.CJW.Scripts.Cars.Car>();
                    var so = new SerializedObject(car);
                    SerializedProperty seats = so.FindProperty("seats");

                    if (seats.arraySize >= TargetSeats)
                    {
                        report.AppendLine($"Car{n}: 이미 {seats.arraySize}석, 건너뜀");
                        continue;
                    }

                    int front = seats.arraySize;
                    Transform rearWheel = FindDeep(root.transform, "Rear_Left_Wheel");
                    float rearZ = rearWheel != null
                        ? root.transform.InverseTransformPoint(rearWheel.position).z
                        : float.NaN;

                    for (int i = 0; i < front && seats.arraySize < TargetSeats; i++)
                    {
                        var seat = seats.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        if (seat == null)
                        {
                            continue;
                        }

                        Vector3 local = root.transform.InverseTransformPoint(seat.position);
                        float z = float.IsNaN(rearZ) ? local.z - 0.8f : Mathf.Lerp(local.z, rearZ, 0.5f);

                        var rear = new GameObject($"Seat Rear {i}").transform;
                        rear.SetParent(seat.parent, false);
                        rear.position = root.transform.TransformPoint(new Vector3(local.x, local.y, z));
                        rear.rotation = seat.rotation;

                        seats.arraySize++;
                        seats.GetArrayElementAtIndex(seats.arraySize - 1).objectReferenceValue = rear;

                        report.AppendLine($"Car{n}: 앞 {i} ({local.x:F2},{local.y:F2},{local.z:F2}) → 뒤 z {z:F2} (뒷바퀴 z {rearZ:F2})");
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/seats.txt", report.ToString());
            Debug.Log("[CarSeatExpander]\n" + report);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
