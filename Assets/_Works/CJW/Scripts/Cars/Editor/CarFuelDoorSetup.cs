using System.Text;
using _Works.JJH._02_Scripts.Objects;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars.Editor
{
    /// <summary>차 프리팹에 주유구(<see cref="FuelDoor"/>)를 붙인다. 차체 왼쪽 뒤편에 트리거 박스를 두고
    /// FuelDoor 레이어로 맞춰 플레이어의 주유 레이캐스트에 걸리게 한다. 이미 있으면 위치만 다시 맞춘다.
    /// 결과는 Temp/car_fuel_door_setup.txt에 남긴다.</summary>
    public static class CarFuelDoorSetup
    {
        private static readonly string[] CarPrefabs =
        {
            "Assets/_Works/Share/Prefabs/Car.prefab",
            "Assets/_Works/Share/Prefabs/TestSUV.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car1.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car2.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car3.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car4.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car5.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car6.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car7.prefab",
            "Assets/_Works/CJW/Prefabs/Cars/Car8.prefab",
        };

        private const string ChildName = "FuelDoor";
        private const string LayerName = "FuelDoor";

        /// <summary>차체 크기를 잴 때 빼는 오브젝트. 바퀴는 차체 밖으로 튀어나와 옆면 위치를 틀리게 만든다.</summary>
        private static readonly string[] IgnoreNameHints = { "Wheel", "Cylinder", "Seat", "Obstacle" };

        /// <summary>주유구 박스 크기(m). 옆면 안팎으로 두께를 둬야 조금 비스듬히 봐도 레이가 걸린다.</summary>
        private static readonly Vector3 DoorSize = new(0.3f, 0.35f, 0.35f);

        /// <summary>차 뒤끝에서 전체 길이의 이 비율만큼 앞쪽. 뒷바퀴 위 펜더 자리다.</summary>
        private const float FromRear = 0.25f;

        /// <summary>차 바닥에서 전체 높이의 이 비율만큼 위.</summary>
        private const float FromBottom = 0.6f;

        [MenuItem("Tools/JW/Car Fuel Door/Setup")]
        public static void Setup()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError($"[CarFuelDoor] '{LayerName}' 레이어가 없습니다. Tags and Layers에 먼저 추가해야 합니다.");
                return;
            }

            var sb = new StringBuilder("[CarFuelDoor] 주유구 설정\n");

            foreach (string path in CarPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                    if (!TryGetBodyBounds(root.transform, out Bounds body))
                    {
                        sb.AppendLine($"{path}: 차체 렌더러가 없어 건너뜀");
                        continue;
                    }

                    Transform door = root.transform.Find(ChildName);
                    if (door == null)
                    {
                        door = new GameObject(ChildName).transform;
                        door.SetParent(root.transform, false);
                    }

                    // 차는 NavMeshAgent로 +Z를 향해 달리므로 로컬 -X가 왼쪽, -Z가 뒤쪽이다.
                    door.localPosition = new Vector3(
                        body.min.x,
                        body.min.y + body.size.y * FromBottom,
                        body.min.z + body.size.z * FromRear);
                    door.localRotation = Quaternion.identity;
                    door.localScale = Vector3.one;
                    door.gameObject.layer = layer;

                    BoxCollider box = door.GetComponent<BoxCollider>();
                    if (box == null)
                    {
                        box = door.gameObject.AddComponent<BoxCollider>();
                    }

                    // 트리거여야 차의 물리·NavMesh 장애물에 끼어들지 않는다. 레이캐스트는 트리거도 맞힌다(queriesHitTriggers).
                    box.isTrigger = true;
                    box.center = Vector3.zero;
                    box.size = DoorSize;

                    if (door.GetComponent<FuelDoor>() == null)
                    {
                        door.gameObject.AddComponent<FuelDoor>();
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    sb.AppendLine($"{path}: 차체 {body.size}, 주유구 {door.localPosition}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "car_fuel_door_setup.txt"), sb.ToString());
            Debug.Log("[CarFuelDoor] 주유구를 붙였습니다. Temp/car_fuel_door_setup.txt를 보세요.");
        }

        /// <summary>바퀴·좌석 같은 부속을 뺀 렌더러를 루트 로컬 공간에서 감싼 상자. 루트가 원점·무회전이라 월드 bounds가 곧 로컬이다.</summary>
        private static bool TryGetBodyBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (IsIgnored(r.name))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return found;
        }

        private static bool IsIgnored(string name)
        {
            foreach (string hint in IgnoreNameHints)
            {
                if (name.Contains(hint))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
