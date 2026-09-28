using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Works.CJW.Scripts.Title.Editor
{
    /// <summary>Title_JW 씬에 JW_RealMapTest의 맵을 복사해 넣고, 도로를 따라 차가 지나다니는 타이틀 배경을 만든다.
    /// 다시 돌리면 전에 넣은 맵·차·카메라 설정을 지우고 새로 만든다. 결과 요약은 Temp/title_build.txt.</summary>
    public static class TitleSceneBuilder
    {
        private const string TitleScenePath = "Assets/_Works/CJW/Scene/Title_JW.unity";
        private const string SourceScenePath = "Assets/_Works/CJW/Scene/JW_RealMapTest.unity";
        private const string CarPrefabFolder = "Assets/_Works/CJW/Prefabs/Cars";
        private const string MapName = "Map";

        /// <summary>맵 말고도 배경으로 같이 가져올 루트들(주유소 건물·자판기·주유기·세워 둔 차). 게임 스크립트는 빼고 겉모습만 남긴다.</summary>
        private static readonly string[] ExtraRoots = { "Warehouse", "Reflection", "Vending_machine", "Sell Desk", "Oil Dispenser", "SUV", "Stealable Cars" };
        private const string RoadName = "Road";
        private const string RoadFenceName = "Road fence";
        private const string TrafficName = "Title Traffic";
        private const string LookTargetName = "Title Camera Target";

        [MenuItem("Tools/JW/Title/Build Title Scene")]
        private static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var log = new StringBuilder();
            Scene title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            RemoveRoot(title, MapName);
            foreach (string name in ExtraRoots)
            {
                RemoveRoot(title, name);
            }

            RemoveRoot(title, TrafficName);
            RemoveRoot(title, LookTargetName);

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject srcMap = FindRoot(source, MapName);
                if (srcMap == null)
                {
                    Debug.LogError($"[TitleBuilder] {SourceScenePath}에 루트 '{MapName}'이 없습니다.");
                    return;
                }

                log.AppendLine("source roots: " + string.Join(", ", RootNames(source)));
                CopyLighting(source, title, log);

                GameObject map = DuplicateInto(srcMap, title);
                int stripped = StripGameScripts(map);
                log.AppendLine($"map copied, stripped {stripped} game scripts");
                foreach (string name in ExtraRoots)
                {
                    GameObject src = FindRoot(source, name);
                    if (src == null)
                    {
                        log.AppendLine($"extra root '{name}' not found");
                        continue;
                    }

                    GameObject copy = DuplicateInto(src, title);
                    log.AppendLine($"copied '{name}', stripped {StripGameScripts(copy)} game scripts");
                }

                SetupTrafficAndCamera(title, map, log);
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
            }

            SceneManager.SetActiveScene(title);
            EditorSceneManager.MarkSceneDirty(title);
            EditorSceneManager.SaveScene(title);

            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/title_build.txt", log.ToString());
            Debug.Log("[TitleBuilder] Title_JW 완료 — " + log.ToString().Replace('\n', ' '));
        }

        /// <summary>프리팹 연결을 살린 채 복사하려고 에디터 복제 기능을 쓴 뒤 대상 씬으로 옮긴다.</summary>
        private static GameObject DuplicateInto(GameObject src, Scene target)
        {
            Selection.activeGameObject = src;
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            GameObject dup = Selection.activeGameObject;
            dup.name = src.name;
            SceneManager.MoveGameObjectToScene(dup, target);
            Selection.activeGameObject = null;
            return dup;
        }

        /// <summary>게임 로직 스크립트(매니저·손님·주유기 등)는 타이틀에 필요 없고 매니저가 없어 오류를 내므로 뺀다.
        /// Unity·TextMeshPro 같은 엔진 쪽 컴포넌트(URP 설정, LOD 등)는 남긴다.</summary>
        private static int StripGameScripts(GameObject root)
        {
            int removed = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }

            // RequireComponent로 묶인 스크립트는 의존하는 쪽부터 지워야 해서 더 못 지울 때까지 반복한다.
            for (int pass = 0; pass < 5; pass++)
            {
                int before = removed;
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || IsEngineType(mb.GetType()))
                    {
                        continue;
                    }

                    if (CanRemove(mb))
                    {
                        Object.DestroyImmediate(mb);
                        removed++;
                    }
                }

                if (removed == before)
                {
                    break;
                }
            }

            // 타이틀 씬엔 NavMesh가 없어 에이전트가 켜지면 경고를 쏟아낸다.
            foreach (UnityEngine.AI.NavMeshAgent agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            {
                if (CanRemove(agent))
                {
                    Object.DestroyImmediate(agent);
                    removed++;
                }
            }

            return removed;
        }

        private static bool CanRemove(Component c)
        {
            foreach (Component other in c.GetComponents<Component>())
            {
                if (other == null || other == c)
                {
                    continue;
                }

                foreach (object attr in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    var req = (RequireComponent)attr;
                    if (IsRequired(req.m_Type0, c) || IsRequired(req.m_Type1, c) || IsRequired(req.m_Type2, c))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsRequired(System.Type type, Component c) => type != null && type.IsInstanceOfType(c);

        private static bool IsEngineType(System.Type type)
        {
            string ns = type.Namespace ?? string.Empty;
            return ns.StartsWith("UnityEngine") || ns.StartsWith("Unity.") || ns.StartsWith("TMPro");
        }

        private static void CopyLighting(Scene source, Scene title, StringBuilder log)
        {
            // RenderSettings는 활성 씬 것만 읽고 쓸 수 있다.
            SceneManager.SetActiveScene(source);
            Material skybox = RenderSettings.skybox;
            var ambientMode = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            float ambientIntensity = RenderSettings.ambientIntensity;
            bool fog = RenderSettings.fog;
            Color fogColor = RenderSettings.fogColor;
            var fogMode = RenderSettings.fogMode;
            float fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            Light srcSun = FindDirectionalLight(source);

            SceneManager.SetActiveScene(title);
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            Light sun = FindDirectionalLight(title);
            if (srcSun != null && sun != null)
            {
                EditorUtility.CopySerialized(srcSun, sun);
                sun.transform.rotation = srcSun.transform.rotation;
                RenderSettings.sun = sun;
            }

            log.AppendLine($"lighting: skybox={(skybox != null ? skybox.name : "none")} fog={fog} sun={(srcSun != null ? srcSun.name : "none")}");
        }

        private static void SetupTrafficAndCamera(Scene title, GameObject map, StringBuilder log)
        {
            Transform road = FindDeep(map.transform, RoadName);
            if (road == null || !TryGetBounds(road.gameObject, out Bounds roadBounds))
            {
                log.AppendLine($"road '{RoadName}' not found — lanes not created");
                return;
            }

            TryGetBounds(map, out Bounds mapBounds);
            roadBounds = MainRoadBounds(road, map.transform, mapBounds, roadBounds, log);
            log.AppendLine($"road bounds center={roadBounds.center} size={roadBounds.size}");
            log.AppendLine($"map bounds center={mapBounds.center} size={mapBounds.size}");

            // 도로의 긴 쪽이 차가 달리는 방향.
            bool alongZ = roadBounds.size.z >= roadBounds.size.x;
            Vector3 along = alongZ ? Vector3.forward : Vector3.right;
            Vector3 across = alongZ ? Vector3.right : Vector3.forward;
            float halfLength = (alongZ ? roadBounds.size.z : roadBounds.size.x) * 0.5f;
            float halfWidth = (alongZ ? roadBounds.size.x : roadBounds.size.z) * 0.5f;
            Vector3 center = roadBounds.center;
            center.y = roadBounds.max.y;

            var traffic = new GameObject(TrafficName);
            SceneManager.MoveGameObjectToScene(traffic, title);
            TitleTraffic component = traffic.AddComponent<TitleTraffic>();

            // 한쪽 방향씩 두 차선. 차선 중심은 도로 폭의 1/4 지점.
            float laneOffset = halfWidth * 0.5f;
            float margin = 2f;
            var lanes = new (Vector3 from, Vector3 to)[]
            {
                (center - along * (halfLength - margin) + across * laneOffset, center + along * (halfLength - margin) + across * laneOffset),
                (center + along * (halfLength - margin) - across * laneOffset, center - along * (halfLength - margin) - across * laneOffset),
            };

            var so = new SerializedObject(component);
            SerializedProperty laneProp = so.FindProperty("lanes");
            laneProp.arraySize = lanes.Length;
            for (int i = 0; i < lanes.Length; i++)
            {
                Transform from = CreatePoint($"Lane {i} From", traffic.transform, lanes[i].from);
                Transform to = CreatePoint($"Lane {i} To", traffic.transform, lanes[i].to);
                SerializedProperty element = laneProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("from").objectReferenceValue = from;
                element.FindPropertyRelative("to").objectReferenceValue = to;
            }

            List<GameObject> prefabs = LoadCarPrefabs();
            SerializedProperty prefabProp = so.FindProperty("carPrefabs");
            prefabProp.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
            {
                prefabProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"traffic: {lanes.Length} lanes, {prefabs.Count} car prefabs, road along {(alongZ ? "z" : "x")} length {halfLength * 2f:F0}m width {halfWidth * 2f:F1}m");

            // 카메라: 주유소 반대편 길가에서 도로를 가로질러 주유소 쪽을 바라본다.
            // 도로가 맵 끝에서 끊기므로 화각을 좁혀 차가 나타나고 사라지는 도로 끝이 화면 밖에 있게 한다.
            float stationSide = Mathf.Sign(Vector3.Dot(mapBounds.center - center, across));
            if (stationSide == 0f)
            {
                stationSide = 1f;
            }

            const float sideDistance = 14f;
            var target = new GameObject(LookTargetName);
            SceneManager.MoveGameObjectToScene(target, title);
            target.transform.position = center + across * (stationSide * (halfWidth + sideDistance)) + along * 6f + Vector3.up * 2.5f;

            Camera cam = FindCamera(title);
            if (cam != null)
            {
                cam.transform.position = center - across * (stationSide * (halfWidth + sideDistance)) - along * 6f + Vector3.up * 4f;
                cam.transform.rotation = Quaternion.LookRotation(target.transform.position - cam.transform.position, Vector3.up);
                cam.fieldOfView = 40f;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 600f);

                TitleCameraDrift drift = cam.GetComponent<TitleCameraDrift>();
                if (drift == null)
                {
                    drift = cam.gameObject.AddComponent<TitleCameraDrift>();
                }

                var driftSo = new SerializedObject(drift);
                driftSo.FindProperty("lookTarget").objectReferenceValue = target.transform;
                driftSo.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"camera at {cam.transform.position} looking at {target.transform.position}");
            }
        }

        /// <summary>Road에는 본 도로와 가드레일 안쪽 주유소 진입로가 같이 들어 있다. 가드레일(Road fence) 바깥, 주유소 반대편 조각만 모아 본 도로로 본다.
        /// 가드레일이 없으면 Road 전체를 그대로 쓴다.</summary>
        private static Bounds MainRoadBounds(Transform road, Transform map, Bounds mapBounds, Bounds whole, StringBuilder log)
        {
            Transform fence = FindDeep(map, RoadFenceName);
            Renderer[] rails = fence != null ? fence.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (rails.Length == 0)
            {
                log.AppendLine($"'{RoadFenceName}' not found — using whole road");
                return whole;
            }

            bool alongZ = whole.size.z >= whole.size.x;
            float Across(Vector3 p) => alongZ ? p.x : p.z;

            // Road fence엔 다른 곳 난간도 섞여 있어 전체 bounds 중심은 어긋난다. 조각들의 중앙값이 도로 옆 가드레일 줄이다.
            var railLines = new List<float>();
            foreach (Renderer rail in rails)
            {
                railLines.Add(Across(rail.bounds.center));
            }

            railLines.Sort();
            float fenceLine = railLines[railLines.Count / 2];
            float stationSide = Mathf.Sign(Across(mapBounds.center) - fenceLine);

            Bounds main = default;
            int kept = 0;
            foreach (Renderer r in road.GetComponentsInChildren<Renderer>())
            {
                if (Mathf.Sign(Across(r.bounds.center) - fenceLine) == stationSide)
                {
                    continue;
                }

                if (kept++ == 0)
                {
                    main = r.bounds;
                }
                else
                {
                    main.Encapsulate(r.bounds);
                }
            }

            log.AppendLine($"main road: {kept} parts outside fence line {fenceLine:F1}");
            return kept > 0 ? main : whole;
        }

        private static List<GameObject> LoadCarPrefabs()
        {
            var list = new List<GameObject>();
            for (int i = 1; i <= 8; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarPrefabFolder}/Car{i}.prefab");
                if (prefab != null)
                {
                    list.Add(prefab);
                }
            }

            return list;
        }

        private static Transform CreatePoint(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return any;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private static IEnumerable<string> RootNames(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                yield return go.name;
            }
        }

        private static void RemoveRoot(Scene scene, string name)
        {
            GameObject go = FindRoot(scene, name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Light FindDirectionalLight(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (Light light in go.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional)
                    {
                        return light;
                    }
                }
            }

            return null;
        }

        private static Camera FindCamera(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                Camera cam = go.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    return cam;
                }
            }

            return null;
        }
    }
}
