using System.Linq;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using DevLib.AnimatorSystem;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>기획서의 정상 손님 3(다른 차 앞에서 대화, INDEX3)과 정상 손님 4(자판기 앞으로 감, INDEX8)를 만든다.
    /// 비정상 손님(Car Basher·Vending Basher)의 변형으로 만들어 대상 찾기·걸어가기는 그대로 쓰고, VandalizeState.noContact로 때리지만 않게 한다.
    /// 이미 있는 에셋은 다시 만들지 않고 설정만 덮어쓴다. 여러 번 눌러도 안전하다.</summary>
    public static class NormalCustomerCreator
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string DataFolder = "Assets/_Works/CJW/Data/Customers";
        private const string ParamFolder = "Assets/_Works/CJW/Data/ParamHash";
        private const string PoolManagerPath = "Assets/DevLib/ObjectPool/PoolManager.asset";
        private static readonly string[] CarDataPaths = { "Assets/_Works/CJW/Data/Car Data.asset", "Assets/_Works/CJW/Data/Test SUV.asset" };

        private readonly struct Spec
        {
            public readonly string Name;
            public readonly string BaseName;
            public readonly CustomerType Type;
            public readonly int Line;
            public readonly string Clip;
            public readonly int Count;
            public readonly float Interval;

            public Spec(string name, string baseName, CustomerType type, int line, string clip, int count, float interval)
            {
                Name = name;
                BaseName = baseName;
                Type = type;
                Line = line;
                Clip = clip;
                Count = count;
                Interval = interval;
            }
        }

        // 동작 시간(Count × Interval)은 대사가 다 나올 만큼(대사 줄 수 × delayTime) 잡는다.
        // 대화 클립이 없어 IDLE로 임시로 채운다 — 빠진 애니메이션 목록에 올릴 것.
        private static readonly Spec[] Specs =
        {
            new("Car Talker Customer", "Car Basher Customer", CustomerType.CarTalker, 3, "IDLE", 5, 4f),
            new("Vending Visitor Customer", "Vending Basher Customer", CustomerType.VendingVisitor, 8, "IMPATIENT", 5, 5f),
        };

        [MenuItem("Tools/JW/Customers/Create Normal Talker And Vending Customers")]
        private static void CreateAll()
        {
            foreach (Spec spec in Specs)
            {
                Create(spec);
            }

            AssetDatabase.SaveAssets();
        }

        private static void Create(Spec spec)
        {
            string prefabPath = $"{PrefabFolder}/{spec.Name}.prefab";
            string poolItemPath = $"{DataFolder}/{spec.Name} Pool Item.asset";
            string dataPath = $"{DataFolder}/{spec.Name}.asset";

            // 1. 프리팹: 비정상 손님의 변형
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{spec.BaseName}.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
            }

            // 2. 풀 아이템
            var poolItem = AssetDatabase.LoadAssetAtPath<PoolItemSO>(poolItemPath);
            if (poolItem == null)
            {
                poolItem = ScriptableObject.CreateInstance<PoolItemSO>();
                AssetDatabase.CreateAsset(poolItem, poolItemPath);
            }

            poolItem.poolingName = spec.Name;
            poolItem.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            poolItem.initCount = 5;
            EditorUtility.SetDirty(poolItem);

            var clip = AssetDatabase.LoadAssetAtPath<HashDataSO>($"{ParamFolder}/{spec.Clip} param.asset");

            // 3. 프리팹 설정: 정상 손님, 자기 풀 아이템, 때리지 않기, 대사
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var customer = root.GetComponent<AbstractCustomer>();
                var customerSo = new SerializedObject(customer);
                customerSo.FindProperty("<HumanType>k__BackingField").intValue = (int)HumanType.Good;
                customerSo.FindProperty("<PoolItem>k__BackingField").objectReferenceValue = poolItem;
                customerSo.ApplyModifiedPropertiesWithoutUndo();

                var fsm = root.GetComponentInChildren<CustomerFSMModule>(true);
                var fsmSo = new SerializedObject(fsm);
                SerializedProperty sequences = fsmSo.FindProperty("sequences");

                for (int i = 0; i < sequences.arraySize; i++)
                {
                    SerializedProperty states = sequences.GetArrayElementAtIndex(i).FindPropertyRelative("States");
                    for (int j = 0; j < states.arraySize; j++)
                    {
                        SerializedProperty state = states.GetArrayElementAtIndex(j);
                        switch (state.managedReferenceValue)
                        {
                            case VandalizeState:
                                state.FindPropertyRelative("noContact").boolValue = true;
                                state.FindPropertyRelative("hitCount").intValue = spec.Count;
                                state.FindPropertyRelative("hitInterval").floatValue = spec.Interval;
                                SerializedProperty clips = state.FindPropertyRelative("hitClips");
                                clips.arraySize = 1;
                                clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                                break;

                            case SpeechState:
                                SerializedProperty lines = state.FindPropertyRelative("lineIndices");
                                lines.arraySize = 1;
                                lines.GetArrayElementAtIndex(0).intValue = spec.Line;
                                break;
                        }
                    }
                }

                fsmSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 4. 손님 데이터: 원본 손님 데이터를 복사해 이동 수치 등을 물려받는다.
            var data = AssetDatabase.LoadAssetAtPath<CustomerDataSO>(dataPath);
            if (data == null)
            {
                var baseData = AssetDatabase.LoadAssetAtPath<CustomerDataSO>($"{DataFolder}/{spec.BaseName}.asset");
                data = Object.Instantiate(baseData);
                AssetDatabase.CreateAsset(data, dataPath);
            }

            var dataSo = new SerializedObject(data);
            dataSo.FindProperty("poolItem").objectReferenceValue = poolItem;
            dataSo.FindProperty("<CustomerType>k__BackingField").intValue = (int)spec.Type;
            dataSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. 등록: 풀 매니저, 차 데이터의 손님 목록
            var poolManager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(PoolManagerPath);
            if (!poolManager.itemList.Contains(poolItem))
            {
                poolManager.itemList.Add(poolItem);
                EditorUtility.SetDirty(poolManager);
            }

            foreach (string carPath in CarDataPaths)
            {
                var car = AssetDatabase.LoadAssetAtPath<ScriptableObject>(carPath);
                var carSo = new SerializedObject(car);
                SerializedProperty list = carSo.FindProperty("customers");
                bool has = Enumerable.Range(0, list.arraySize).Any(k => list.GetArrayElementAtIndex(k).objectReferenceValue == data);
                if (!has)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
                    carSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Debug.Log($"[NormalCustomerCreator] {spec.Name}: {spec.Type}, 대사 {spec.Line}, 클립 {spec.Clip}{(clip == null ? "(없음!)" : "")} × {spec.Count}회/{spec.Interval}s");
        }
    }
}
