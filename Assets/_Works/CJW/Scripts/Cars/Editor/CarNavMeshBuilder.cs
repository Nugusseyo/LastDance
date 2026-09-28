using System.Collections.Generic;
using System.Text;
using _Works.CJW.Scripts.MapSystems;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars.Editor
{
    /// <summary>현재 씬에 차 전용 NavMesh를 만든다. 차 에이전트 타입을 준비하고, 사람용 NavMesh와 같은 범위를 굽되
    /// 바닥(Map/Plane 또는 Map/Terrain)과 Map/Road 아래만 걸을 수 있게 한다. 차 프리팹의 NavMeshAgent도 차 타입으로 바꾼다.</summary>
    public static class CarNavMeshBuilder
    {
        private const string SurfaceObjectName = "CarNavMesh";
        private const string DataFileName = "NavMesh-CarNavMesh.asset";

        // 씬마다 바닥 이름이 다르다(JW_RealMapTest는 Plane, Demo는 Terrain). 있는 것만 쓴다.
        private static readonly string[] WalkableRoots = { "Map/Plane", "Map/Terrain", "Map/Road" };

        private static readonly string[] CarPrefabs =
        {
            "Assets/_Works/Share/Prefabs/Car.prefab",
            "Assets/_Works/Share/Prefabs/TestSUV.prefab",
        };

        // 차 반폭(Car 0.8, TestSUV 약 1.06)보다 조금 크게 잡아, 경로가 장애물에 차체가 닿을 만큼 붙지 않게 한다.
        private const float AgentRadius = 1.0f;
        private const float AgentHeight = 1.6f;
        private const float AgentSlope = 30f;
        private const float AgentClimb = 0.3f;

        // JW_RealMapTest에서 맞춰 둔 값(바닥 4.44, 볼륨 중심 5.02, 높이 1.51)을 바닥 기준으로 옮긴 것.
        private const float VolumeCenterAboveGround = 0.58f;
        private const float VolumeHeight = 1.51f;
        private const float VolumeMargin = 2f;

        private const int WalkableArea = 0;
        private const int NotWalkableArea = 1;

        [MenuItem("Tools/JW/Car NavMesh/Build")]
        public static void Build()
        {
            int agentId = EnsureAgentType();

            NavMeshSurface humanSurface = FindSurface(0);
            if (humanSurface == null)
            {
                Debug.LogError("[CarNavMesh] 사람용 NavMeshSurface(에이전트 타입 0)를 찾지 못했습니다. 범위와 레이어를 그걸 기준으로 잡습니다.");
                return;
            }

            NavMeshSurface surface = EnsureSurface(humanSurface, agentId);
            int modifiers = EnsureModifiers(agentId);
            if (modifiers == 0)
            {
                Debug.LogError($"[CarNavMesh] 걸을 수 있는 바닥({string.Join(", ", WalkableRoots)})을 하나도 찾지 못했습니다.");
                return;
            }

            string oldDataPath = surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : null;
            surface.BuildNavMesh();
            string dataPath = SaveData(surface, oldDataPath);

            int prefabs = RetargetPrefabs(agentId);

            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            EditorSceneManager.SaveScene(surface.gameObject.scene);

            Debug.Log($"[CarNavMesh] 완료({surface.gameObject.scene.name}). 에이전트 '{CarNavMesh.AgentTypeName}'(id {agentId}), 수정자 {modifiers}개, 데이터 {dataPath}, 차 프리팹 {prefabs}개를 차 타입으로 바꿨습니다.");
            Report();
        }

        [MenuItem("Tools/JW/Car NavMesh/Report")]
        public static void Report()
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = CarNavMesh.FindAgentTypeId(CarNavMesh.AgentTypeName),
                areaMask = NavMesh.AllAreas,
            };

            var sb = new StringBuilder("[CarNavMesh] 지점별로 차 NavMesh에 닿는지(가장 가까운 점까지 거리):\n");
            foreach (NavMeshSurface s in NavMeshSurface.activeSurfaces)
            {
                sb.AppendLine($"  표면 {s.name} 타입 {NavMesh.GetSettingsNameFromID(s.agentTypeID)} 수집 {s.collectObjects} 볼륨중심 {s.transform.TransformPoint(s.center)} 크기 {s.size} " +
                              $"데이터 {(s.navMeshData != null ? $"{AssetDatabase.GetAssetPath(s.navMeshData)} 범위 {s.navMeshData.sourceBounds}" : "없음")}");
            }

            GameObject map = GameObject.Find("Map");
            if (map != null)
            {
                foreach (Transform child in map.transform)
                {
                    Collider[] colliders = child.GetComponentsInChildren<Collider>();
                    if (colliders.Length == 0 || child.GetComponent<Terrain>() != null)
                    {
                        continue;
                    }

                    Bounds b = colliders[0].bounds;
                    foreach (Collider c in colliders)
                    {
                        b.Encapsulate(c.bounds);
                    }

                    sb.AppendLine($"  맵 {child.name}(layer {LayerMask.LayerToName(child.gameObject.layer)}) 콜라이더 {colliders.Length}개 x {b.min.x:F1}..{b.max.x:F1} y {b.min.y:F1}..{b.max.y:F1} z {b.min.z:F1}..{b.max.z:F1}");
                }
            }

            var points = new List<(string, Vector3)>();

            foreach (MapPosition p in Object.FindObjectsByType<MapPosition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                points.Add(($"{p.Type} {p.name}", p.Position));

                if (p.Type == MapPointType.ParkingSlot)
                {
                    points.Add(($"  진입점 {p.name}", p.Position - p.Rotation * Vector3.forward * 9f));
                }
            }

            GameObject director = GameObject.Find("VisitDirector");
            if (director != null)
            {
                foreach (Transform child in director.transform)
                {
                    points.Add(($"VisitDirector/{child.name}", child.position));
                }
            }

            foreach ((string label, Vector3 position) in points)
            {
                bool ok = NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, filter);
                float distance = ok ? Vector2.Distance(new Vector2(position.x, position.z), new Vector2(hit.position.x, hit.position.z)) : -1f;
                if (ok)
                {
                    sb.AppendLine($"  {(distance < 0.5f ? "OK " : "먼 ")} {label} {Round(position)} → {distance:F1}m");
                    continue;
                }

                // 3m 안에 없으면 높이가 어긋났는지 볼 수 있게, 넓게 찾아 NavMesh 높이와 지점 높이를 같이 적는다.
                sb.AppendLine(NavMesh.SamplePosition(position, out NavMeshHit far, 30f, filter)
                    ? $"  없음 {label} {Round(position)} y {position.y:F2} (가장 가까운 차 NavMesh {Round(far.position)} y {far.position.y:F2})"
                    : $"  없음 {label} {Round(position)} y {position.y:F2} (30m 안에도 차 NavMesh 없음)");
            }

            Debug.Log(sb.ToString());

            // 콘솔은 여러 줄 로그를 접어 보여 준다. 전체를 파일로도 남긴다.
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "car_navmesh_report.txt"), sb.ToString());
        }

        /// <summary>차 NavMesh가 덮는 곳을 1m 격자 글자 지도로 Temp/car_navmesh_map.txt에 남긴다. 위가 +z(북), 오른쪽이 +x(동).
        /// '#' 차 NavMesh, '.' 없음, 'S' 주차 자리, 'E' 입구, 'P' 순회 지점, 'O' 스폰·퇴장 지점.</summary>
        [MenuItem("Tools/JW/Car NavMesh/Map")]
        public static void Map()
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = CarNavMesh.FindAgentTypeId(CarNavMesh.AgentTypeName),
                areaMask = NavMesh.AllAreas,
            };

            int minX = -40, maxX = 40, minZ = -45, maxZ = 45;
            NavMeshSurface surface = FindSurface(filter.agentTypeID);
            if (surface != null && surface.collectObjects == CollectObjects.Volume)
            {
                Vector3 c = surface.transform.TransformPoint(surface.center);
                Vector3 half = Vector3.Scale(surface.size, surface.transform.lossyScale) * 0.5f;
                minX = Mathf.FloorToInt(c.x - half.x);
                maxX = Mathf.CeilToInt(c.x + half.x);
                minZ = Mathf.FloorToInt(c.z - half.z);
                maxZ = Mathf.CeilToInt(c.z + half.z);
            }

            var marks = new Dictionary<(int, int), char>();

            foreach (MapPosition p in Object.FindObjectsByType<MapPosition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                char c = p.Type switch
                {
                    MapPointType.ParkingSlot => 'S',
                    MapPointType.Entrance => 'E',
                    MapPointType.Patrol => 'P',
                    _ => '\0',
                };

                if (c != '\0')
                {
                    marks[(Mathf.RoundToInt(p.Position.x), Mathf.RoundToInt(p.Position.z))] = c;
                }
            }

            GameObject director = GameObject.Find("VisitDirector");
            if (director != null)
            {
                foreach (Transform child in director.transform)
                {
                    marks[(Mathf.RoundToInt(child.position.x), Mathf.RoundToInt(child.position.z))] = 'O';
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"x {minX}..{maxX} (왼→오), z {maxZ}..{minZ} (위→아래). 줄 앞 숫자는 z.");

            for (int z = maxZ; z >= minZ; z--)
            {
                sb.Append(z.ToString().PadLeft(4)).Append(' ');
                for (int x = minX; x <= maxX; x++)
                {
                    if (marks.TryGetValue((x, z), out char mark))
                    {
                        sb.Append(mark);
                        continue;
                    }

                    // 높이는 모르므로 위아래로 넉넉히 찾되, 수평으로 반 칸 안에 있을 때만 덮인 것으로 본다.
                    var probe = new Vector3(x, 0f, z);
                    bool covered = NavMesh.SamplePosition(probe, out NavMeshHit hit, 6f, filter) &&
                                   Mathf.Abs(hit.position.x - x) <= 0.5f && Mathf.Abs(hit.position.z - z) <= 0.5f;
                    sb.Append(covered ? '#' : '.');
                }

                sb.AppendLine();
            }

            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "car_navmesh_map.txt"), sb.ToString());
            Debug.Log("[CarNavMesh] 지도를 Temp/car_navmesh_map.txt에 썼습니다.");
        }

        /// <summary>차가 바닥에 파묻히는 원인을 가린다. 지점마다 차 NavMesh 높이와 실제 바닥 콜라이더 높이의 차이,
        /// 그리고 차 프리팹마다 루트(피벗)에서 모델 바닥까지의 높이를 Temp/car_height_report.txt에 쓴다.</summary>
        [MenuItem("Tools/JW/Car NavMesh/Diagnose Height")]
        public static void DiagnoseHeight()
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = CarNavMesh.FindAgentTypeId(CarNavMesh.AgentTypeName),
                areaMask = NavMesh.AllAreas,
            };

            var sb = new StringBuilder("[CarHeight] 지점: 차 NavMesh y / 바닥 콜라이더 y / 차이(NavMesh - 바닥)\n");

            for (int z = -30; z <= 25; z += 5)
            {
                for (int x = -5; x <= 12; x += 4)
                {
                    var probe = new Vector3(x, 10f, z);
                    bool nav = NavMesh.SamplePosition(new Vector3(x, 4.44f, z), out NavMeshHit hit, 3f, filter);
                    bool ground = Physics.Raycast(probe, Vector3.down, out RaycastHit rh, 20f, 1 << 7, QueryTriggerInteraction.Ignore);
                    if (nav && ground)
                    {
                        sb.AppendLine($"  ({x},{z}) nav {hit.position.y:F3} / ground {rh.point.y:F3} ({rh.collider.name}) / {hit.position.y - rh.point.y:+0.000;-0.000}");
                    }
                }
            }

            sb.AppendLine("[CarHeight] 맵 지점: 지점 y / 위에서 쏜 레이가 처음 닿은 콜라이더 / 사람 NavMesh y / 차 NavMesh y");
            var human = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            foreach (MapPosition p in Object.FindObjectsByType<MapPosition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Vector3 pos = p.Position;
                string ground = Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit rh, 100f, ~0, QueryTriggerInteraction.Ignore)
                    ? $"{rh.collider.name}(layer {LayerMask.LayerToName(rh.collider.gameObject.layer)}) y {rh.point.y:F2}"
                    : "없음";
                string humanY = NavMesh.SamplePosition(pos, out NavMeshHit hh, 30f, human) ? hh.position.y.ToString("F2") : "없음";
                string carY = NavMesh.SamplePosition(pos, out NavMeshHit ch, 30f, filter) ? ch.position.y.ToString("F2") : "없음";
                sb.AppendLine($"  {p.Type} {p.name} {Round(pos)} y {pos.y:F2} / {ground} / {humanY} / {carY}");
            }

            sb.AppendLine("[CarHeight] 프리팹: 루트 y=0 기준 렌더러 바닥 높이(음수면 루트보다 아래로 파묻힘)");
            foreach (string path in CarPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.transform.position = Vector3.zero;
                    float min = float.MaxValue;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                    {
                        min = Mathf.Min(min, r.bounds.min.y);
                        sb.AppendLine($"    {r.name}: 바닥 {r.bounds.min.y:F3}, 위 {r.bounds.max.y:F3}");
                    }

                    sb.AppendLine($"  {path}: 가장 낮은 바닥 {min:F3}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "car_height_report.txt"), sb.ToString());
            Debug.Log("[CarHeight] Temp/car_height_report.txt에 썼습니다.");
        }

        private static string Round(Vector3 v) => $"({v.x:F1}, {v.z:F1})";

        /// <summary>차 에이전트 타입이 없으면 만들고, 있으면 수치만 맞춘다.</summary>
        private static int EnsureAgentType()
        {
            int id = CarNavMesh.FindAgentTypeId(CarNavMesh.AgentTypeName);
            bool exists = id != 0 && NavMesh.GetSettingsNameFromID(id) == CarNavMesh.AgentTypeName;

            if (!exists)
            {
                id = NavMesh.CreateSettings().agentTypeID;
            }

            Object settingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];
            var so = new SerializedObject(settingsAsset);
            SerializedProperty settings = so.FindProperty("m_Settings");
            SerializedProperty names = so.FindProperty("m_SettingNames");

            int index = -1;
            for (int i = 0; i < settings.arraySize; i++)
            {
                if (settings.GetArrayElementAtIndex(i).FindPropertyRelative("agentTypeID").intValue == id)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Debug.LogError("[CarNavMesh] 새 에이전트 타입이 프로젝트 설정에 저장되지 않았습니다. Navigation 창의 Agents 탭에서 'Car'를 직접 만든 뒤 다시 실행하세요.");
                return id;
            }

            SerializedProperty entry = settings.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
            entry.FindPropertyRelative("agentHeight").floatValue = AgentHeight;
            entry.FindPropertyRelative("agentSlope").floatValue = AgentSlope;
            entry.FindPropertyRelative("agentClimb").floatValue = AgentClimb;

            while (names.arraySize <= index)
            {
                names.InsertArrayElementAtIndex(names.arraySize);
            }

            names.GetArrayElementAtIndex(index).stringValue = CarNavMesh.AgentTypeName;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            return id;
        }

        private static NavMeshSurface FindSurface(int agentTypeId)
        {
            foreach (NavMeshSurface s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (s.agentTypeID == agentTypeId)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>사람용과 같은 범위·레이어·기하를 쓰되, 기본 영역을 Not Walkable로 둔다. 걸을 수 있는 건 수정자가 붙은 곳뿐이다.</summary>
        private static NavMeshSurface EnsureSurface(NavMeshSurface human, int agentId)
        {
            NavMeshSurface surface = FindSurface(agentId);
            if (surface == null)
            {
                var go = new GameObject(SurfaceObjectName);
                go.transform.SetPositionAndRotation(human.transform.position, human.transform.rotation);
                go.transform.localScale = human.transform.lossyScale;
                surface = go.AddComponent<NavMeshSurface>();
            }

            surface.agentTypeID = agentId;
            // 사람용이 All이어도 차는 볼륨 안만 굽는다. 지형 전체를 수집하면 맵 밖 벌판까지 차 길이 된다.
            surface.collectObjects = CollectObjects.Volume;
            surface.layerMask = human.layerMask;
            surface.useGeometry = human.useGeometry;

            // 씬마다 맵 위치와 바닥 높이가 다르므로(JW_RealMapTest 바닥 4.44, Demo 0) 볼륨을 맵에서 잰다.
            // 수평은 맵의 도로·장애물을 모두 덮고, 수직은 바닥을 가운데쯤 끼고 얇게 잡는다. 바닥이 볼륨 밖이면 장애물이 깎이지 않는다.
            if (TryMeasureMap(surface.layerMask, out Bounds area, out float groundY))
            {
                surface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                surface.transform.localScale = Vector3.one;
                surface.center = new Vector3(area.center.x, groundY + VolumeCenterAboveGround, area.center.z);
                surface.size = new Vector3(area.size.x + VolumeMargin * 2f, VolumeHeight, area.size.z + VolumeMargin * 2f);
            }
            else
            {
                Debug.LogWarning("[CarNavMesh] Map 아래 도로·장애물 콜라이더를 찾지 못해 사람용 볼륨을 그대로 씁니다.");
                surface.center = human.center;
                surface.size = human.size;
            }
            surface.defaultArea = NotWalkableArea;

            EditorUtility.SetDirty(surface);
            return surface;
        }

        /// <summary>Map 아래에서 굽는 레이어에 든 콜라이더(지형 제외)를 모두 덮는 범위와, 도로(Plane·Road) 윗면 높이를 잰다.
        /// 지형은 맵보다 훨씬 넓어(Demo 1000m) 범위에서 뺀다.</summary>
        private static bool TryMeasureMap(LayerMask layerMask, out Bounds area, out float groundY)
        {
            area = default;
            groundY = 0f;

            GameObject map = GameObject.Find("Map");
            if (map == null)
            {
                return false;
            }

            bool hasArea = false;
            foreach (Collider c in map.GetComponentsInChildren<Collider>())
            {
                if (c is TerrainCollider || c.isTrigger || (layerMask.value & (1 << c.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (hasArea)
                {
                    area.Encapsulate(c.bounds);
                }
                else
                {
                    area = c.bounds;
                    hasArea = true;
                }
            }

            bool hasGround = false;
            foreach (string path in WalkableRoots)
            {
                GameObject root = GameObject.Find(path);
                if (root == null || root.GetComponent<Terrain>() != null)
                {
                    continue;
                }

                foreach (Collider c in root.GetComponentsInChildren<Collider>())
                {
                    groundY = hasGround ? Mathf.Max(groundY, c.bounds.max.y) : c.bounds.max.y;
                    hasGround = true;
                }
            }

            return hasArea && hasGround;
        }

        /// <summary>Plane과 Road 아래에만 Walkable 수정자를 단다. 차 타입에만 적용해 사람용 NavMesh는 건드리지 않는다.</summary>
        private static int EnsureModifiers(int agentId)
        {
            int count = 0;

            foreach (string path in WalkableRoots)
            {
                GameObject root = GameObject.Find(path);
                if (root == null)
                {
                    continue;
                }

                NavMeshModifier modifier = root.GetComponent<NavMeshModifier>();
                if (modifier == null)
                {
                    modifier = root.AddComponent<NavMeshModifier>();
                }

                modifier.overrideArea = true;
                modifier.area = WalkableArea;
                modifier.applyToChildren = true;

                var so = new SerializedObject(modifier);
                SerializedProperty agents = so.FindProperty("m_AffectedAgents");
                agents.ClearArray();
                agents.InsertArrayElementAtIndex(0);
                agents.GetArrayElementAtIndex(0).intValue = agentId;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(modifier);
                count++;
            }

            return count;
        }

        /// <summary>구운 데이터를 씬 옆 폴더(씬 이름)에 저장한다. 예전 데이터는 이 씬 폴더 안에 있을 때만 지운다.
        /// 씬을 복사해 오면 다른 씬의 데이터를 가리키고 있을 수 있는데, 그건 원래 씬이 쓰므로 남긴다.</summary>
        private static string SaveData(NavMeshSurface surface, string oldDataPath)
        {
            string scenePath = surface.gameObject.scene.path;
            string sceneDir = System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            string folder = $"{sceneDir}/{sceneName}";
            string dataPath = $"{folder}/{DataFileName}";

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(sceneDir, sceneName);
            }

            if (!string.IsNullOrEmpty(oldDataPath) && oldDataPath != dataPath && oldDataPath.StartsWith(folder + "/"))
            {
                AssetDatabase.DeleteAsset(oldDataPath);
            }

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(dataPath) != null)
            {
                AssetDatabase.DeleteAsset(dataPath);
            }

            AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
            AssetDatabase.SaveAssets();
            return dataPath;
        }

        private static int RetargetPrefabs(int agentId)
        {
            int count = 0;

            foreach (string path in CarPrefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    NavMeshAgent agent = root.GetComponentInChildren<NavMeshAgent>(true);
                    if (agent == null)
                    {
                        continue;
                    }

                    agent.agentTypeID = agentId;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return count;
        }
    }
}
