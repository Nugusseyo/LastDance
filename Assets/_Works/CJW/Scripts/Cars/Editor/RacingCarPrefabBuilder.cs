using System.Collections.Generic;
using System.IO;
using System.Text;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.MapSystems;
using DevLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars.Editor
{
    /// <summary>TestSUV를 틀로 삼아 Racing Cars Pack의 차 모델마다 차 프리팹을 만든다.
    /// 비주얼만 갈아끼우고, 크기에 묶인 값(장애물·에이전트 반경·좌석·하차 지점·휠베이스)은 Car2 대비 크기 비율로 맞춘다.</summary>
    public static class RacingCarPrefabBuilder
    {
        private const string TemplatePath = "Assets/_Works/Share/Prefabs/TestSUV.prefab";
        private const string TemplateVisualName = "Car2";
        private const string SourceFolder = "Assets/Racing Cars Pack 1/Prefabs";
        private const string OutputFolder = "Assets/_Works/CJW/Prefabs/Cars";
        private const string PoolItemFolder = "Assets/DevLib/ObjectPool/Items";
        private const string PoolManagerPath = "Assets/DevLib/ObjectPool/PoolManager.asset";
        private const string CarDataFolder = "Assets/_Works/CJW/Data/Cars";
        /// <summary>팩 모델에 곱할 크기. 0.7에서 1.2배 키웠다(2026-09-28). 바꾼 뒤 Resize Racing Car Prefabs를 돌리면 만든 프리팹에 반영된다.</summary>
        private const float VisualScale = 0.84f;

        [MenuItem("Tools/JW/Cars/Build Racing Car Prefabs")]
        public static void Build()
        {
            var sb = new StringBuilder("[RacingCarPrefabBuilder]\n");

            if (AssetDatabase.IsValidFolder(OutputFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/_Works/CJW/Prefabs", "Cars");
            }

            // 틀의 기준 크기. 새 차의 크기 비율을 이것과 비교한다.
            GameObject template = PrefabUtility.LoadPrefabContents(TemplatePath);
            Bounds baseBounds;
            try
            {
                baseBounds = LocalBounds(template.transform, template.transform.Find(TemplateVisualName));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(template);
            }

            sb.AppendLine($"template bounds c={baseBounds.center} s={baseBounds.size}");

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SourceFolder }))
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
                string carName = Path.GetFileNameWithoutExtension(sourcePath);
                string outPath = $"{OutputFolder}/{carName}.prefab";

                GameObject root = PrefabUtility.LoadPrefabContents(TemplatePath);
                try
                {
                    BuildOne(root, sourcePath, carName, baseBounds, sb);
                    PrefabUtility.SaveAsPrefabAsset(root, outPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                LinkPoolItem(outPath, carName, sb);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        private static void BuildOne(GameObject root, string sourcePath, string carName, Bounds baseBounds, StringBuilder sb)
        {
            root.name = carName;
            Transform oldVisual = root.transform.Find(TemplateVisualName);

            // 옛 비주얼을 가리키던 참조를 이름으로 기억해 두었다가 새 비주얼에서 같은 이름을 찾아 잇는다.
            CarWheelModule wheelModule = root.GetComponent<CarWheelModule>();
            var wheelSo = new SerializedObject(wheelModule);
            SerializedProperty wheels = wheelSo.FindProperty("wheels");
            var wheelNames = new List<string>();
            for (int i = 0; i < wheels.arraySize; i++)
            {
                var t = wheels.GetArrayElementAtIndex(i).FindPropertyRelative("transform").objectReferenceValue as Transform;
                wheelNames.Add(t != null ? t.name : null);
            }

            Object.DestroyImmediate(oldVisual.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            visual.transform.SetSiblingIndex(0);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * VisualScale;
            visual.transform.localPosition = Vector3.zero;

            // 차 중심이 루트 위에 오도록 좌우만 맞춘다. 앞뒤는 틀(TestSUV)과 같은 기준을 쓰도록 비율로 옮긴다.
            Bounds b = LocalBounds(root.transform, visual.transform);
            Vector3 ratio = new Vector3(b.size.x / baseBounds.size.x, b.size.y / baseBounds.size.y, b.size.z / baseBounds.size.z);
            visual.transform.localPosition = new Vector3(
                baseBounds.center.x - b.center.x,
                0f,
                baseBounds.center.z * ratio.z - b.center.z);
            b = LocalBounds(root.transform, visual.transform);

            // 바퀴
            for (int i = 0; i < wheels.arraySize; i++)
            {
                Transform w = wheelNames[i] != null ? visual.transform.Find(wheelNames[i]) : null;
                wheels.GetArrayElementAtIndex(i).FindPropertyRelative("transform").objectReferenceValue = w;
                if (w == null)
                {
                    sb.AppendLine($"  !! {carName}: 바퀴 {wheelNames[i]} 없음");
                }
            }
            wheelSo.ApplyModifiedPropertiesWithoutUndo();

            // 차체 렌더러 = 바퀴가 아닌 렌더러
            var bodies = new List<Renderer>();
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Wheel") == false)
                {
                    bodies.Add(r);
                }
            }

            TestCar car = root.GetComponent<TestCar>();
            var carSo = new SerializedObject(car);
            SerializedProperty bodyProp = carSo.FindProperty("bodyRenderers");
            bodyProp.arraySize = bodies.Count;
            for (int i = 0; i < bodies.Count; i++)
            {
                bodyProp.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            }

            // 좌석·하차 지점은 크기 비율로 옮긴다.
            SerializedProperty seats = carSo.FindProperty("seats");
            for (int i = 0; i < seats.arraySize; i++)
            {
                ScaleLocal(seats.GetArrayElementAtIndex(i).objectReferenceValue as Transform, ratio);
            }
            ScaleLocal(carSo.FindProperty("dropOffPoint").objectReferenceValue as Transform, ratio);
            carSo.FindProperty("<PoolItem>k__BackingField").objectReferenceValue = null;
            carSo.ApplyModifiedPropertiesWithoutUndo();

            // 장애물 박스
            NavMeshObstacle obstacle = root.GetComponentInChildren<NavMeshObstacle>(true);
            obstacle.size = Vector3.Scale(obstacle.size, ratio);
            obstacle.center = Vector3.Scale(obstacle.center, ratio);

            // 에이전트
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            agent.radius *= ratio.x;
            agent.height *= ratio.y;

            // 틀의 휠베이스는 실측(약 3.25)이 아니라 회전 반경을 맞춘 튜닝값이라, 실측 대신 길이 비율로만 늘리고 줄인다.
            var moveSo = new SerializedObject(root.GetComponent<CarSteeringMoveModule>());
            SerializedProperty wheelBase = moveSo.FindProperty("_wheelBase");
            wheelBase.floatValue *= ratio.z;
            moveSo.ApplyModifiedPropertiesWithoutUndo();

            // 교통 센서의 대체 크기(반폭, 반길이)
            var sensorSo = new SerializedObject(root.GetComponent<CarTrafficSensorModule>());
            SerializedProperty half = sensorSo.FindProperty("fallbackHalfSize");
            half.vector2Value = new Vector2(half.vector2Value.x * ratio.x, half.vector2Value.y * ratio.z);
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            sb.AppendLine($"  {carName}: size={b.size} ratio={ratio} wheelBase={wheelBase.floatValue:F2} bodies={bodies.Count}");
        }

        /// <summary>이미 만든 차 프리팹의 크기를 <see cref="VisualScale"/>에 맞춘다. Build를 다시 돌리면 뒤에 붙인 설정(바퀴·주유구·사운드 등)이
        /// 날아가므로, 지금 모델 배율과의 비율만큼 크기에 묶인 값을 함께 늘리고 줄인다. 이미 맞춰져 있으면 건너뛴다.
        /// 좌석·하차 지점은 위치만 옮긴다 — 좌석을 키우면 거기 앉는 손님도 커지고, 내릴 때 그 크기로 남는다.</summary>
        [MenuItem("Tools/JW/Cars/Resize Racing Car Prefabs")]
        public static void Resize()
        {
            var sb = new StringBuilder("[RacingCarPrefabBuilder] Resize\n");

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { OutputFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (ResizeOne(root, sb))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        private static bool ResizeOne(GameObject root, StringBuilder sb)
        {
            TestCar car = root.GetComponent<TestCar>();
            if (car == null || root.transform.childCount == 0)
            {
                sb.AppendLine($"  {root.name}: 차가 아님, 건너뜀");
                return false;
            }

            // Build가 모델을 첫 번째 자식으로 넣는다.
            Transform visual = root.transform.GetChild(0);
            if (PrefabUtility.IsAnyPrefabInstanceRoot(visual.gameObject) == false)
            {
                sb.AppendLine($"  !! {root.name}: 첫 자식 {visual.name}이(가) 모델 프리팹이 아님, 건너뜀");
                return false;
            }

            float current = visual.localScale.x;
            float factor = VisualScale / current;
            if (current <= 0f || Mathf.Abs(factor - 1f) < 1e-3f)
            {
                sb.AppendLine($"  {root.name}: 이미 {current:F2}배, 건너뜀");
                return false;
            }

            var carSo = new SerializedObject(car);
            var markers = new HashSet<Transform>();
            SerializedProperty seats = carSo.FindProperty("seats");
            for (int i = 0; i < seats.arraySize; i++)
            {
                if (seats.GetArrayElementAtIndex(i).objectReferenceValue is Transform seat)
                {
                    markers.Add(seat);
                }
            }

            if (carSo.FindProperty("dropOffPoint").objectReferenceValue is Transform dropOff)
            {
                markers.Add(dropOff);
            }

            // 루트 바로 아래 자식은 루트 기준으로 벌어진다. 표식(좌석·하차 지점)은 위치만, 나머지(모델·장애물·주유구)는 크기까지.
            foreach (Transform child in root.transform)
            {
                child.localPosition *= factor;
                if (markers.Contains(child) == false)
                {
                    child.localScale *= factor;
                }
            }

            // 좌석이 루트 바로 아래가 아니면 위에서 못 옮겼다.
            foreach (Transform marker in markers)
            {
                if (marker.parent != root.transform)
                {
                    sb.AppendLine($"  !! {root.name}: {marker.name}이(가) 루트 바로 아래가 아니라 위치를 못 옮김");
                }
            }

            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius *= factor;
                agent.height *= factor;
            }

            var moveSo = new SerializedObject(root.GetComponent<CarSteeringMoveModule>());
            SerializedProperty wheelBase = moveSo.FindProperty("_wheelBase");
            wheelBase.floatValue *= factor;
            moveSo.ApplyModifiedPropertiesWithoutUndo();

            var sensorSo = new SerializedObject(root.GetComponent<CarTrafficSensorModule>());
            SerializedProperty half = sensorSo.FindProperty("fallbackHalfSize");
            half.vector2Value *= factor;
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            sb.AppendLine($"  {root.name}: {current:F2} → {VisualScale:F2} (x{factor:F2}), wheelBase={wheelBase.floatValue:F2}, 표식 {markers.Count}개");
            return true;
        }

        /// <summary>만든 풀 아이템을 PoolManager에 올리고, 차마다 CarDataSO를 만든다.
        /// 손님·색은 비워둬서 VisitDirector 기본 손님과 모델 원래 머티리얼을 쓴다.</summary>
        [MenuItem("Tools/JW/Cars/Register Racing Cars (Pool + CarData)")]
        public static void Register()
        {
            var sb = new StringBuilder("[RacingCarPrefabBuilder] Register\n");

            var manager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(PoolManagerPath);
            var managerSo = new SerializedObject(manager);
            SerializedProperty itemList = managerSo.FindProperty("itemList");

            if (AssetDatabase.IsValidFolder(CarDataFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/_Works/CJW/Data", "Cars");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:PoolItemSO Racing", new[] { PoolItemFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<PoolItemSO>(AssetDatabase.GUIDToAssetPath(guid));

                bool listed = false;
                for (int i = 0; i < itemList.arraySize; i++)
                {
                    if (itemList.GetArrayElementAtIndex(i).objectReferenceValue == item)
                    {
                        listed = true;
                        break;
                    }
                }

                if (listed == false)
                {
                    itemList.arraySize++;
                    itemList.GetArrayElementAtIndex(itemList.arraySize - 1).objectReferenceValue = item;
                    sb.AppendLine($"  pool += {item.name}");
                }

                string dataPath = $"{CarDataFolder}/{item.name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(dataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CarDataSO>();
                    AssetDatabase.CreateAsset(data, dataPath);
                    sb.AppendLine($"  data  += {dataPath}");
                }

                var dataSo = new SerializedObject(data);
                dataSo.FindProperty("poolItem").objectReferenceValue = item;
                dataSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }

            managerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        /// <summary>열린 씬의 VisitDirector carDataList에 Racing CarData를 넣고 씬을 저장한다. 이미 있는 항목은 건너뛴다.</summary>
        [MenuItem("Tools/JW/Cars/Add Racing CarData To Open Scene")]
        public static void AddToScene()
        {
            var sb = new StringBuilder("[RacingCarPrefabBuilder] AddToScene\n");

            foreach (VisitDirector director in Object.FindObjectsByType<VisitDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(director);
                SerializedProperty list = so.FindProperty("carDataList");

                foreach (string guid in AssetDatabase.FindAssets("t:CarDataSO Racing", new[] { CarDataFolder }))
                {
                    var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(AssetDatabase.GUIDToAssetPath(guid));

                    bool listed = false;
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        if (list.GetArrayElementAtIndex(i).objectReferenceValue == data)
                        {
                            listed = true;
                            break;
                        }
                    }

                    if (listed == false)
                    {
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
                        sb.AppendLine($"  {director.gameObject.scene.name}/{director.name} += {data.name}");
                    }
                }

                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
                EditorSceneManager.SaveScene(director.gameObject.scene);
            }

            Debug.Log(sb.ToString());
        }

        private static void LinkPoolItem(string prefabPath, string carName, StringBuilder sb)
        {
            string itemPath = $"{PoolItemFolder}/Racing {carName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<PoolItemSO>(itemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<PoolItemSO>();
                AssetDatabase.CreateAsset(item, itemPath);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            item.poolingName = $"Racing {carName}";
            item.prefab = prefab;
            item.initCount = 3;
            EditorUtility.SetDirty(item);

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var so = new SerializedObject(contents.GetComponent<TestCar>());
                so.FindProperty("<PoolItem>k__BackingField").objectReferenceValue = item;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            sb.AppendLine($"  pool item: {itemPath}");
        }

        private static void ScaleLocal(Transform t, Vector3 ratio)
        {
            if (t != null)
            {
                t.localPosition = Vector3.Scale(t.localPosition, ratio);
            }
        }

        /// <summary>visual 아래 렌더러들의 경계를 root 로컬 공간으로 모은다.</summary>
        private static Bounds LocalBounds(Transform root, Transform visual)
        {
            bool has = false;
            var bounds = new Bounds();
            foreach (MeshFilter mf in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (has == false)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }
    }
}
