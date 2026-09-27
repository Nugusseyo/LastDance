using System.Collections.Generic;
using System.IO;
using System.Text;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.MapSystems;
using _Works.JJH._02_Scripts.Items;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.KDH._01.Scripts.Car;
using DevLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars.Editor
{
    /// <summary>Racing Cars pack의 RePrefabs(등급 이름 + 판매·바퀴 스크립트)를 CJW Cars 프리팹에 옮긴다.
    /// 두 프리팹은 차체 메시의 FBX가 같으면 같은 차로 본다.</summary>
    public static class CarGradeSetup
    {
        private const string RePrefabFolder = "Assets/Racing Cars pack 1/RePrefabs";
        private const string CarFolder = "Assets/_Works/CJW/Prefabs/Cars";
        private const string CarDataFolder = "Assets/_Works/CJW/Data";
        private const string GradeTablePath = "Assets/_Works/CJW/Data/Cars/Car Grade Table.asset";
        private const string ReputationPath = "Assets/_Works/JYG/Data/Data Managers/Review Manager.asset";

        /// <summary>방문 연출 전용 차(겉모습은 Share/Car). 등급이 없으면 등급 추첨에서 빠져 안 나오므로 Low에 넣는다.</summary>
        private static readonly string[] BehaviourCarNames = { "Circler Car", "Entrance Blocker Car" };

        [MenuItem("Tools/JW/Cars/Port RePrefab Scripts + Grades")]
        public static void Port()
        {
            var sb = new StringBuilder("[CarGradeSetup] Port\n");

            // 차체 FBX 경로 → RePrefab. 같은 FBX를 쓰는 RePrefab이 둘이면(MidCar2/3) 이름순 앞의 것을 쓴다.
            var byFbx = new SortedDictionary<string, GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RePrefabFolder }))
            {
                var re = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (ParseGrade(re.name) == CarGrade.None)
                {
                    continue;
                }

                string fbx = BodyFbx(re);
                if (fbx == null)
                {
                    sb.AppendLine($"  !! {re.name}: 차체 메시 없음");
                    continue;
                }

                if (byFbx.TryGetValue(fbx, out GameObject prev) && string.CompareOrdinal(prev.name, re.name) < 0)
                {
                    sb.AppendLine($"  {re.name}: {prev.name}와 겉모습이 같아 {prev.name}를 쓴다");
                    continue;
                }

                byFbx[fbx] = re;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CarFolder }))
            {
                string carPath = AssetDatabase.GUIDToAssetPath(guid);
                var carAsset = AssetDatabase.LoadAssetAtPath<GameObject>(carPath);
                string fbx = BodyFbx(carAsset);

                if (fbx == null || !byFbx.TryGetValue(fbx, out GameObject re))
                {
                    sb.AppendLine($"  !! {carAsset.name}: 같은 겉모습의 RePrefab 없음 (fbx={fbx})");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(carPath);
                try
                {
                    PortOne(root, re, sb);
                    PrefabUtility.SaveAsPrefabAsset(root, carPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                CarGrade grade = ParseGrade(re.name);
                int assigned = AssignGrade(carAsset, grade);
                sb.AppendLine($"  {carAsset.name} ← {re.name} ({grade}), CarData {assigned}개");
            }

            foreach (string name in BehaviourCarNames)
            {
                var data = AssetDatabase.LoadAssetAtPath<CarDataSO>($"{CarDataFolder}/{name}.asset");
                if (data != null && data.Grade == CarGrade.None)
                {
                    SetGrade(data, CarGrade.Low);
                    sb.AppendLine($"  {name}: 등급 없음 → Low");
                }
            }

            EnsureGradeTable(sb);
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        /// <summary>열린 씬의 VisitDirector에 평판(Review Manager)과 등급 표를 넣고 씬을 저장한다.</summary>
        [MenuItem("Tools/JW/Cars/Assign Reputation + Grade Table To Open Scene")]
        public static void AssignToScene()
        {
            var sb = new StringBuilder("[CarGradeSetup] AssignToScene\n");
            var table = AssetDatabase.LoadAssetAtPath<CarGradeTableSO>(GradeTablePath);
            var reputation = AssetDatabase.LoadAssetAtPath<IntegerDataContainer>(ReputationPath);

            foreach (VisitDirector director in Object.FindObjectsByType<VisitDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(director);
                so.FindProperty("reputation").objectReferenceValue = reputation;
                so.FindProperty("carGradeTable").objectReferenceValue = table;
                so.ApplyModifiedProperties();

                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
                EditorSceneManager.SaveScene(director.gameObject.scene);
                sb.AppendLine($"  {director.gameObject.scene.name}/{director.name}: reputation={reputation?.name}, table={table?.name}");
            }

            Debug.Log(sb.ToString());
        }

        private static void PortOne(GameObject root, GameObject re, StringBuilder sb)
        {
            // 판매 가격
            var reSeller = re.GetComponent<CarSeller>();
            if (reSeller != null)
            {
                EditorUtility.CopySerialized(reSeller, GetOrAdd<CarSeller>(root));
            }

            // 바퀴: RePrefab 바퀴의 레이어와 Rigidbody·WheelCollider·GrabItem을 옮긴다.
            var reWheel = re.GetComponentInChildren<GrabItem>(true);
            var wheelSo = new SerializedObject(root.GetComponent<CarWheelModule>());
            SerializedProperty wheels = wheelSo.FindProperty("wheels");
            var wheelTransforms = new List<Transform>();

            for (int i = 0; i < wheels.arraySize; i++)
            {
                var w = wheels.GetArrayElementAtIndex(i).FindPropertyRelative("transform").objectReferenceValue as Transform;
                if (w == null)
                {
                    continue;
                }

                wheelTransforms.Add(w);
                if (reWheel == null)
                {
                    continue;
                }

                w.gameObject.layer = reWheel.gameObject.layer;
                EditorUtility.CopySerialized(reWheel.GetComponent<Rigidbody>(), GetOrAdd<Rigidbody>(w.gameObject));

                var collider = GetOrAdd<WheelCollider>(w.gameObject);
                EditorUtility.CopySerialized(reWheel.GetComponent<WheelCollider>(), collider);
                FitWheelCollider(w, collider);

                EditorUtility.CopySerialized(reWheel, GetOrAdd<GrabItem>(w.gameObject));
            }

            // 직진 이동기. 우리 차는 이동 모듈이 몰기 때문에 꺼 둔다. 켜 두면 Update에서 매 프레임 앞으로 밀어 경로를 벗어난다.
            // KDH 쪽(PartDetacher·NailTirePopper·CubeTeleportDetector)이 GetComponent로 찾아 Stop()을 부르므로 컴포넌트는 둔다.
            var reMover = re.GetComponent<CarStraightMover>();
            if (reMover != null)
            {
                var mover = GetOrAdd<CarStraightMover>(root);
                EditorUtility.CopySerialized(reMover, mover);
                mover.enabled = false;

                var moverSo = new SerializedObject(mover);
                SerializedProperty moverWheels = moverSo.FindProperty("wheels");
                moverWheels.arraySize = wheelTransforms.Count;
                for (int i = 0; i < wheelTransforms.Count; i++)
                {
                    moverWheels.GetArrayElementAtIndex(i).objectReferenceValue = wheelTransforms[i];
                }
                moverSo.ApplyModifiedPropertiesWithoutUndo();
            }

            sb.AppendLine($"  {root.name}: seller={reSeller != null}, wheels={wheelTransforms.Count}, mover={reMover != null}");
        }

        /// <summary>바퀴 메시 크기에 맞춘다. RePrefab 바퀴와 모델·스케일이 달라 반지름을 그대로 쓰면 어긋난다.</summary>
        private static void FitWheelCollider(Transform wheel, WheelCollider collider)
        {
            var mf = wheel.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                return;
            }

            Bounds b = mf.sharedMesh.bounds;
            collider.center = b.center;
            collider.radius = Mathf.Max(b.extents.y, b.extents.z);
        }

        private static int AssignGrade(GameObject carPrefab, CarGrade grade)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:CarDataSO", new[] { CarDataFolder }))
            {
                var data = AssetDatabase.LoadAssetAtPath<CarDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                PoolItemSO item = data.PoolItem;
                if (item == null || item.prefab != carPrefab)
                {
                    continue;
                }

                SetGrade(data, grade);
                count++;
            }

            return count;
        }

        private static void SetGrade(CarDataSO data, CarGrade grade)
        {
            var so = new SerializedObject(data);
            so.FindProperty("grade").enumValueIndex = (int)grade;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        private static void EnsureGradeTable(StringBuilder sb)
        {
            if (AssetDatabase.LoadAssetAtPath<CarGradeTableSO>(GradeTablePath) != null)
            {
                return;
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CarGradeTableSO>(), GradeTablePath);
            sb.AppendLine($"  table += {GradeTablePath}");
        }

        /// <summary>차체(바퀴가 아닌 렌더러)의 메시가 들어 있는 FBX 경로.</summary>
        private static string BodyFbx(GameObject prefab)
        {
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name.Contains("Wheel"))
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (Path.GetExtension(path).ToLowerInvariant() == ".fbx")
                {
                    return path;
                }
            }

            return null;
        }

        private static CarGrade ParseGrade(string prefabName)
        {
            if (prefabName.StartsWith("LowCar")) return CarGrade.Low;
            if (prefabName.StartsWith("MidCar")) return CarGrade.Mid;
            if (prefabName.StartsWith("HighCar")) return CarGrade.High;
            if (prefabName.StartsWith("SuperCar")) return CarGrade.Super;
            return CarGrade.None;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
        }
    }
}
