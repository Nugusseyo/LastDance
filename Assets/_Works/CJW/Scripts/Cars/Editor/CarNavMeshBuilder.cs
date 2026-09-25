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
    /// <summary>차 전용 NavMesh를 만든다. 차 에이전트 타입을 준비하고, 사람용 NavMesh와 같은 범위를 굽되
    /// Map/Plane과 Map/Road 아래만 걸을 수 있게 한다. 차 프리팹의 NavMeshAgent도 차 타입으로 바꾼다.</summary>
    public static class CarNavMeshBuilder
    {
        private const string SurfaceObjectName = "CarNavMesh";
        private const string DataFolder = "Assets/_Works/CJW/Scene/JW_RealMapTest";
        private const string DataPath = DataFolder + "/CarNavMesh.asset";

        private static readonly string[] WalkableRoots = { "Map/Plane", "Map/Road" };

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

            surface.BuildNavMesh();
            SaveData(surface);

            int prefabs = RetargetPrefabs(agentId);

            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            EditorSceneManager.SaveScene(surface.gameObject.scene);

            Debug.Log($"[CarNavMesh] 완료. 에이전트 '{CarNavMesh.AgentTypeName}'(id {agentId}), 수정자 {modifiers}개, 차 프리팹 {prefabs}개를 차 타입으로 바꿨습니다.");
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
                sb.AppendLine(ok
                    ? $"  {(distance < 0.5f ? "OK " : "먼 ")} {label} {Round(position)} → {distance:F1}m"
                    : $"  없음 {label} {Round(position)} (3m 안에 차 NavMesh 없음)");
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

            const int minX = -40, maxX = 40, minZ = -45, maxZ = 45;
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
            surface.collectObjects = human.collectObjects;
            surface.center = human.center;
            surface.size = human.size;
            surface.layerMask = human.layerMask;
            surface.useGeometry = human.useGeometry;
            surface.defaultArea = NotWalkableArea;

            EditorUtility.SetDirty(surface);
            return surface;
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
                    Debug.LogError($"[CarNavMesh] '{path}'를 찾지 못했습니다.");
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

        private static void SaveData(NavMeshSurface surface)
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Works/CJW/Scene", "JW_RealMapTest");
            }

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath) != null)
            {
                AssetDatabase.DeleteAsset(DataPath);
            }

            AssetDatabase.CreateAsset(surface.navMeshData, DataPath);
            AssetDatabase.SaveAssets();
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
