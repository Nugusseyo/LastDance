using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars.Editor
{
    /// <summary>차 프리팹에 CarWheelModule을 붙이고 바퀴를 채운다. 바퀴마다 차축·위쪽에 해당하는 로컬 축을 찾아 넣고,
    /// 차 앞쪽 절반에 있는 바퀴만 조향 바퀴로 둔다. 결과는 Temp/car_wheel_setup.txt에 남긴다.</summary>
    public static class CarWheelSetup
    {
        private static readonly string[] CarPrefabs =
        {
            "Assets/_Works/Share/Prefabs/Car.prefab",
            "Assets/_Works/Share/Prefabs/TestSUV.prefab",
        };

        /// <summary>이름에 이 말이 들어간 오브젝트를 바퀴로 본다. 큐브 차는 원통(Cylinder) 두 개가 앞뒤 차축이다.</summary>
        private static readonly string[] WheelNameHints = { "Wheel", "Cylinder" };

        private static readonly Vector3[] LocalAxes =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        [MenuItem("Tools/JW/Car Wheels/Setup")]
        public static void Setup()
        {
            var sb = new StringBuilder("[CarWheel] 바퀴 설정\n");

            foreach (string path in CarPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                    CarWheelModule module = root.GetComponent<CarWheelModule>();
                    if (module == null)
                    {
                        module = root.AddComponent<CarWheelModule>();
                    }

                    List<Transform> found = FindWheels(root.transform);
                    float centerZ = 0f;
                    foreach (Transform t in found)
                    {
                        centerZ += WheelCenter(t).z;
                    }

                    centerZ = found.Count > 0 ? centerZ / found.Count : 0f;

                    var so = new SerializedObject(module);
                    SerializedProperty array = so.FindProperty("wheels");
                    array.ClearArray();

                    sb.AppendLine($"{path}");
                    for (int i = 0; i < found.Count; i++)
                    {
                        Transform t = found[i];
                        Vector3 spin = BestLocalAxis(t, root.transform.right);
                        Vector3 up = BestLocalAxis(t, root.transform.up);
                        Vector3 center = WheelCenter(t);
                        bool front = center.z > centerZ;
                        float pivotOffset = Vector3.Distance(center, t.position);

                        array.InsertArrayElementAtIndex(i);
                        SerializedProperty e = array.GetArrayElementAtIndex(i);
                        e.FindPropertyRelative("transform").objectReferenceValue = t;
                        e.FindPropertyRelative("spinAxis").vector3Value = spin;
                        e.FindPropertyRelative("steerAxis").vector3Value = up;
                        e.FindPropertyRelative("steer").boolValue = front;
                        e.FindPropertyRelative("radius").floatValue = 0f;

                        sb.AppendLine($"  {t.name}: 굴림축 {spin}, 위축 {up}, {(front ? "앞(조향)" : "뒤")}, " +
                                      $"피벗과 모양 중심 거리 {pivotOffset:F3}m{(pivotOffset > 0.05f ? "  ← 피벗이 중심이 아니라 돌리면 흔들립니다" : "")}");
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "car_wheel_setup.txt"), sb.ToString());
            Debug.Log("[CarWheel] 바퀴 모듈을 붙였습니다. Temp/car_wheel_setup.txt를 보세요.");
        }

        private static List<Transform> FindWheels(Transform root)
        {
            var result = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || t.GetComponent<Renderer>() == null)
                {
                    continue;
                }

                foreach (string hint in WheelNameHints)
                {
                    if (t.name.Contains(hint))
                    {
                        result.Add(t);
                        break;
                    }
                }
            }

            return result;
        }

        private static Vector3 WheelCenter(Transform t)
        {
            Renderer r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }

        /// <summary>바퀴 로컬 축 중 월드 방향이 target과 가장 나란한 것. 부호까지 맞춘다.</summary>
        private static Vector3 BestLocalAxis(Transform t, Vector3 target)
        {
            Vector3 best = Vector3.right;
            float bestDot = float.NegativeInfinity;

            foreach (Vector3 axis in LocalAxes)
            {
                float dot = Vector3.Dot(t.TransformDirection(axis).normalized, target);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = axis;
                }
            }

            return best;
        }
    }
}
