using System;
using System.Collections.Generic;
using System.Reflection;
using _Works.CJW.Scripts.Customers.Animation;
using _Works.CJW.Scripts.Customers.Interaction;
using _Works.CJW.Scripts.Customers.Movement;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using _Works.CJW.Scripts.MapSystems;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Works.CJW.Scripts.Customers.Data.Editor
{
    /// <summary>기획서에 적힌 손님 종류를 한 번에 찍어내는 도구. 한 종류씩 만드는 <see cref="CustomerDataCreator"/>와
    /// 같은 순서(프리팹 → 풀 항목 → 데이터 → 풀 매니저 등록)를 따르되, 어떤 행동을 넣을지는 아래 표가 들고 있다.
    /// 이미 같은 이름의 데이터가 있으면 건너뛰므로 여러 번 눌러도 사본이 쌓이지 않는다 —
    /// 손으로 고쳐 둔 손님을 이 도구가 되돌려 버리지 않게 하려는 것이다.</summary>
    public static class CustomerBatchCreator
    {
        private const string TemplatePath = "Assets/_Works/CJW/Prefabs/Customers/New Customer.prefab";
        private const string DataFolder = "Assets/_Works/CJW/Data/Customers";
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";

        /// <summary>CustomerFSMModule의 직렬화 이름. 필드 이름을 바꾸면 여기도 따라와야 한다.</summary>
        private const string SequencesField = "sequences";
        private const string MapDataField = "mapData";
        private const string PhaseField = "Phase";
        private const string StatesField = "States";

        /// <summary>손님 한 종류를 어떻게 찍을지. 행동은 Phase별 상태 배열로만 적는다.</summary>
        private sealed class Recipe
        {
            public string Name;
            public CustomerType Type;
            public HumanType Human = HumanType.Good;

            /// <summary>프리팹에 더 붙일 모듈. 연출·걸음걸이·요구 창구는 필요한 손님에게만 붙인다.</summary>
            public Type[] Modules = Array.Empty<Type>();

            /// <summary>Phase마다 실행할 상태. 비어 있는 Phase는 적지 않는다 — 적지 않으면 그 단계는 즉시 넘어간다.</summary>
            public (VisitPhase Phase, CustomerState[] States)[] Sequences;
        }

        [MenuItem("Tools/Customer Generator (기획 손님 전체 양산)")]
        public static void CreateAll()
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (template == null || template.GetComponent<AbstractCustomer>() == null)
            {
                EditorUtility.DisplayDialog("손님 양산", $"템플릿 프리팹을 찾지 못했습니다:\n{TemplatePath}", "확인");
                return;
            }

            // 템플릿이 이미 물고 있는 참조를 그대로 물려받는다. 경로를 새로 박아 두면 에셋을 옮겼을 때 조용히 끊긴다.
            CustomerFSMModule templateModule = template.GetComponentInChildren<CustomerFSMModule>(true);
            MapDataSo mapData = templateModule != null ? ReadObject<MapDataSo>(templateModule, MapDataField) : null;
            PoolManagerSO poolManager = FindPoolManager(template);

            if (mapData == null)
            {
                Debug.LogWarning($"[손님 양산] 템플릿에 MapData가 없어 만든 손님들의 MapData 칸이 비어 있습니다. " +
                                 "인스펙터에서 채워야 목적지를 찾습니다.");
            }

            Recipe[] recipes = BuildRecipes();

            int created = 0;
            int skipped = 0;

            try
            {
                for (int i = 0; i < recipes.Length; i++)
                {
                    Recipe recipe = recipes[i];

                    if (AssetDatabase.LoadAssetAtPath<CustomerDataSO>($"{DataFolder}/{recipe.Name}.asset") != null)
                    {
                        skipped++;
                        continue;
                    }

                    if (Create(recipe, template, mapData, poolManager))
                    {
                        created++;
                    }
                }
            }
            finally
            {
                // 프리팹을 굽는 동안에는 AssetDatabase를 묶어 두지 않는다.
                // StartAssetEditing 중의 SaveAsPrefabAsset은 임포트 시점이 어긋나 빈 프리팹이 나올 수 있다.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[손님 양산] {created}종을 만들었고 {skipped}종은 이미 있어 건너뛰었습니다.");
        }

        private static bool Create(Recipe recipe, GameObject template, MapDataSo mapData, PoolManagerSO poolManager)
        {
            // InstantiatePrefab으로 올린 인스턴스를 저장하면 템플릿의 배리언트가 된다.
            // 템플릿(모델·애니메이터·NavMeshAgent)을 고치면 손님들이 함께 따라오라는 뜻이다.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
            if (instance == null)
            {
                Debug.LogError($"[손님 양산] {recipe.Name}: 템플릿을 인스턴스화하지 못했습니다.", template);
                return false;
            }

            GameObject prefab;

            try
            {
                instance.name = recipe.Name;

                AddModules(instance, recipe.Modules);
                ApplyHumanType(instance, recipe.Human);
                ApplySequences(instance, recipe, mapData);

                // 저장 전에 심어야 오버라이드가 배리언트 고유값으로 굳는다. 저장 뒤에 심으면 한 번 더 구워야 한다.
                string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/{recipe.Name}.prefab");
                prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);

                if (!success || prefab == null)
                {
                    Debug.LogError($"[손님 양산] {recipe.Name}: {path} 에 프리팹을 저장하지 못했습니다.");
                    return false;
                }
            }
            finally
            {
                // 안 치우면 손님을 만들 때마다 씬에 하나씩 쌓인다.
                Object.DestroyImmediate(instance);
            }

            PoolItemSO poolItem = CreatePoolItem(prefab, recipe.Name);
            CustomerDataSO data = CreateData(recipe, poolItem);

            RegisterInPoolManager(poolManager, poolItem);

            return data != null;
        }

        /// <summary>이 손님에게만 필요한 모듈을 붙인다. 이미 붙어 있으면 그대로 둔다.</summary>
        private static void AddModules(GameObject instance, Type[] modules)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                if (instance.GetComponentInChildren(modules[i], true) == null)
                {
                    instance.AddComponent(modules[i]);
                }
            }
        }

        private static void ApplyHumanType(GameObject instance, HumanType human)
        {
            AbstractCustomer customer = instance.GetComponent<AbstractCustomer>();
            if (customer == null)
            {
                return;
            }

            SerializedObject so = new(customer);

            // [field: SerializeField] 자동 프로퍼티는 이 이름으로 직렬화된다.
            SerializedProperty property = so.FindProperty("<HumanType>k__BackingField");

            if (property != null)
            {
                property.intValue = (int)human;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            so.Dispose();
        }

        /// <summary>행동 시퀀스를 프리팹의 CustomerFSMModule에 심는다. 상태는 [SerializeReference]라
        /// 배열 요소에 인스턴스를 그대로 얹는다 — 값이 아니라 객체가 통째로 직렬화된다.</summary>
        private static void ApplySequences(GameObject instance, Recipe recipe, MapDataSo mapData)
        {
            CustomerFSMModule module = instance.GetComponentInChildren<CustomerFSMModule>(true);

            if (module == null)
            {
                module = instance.AddComponent<CustomerFSMModule>();
                Debug.LogWarning($"[손님 양산] {recipe.Name}: 템플릿에 CustomerFSMModule이 없어 새로 붙였습니다.", instance);
            }

            SerializedObject so = new(module);

            SerializedProperty mapDataProperty = so.FindProperty(MapDataField);
            if (mapDataProperty != null && mapData != null)
            {
                mapDataProperty.objectReferenceValue = mapData;
            }

            SerializedProperty sequences = so.FindProperty(SequencesField);
            sequences.arraySize = recipe.Sequences.Length;

            for (int i = 0; i < recipe.Sequences.Length; i++)
            {
                (VisitPhase phase, CustomerState[] states) = recipe.Sequences[i];

                SerializedProperty element = sequences.GetArrayElementAtIndex(i);

                // enum은 intValue가 곧 직렬화된 숫자다. VisitPhase는 번호를 명시한 enum이라 인덱스를 쓰면 어긋난다.
                element.FindPropertyRelative(PhaseField).intValue = (int)phase;

                SerializedProperty stateArray = element.FindPropertyRelative(StatesField);
                stateArray.arraySize = states.Length;

                for (int j = 0; j < states.Length; j++)
                {
                    stateArray.GetArrayElementAtIndex(j).managedReferenceValue = states[j];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            so.Dispose();
        }

        private static PoolItemSO CreatePoolItem(GameObject prefab, string name)
        {
            PoolItemSO poolItem = ScriptableObject.CreateInstance<PoolItemSO>();
            poolItem.poolingName = name;
            poolItem.prefab = prefab;
            poolItem.initCount = 5;

            AssetDatabase.CreateAsset(poolItem, AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{name} Pool Item.asset"));

            // 템플릿에서 물려받은 풀 항목은 남의 설정이 아니라 템플릿의 잔재다.
            // 그대로 두면 새 손님이 전부 템플릿의 풀로 반납되어 종류가 뒤섞인다.
            if (prefab.TryGetComponent(out AbstractCustomer customer))
            {
                customer.PoolItem = poolItem;
                EditorUtility.SetDirty(customer);
            }

            return poolItem;
        }

        private static CustomerDataSO CreateData(Recipe recipe, PoolItemSO poolItem)
        {
            CustomerDataSO data = ScriptableObject.CreateInstance<CustomerDataSO>();

            SerializedObject so = new(data);
            so.FindProperty("poolItem").objectReferenceValue = poolItem;
            so.FindProperty("<customerType>k__BackingField").intValue = (int)recipe.Type;
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Dispose();

            AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{recipe.Name}.asset"));

            return data;
        }

        /// <summary>풀 매니저의 목록에 넣는다. 여기 없으면 InitializePool이 풀을 만들지 않아 Pop이 아무것도 돌려주지 않는다.</summary>
        private static void RegisterInPoolManager(PoolManagerSO poolManager, PoolItemSO poolItem)
        {
            if (poolManager == null || poolItem == null || poolManager.itemList.Contains(poolItem))
            {
                return;
            }

            poolManager.itemList.Add(poolItem);
            EditorUtility.SetDirty(poolManager);
        }

        /// <summary>템플릿이 속한 풀 매니저를 찾는다. 프로젝트에 하나뿐이면 그걸 쓰고, 여럿이면 템플릿을 이미 들고 있는 쪽을 고른다.</summary>
        private static PoolManagerSO FindPoolManager(GameObject template)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(PoolManagerSO)}");

            PoolManagerSO first = null;

            for (int i = 0; i < guids.Length; i++)
            {
                PoolManagerSO manager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (manager == null)
                {
                    continue;
                }

                first ??= manager;

                for (int j = 0; j < manager.itemList.Count; j++)
                {
                    if (manager.itemList[j] != null && manager.itemList[j].prefab == template)
                    {
                        return manager;
                    }
                }
            }

            if (first == null)
            {
                Debug.LogWarning("[손님 양산] 풀 매니저를 찾지 못해 만든 풀 항목이 등록되지 않았습니다. " +
                                 "등록하지 않으면 런타임에 Pop이 손님을 꺼내지 못합니다.");
            }

            return first;
        }

        private static T ReadObject<T>(Object target, string fieldName) where T : Object
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(fieldName);
            T value = property?.objectReferenceValue as T;

            so.Dispose();

            return value;
        }

        // ── 상태 만들기 ─────────────────────────────────────────────
        // 상태의 설정값은 전부 private 필드다. 인스펙터에서 손으로 넣는 값을 코드로 넣어야 하므로
        // 에디터 전용인 여기서만 리플렉션으로 채운다. 런타임 쪽에 setter를 열면 프리팹이 아닌 코드가
        // 손님의 수치를 쥐게 되어, 값을 바꿀 때마다 스크립트를 고쳐야 한다.

        private static T State<T>(params (string Field, object Value)[] fields) where T : CustomerState, new()
        {
            T state = new();

            for (int i = 0; i < fields.Length; i++)
            {
                SetField(state, fields[i].Field, fields[i].Value);
            }

            return state;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (field == null)
            {
                Debug.LogError($"[손님 양산] {target.GetType().Name}에 {name} 필드가 없습니다. 필드 이름이 바뀌었는지 확인하세요.");
                return;
            }

            // 상태 안에 숨은 enum(때릴 대상 고르는 방법 등)은 바깥에서 타입을 쓸 수 없어 이름으로 넘긴다.
            if (field.FieldType.IsEnum && value is string name2)
            {
                field.SetValue(target, Enum.Parse(field.FieldType, name2));
                return;
            }

            field.SetValue(target, value);
        }

        // ── 기획서의 손님 표 ─────────────────────────────────────────

        private static Recipe[] BuildRecipes()
        {
            List<Recipe> recipes = new()
            {
                // 정상 2 — 주유를 기다리지만 이상하게 걷는다. 행동은 정상 손님과 같고 걸음걸이 모듈만 다르다.
                new Recipe
                {
                    Name = "Odd Walk Customer",
                    Type = CustomerType.OddWalker,
                    Human = HumanType.Good,
                    Modules = new[] { typeof(OddGaitModule) },
                    Sequences = new[]
                    {
                        (VisitPhase.Unloading, new CustomerState[] { State<UnboardState>() }),
                        (VisitPhase.Waiting, new CustomerState[]
                        {
                            State<OilingState>(("timeout", 15f)),
                            State<StayState>(("duration", 0f)),
                        }),
                        (VisitPhase.Boarding, new CustomerState[] { State<BoardState>(("timeout", 20f)) }),
                    },
                },

                // 정상 3 — 차고 옆으로 가서 바퀴를 갈아 달라고 한다.
                new Recipe
                {
                    Name = "Tire Change Customer",
                    Type = CustomerType.TireChange,
                    Human = HumanType.Good,
                    Modules = new[] { typeof(CustomerRequestModule) },
                    Sequences = new[]
                    {
                        (VisitPhase.Unloading, new CustomerState[] { State<UnboardState>() }),
                        (VisitPhase.Waiting, new CustomerState[]
                        {
                            State<RequestServiceState>(
                                ("requestType", CustomerRequestType.TireChange),
                                ("requestAt", MapPointType.Garage),
                                ("waitTimeout", 30f)),
                            State<StayState>(("duration", 0f)),
                        }),
                        (VisitPhase.Boarding, new CustomerState[] { State<BoardState>(("timeout", 20f)) }),
                    },
                },

                // 비정상 1 — 입구 도로 옆으로 가 춤을 춘다.
                new Recipe
                {
                    Name = "Dancer Customer",
                    Type = CustomerType.Dancer,
                    Human = HumanType.Bad,
                    Modules = new[] { typeof(ActionAnimatorModule) },
                    Sequences = new[]
                    {
                        (VisitPhase.Unloading, new CustomerState[] { State<UnboardState>() }),
                        (VisitPhase.Waiting, new CustomerState[]
                        {
                            State<MoveToNearestPointState>(("targetPoint", MapPointType.Roadside), ("timeout", 20f)),
                            // 퇴치될 때까지 춘다. 0이면 Phase가 바뀌거나 인터럽트가 들어올 때까지 이어진다.
                            State<PlayAnimationState>(("duration", 0f)),
                        }),
                        (VisitPhase.Boarding, new CustomerState[] { State<BoardState>(("timeout", 20f)) }),
                    },
                },

                // 비정상 3 — 자판기를 때린다.
                new Recipe
                {
                    Name = "Vending Basher Customer",
                    Type = CustomerType.VendingBasher,
                    Human = HumanType.Bad,
                    Modules = new[] { typeof(ActionAnimatorModule) },
                    Sequences = new[]
                    {
                        (VisitPhase.Unloading, new CustomerState[] { State<UnboardState>() }),
                        (VisitPhase.Waiting, new CustomerState[]
                        {
                            State<VandalizeState>(
                                ("source", "MapPoint"),
                                ("targetPoint", MapPointType.VendingMachine),
                                ("hitCount", 6)),
                            State<StayState>(("duration", 0f)),
                        }),
                        (VisitPhase.Boarding, new CustomerState[] { State<BoardState>(("timeout", 20f)) }),
                    },
                },

                // 비정상 4 — 다른 차 앞에 가서 주먹질한다.
                new Recipe
                {
                    Name = "Car Basher Customer",
                    Type = CustomerType.CarBasher,
                    Human = HumanType.Bad,
                    Modules = new[] { typeof(ActionAnimatorModule) },
                    Sequences = new[]
                    {
                        (VisitPhase.Unloading, new CustomerState[] { State<UnboardState>() }),
                        (VisitPhase.Waiting, new CustomerState[]
                        {
                            State<VandalizeState>(
                                ("source", "OtherCar"),
                                ("searchRadius", 30f),
                                ("standoff", 2.5f),
                                ("hitCount", 6)),
                            State<StayState>(("duration", 0f)),
                        }),
                        (VisitPhase.Boarding, new CustomerState[] { State<BoardState>(("timeout", 20f)) }),
                    },
                },

                // 비정상 5 — 카운터로 가서 값을 깎아 달라고 한다.
                new Recipe
                {
                    Name = "Negotiator Customer",
                    Type = CustomerType.Negotiator,
                    Human = HumanType.Bad,
                    Modules = new[] { typeof(CustomerRequestModule) },
                    Sequences = new[]
                    {
                        (VisitPhase.Unloading, new CustomerState[] { State<UnboardState>() }),
                        (VisitPhase.Waiting, new CustomerState[]
                        {
                            State<MoveToNearestPointState>(("targetPoint", MapPointType.Counter), ("timeout", 20f)),
                            State<RequestServiceState>(
                                ("requestType", CustomerRequestType.Negotiation),
                                // 이미 카운터까지 걸어왔다. 여기서 또 자리를 찾으면 같은 곳으로 한 번 더 간다.
                                ("requestAt", MapPointType.None),
                                ("waitTimeout", 30f)),
                            State<StayState>(("duration", 0f)),
                        }),
                        (VisitPhase.Boarding, new CustomerState[] { State<BoardState>(("timeout", 20f)) }),
                    },
                },

                // 차에서 안 내리는 손님 — 입구를 가로로 막는다.
                // 막는 연출 자체는 차가 한다. CarDataSO의 연출 덮어쓰기에 ParkAtMapPointState를 꽂아야 완성된다.
                new Recipe
                {
                    Name = "Entrance Blocker Customer",
                    Type = CustomerType.EntranceBlocker,
                    Human = HumanType.Bad,
                    Sequences = new[]
                    {
                        // Unloading을 비워 두는 것이 '차에서 안 내리는 손님'의 전부다. 코드 분기가 아니다.
                        (VisitPhase.Waiting, new CustomerState[] { State<StayState>(("duration", 0f)) }),
                    },
                },

                // 차에서 안 내리는 손님 — 주유소 안을 뺑뺑 돈다.
                // 도는 연출도 차가 한다. CarDataSO의 연출 덮어쓰기에 CirclingState를 꽂아야 완성된다.
                new Recipe
                {
                    Name = "Circler Customer",
                    Type = CustomerType.Circler,
                    Human = HumanType.Bad,
                    Sequences = new[]
                    {
                        (VisitPhase.Waiting, new CustomerState[] { State<StayState>(("duration", 0f)) }),
                    },
                },
            };

            return recipes.ToArray();
        }
    }
}
