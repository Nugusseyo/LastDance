using System;
using System.Collections.Generic;
using System.IO;
using DevLib.ObjectPool.Runtime;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _Works.CJW.Scripts.Customers.Data.Editor
{
    /// <summary>손님 한 종류를 통째로 찍어내는 창. 값 입력은 초안 인스턴스 하나에 모아 두고, 만들기를 누를 때 프리팹 → 풀 항목 → CustomerDataSO 순으로 굽고 풀 매니저에 등록까지 한다. 필드를 직접 나열하지 않아 CustomerDataSO에 항목이 늘어도 이 창은 고칠 필요가 없다.</summary>
    public sealed class CustomerDataCreator : EditorWindow
    {
        /// <summary>틀 자산은 경로를 박아두지 않고 이름으로 찾는다. 폴더를 옮겨도 조용히 죽지 않는다.</summary>
        private const string AssetName = "CustomerDataCreator";

        /// <summary>CustomerFSMModule의 직렬화 이름. 이름을 바꾸면 여기도 따라와야 한다.</summary>
        private const string SequencesField = "sequences";
        private const string MapDataField = "mapData";
        private const string PhaseField = "Phase";
        private const string StatesField = "States";

        private const string DefaultFolder = "Assets/_Works/CJW/Data/Customers";
        private const string DefaultPrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";

        /// <summary>도메인 리로드를 넘겨야 하는 값. 초안 인스턴스는 살아남지 못하므로 매번 다시 만든다.</summary>
        [SerializeField] private string assetName = "New Customer";
        [SerializeField] private string folder = DefaultFolder;
        [SerializeField] private string prefabFolder = DefaultPrefabFolder;
        [SerializeField] private PoolManagerSO poolManager;
        [SerializeField] private GameObject customerTemplate;
        [SerializeField] private bool overwriteStates;

        /// <summary>입력을 모아 두는 메모리 전용 인스턴스. 에셋으로 굽기 전까지는 프로젝트에 남지 않는다.</summary>
        private CustomerDataSO _draft;
        private SerializedObject _draftSo;

        /// <summary>행동 상태를 모아 두는 초안. CustomerFSMModule은 MonoBehaviour라 CreateInstance가 안 되므로
        /// 숨은 GameObject에 붙여 직렬화 그릇으로만 쓴다. ModuleOwner가 없으니 Initialize는 아무도 부르지 않는다.</summary>
        private GameObject _draftModuleHost;
        private CustomerFSMModule _draftModule;
        private SerializedObject _moduleSo;

        private TextField _nameField;
        private TextField _folderField;
        private TextField _prefabFolderField;
        private ObjectField _prefabField;
        private ObjectField _templateField;
        private ObjectField _poolManagerField;
        private Toggle _overwriteToggle;
        private VisualElement _issueList;
        private VisualElement _existingList;
        private Button _createButton;

        [MenuItem("Tools/Customer Generator")]
        public static void Open()
        {
            CustomerDataCreator window = GetWindow<CustomerDataCreator>();
            window.titleContent = new GUIContent("손님 만들기");
            // 좌우로 갈라 쓰므로 한 쪽이 찌그러지지 않을 만큼은 있어야 한다.
            window.minSize = new Vector2(720f, 520f);
        }

        private void CreateGUI()
        {
            VisualTreeAsset tree = FindAsset<VisualTreeAsset>(".uxml");
            if (tree == null)
            {
                rootVisualElement.Add(new HelpBox($"{AssetName}.uxml을 찾지 못했습니다.", HelpBoxMessageType.Error));
                return;
            }

            // 스타일시트는 uxml이 GUID로 물고 있다. 여기서 또 붙이면 두 번 적용된다.
            tree.CloneTree(rootVisualElement);

            EnsureDraft();

            rootVisualElement.Q<Label>("title-label").text = "손님 종류 만들기";
            rootVisualElement.Q<Label>("create-title").text = "새 손님";
            rootVisualElement.Q<Label>("pool-title").text = "프리팹 · 풀";
            rootVisualElement.Q<Label>("existing-title").text = "이미 만들어 둔 손님";

            _nameField = rootVisualElement.Q<TextField>("asset-name-field");
            _nameField.label = "에셋 이름";
            _nameField.SetValueWithoutNotify(assetName);
            _nameField.RegisterValueChangedCallback(evt =>
            {
                assetName = evt.newValue;
                RefreshIssues();
            });

            _folderField = rootVisualElement.Q<TextField>("folder-field");
            _folderField.label = "데이터 폴더";
            _folderField.SetValueWithoutNotify(folder);
            _folderField.RegisterValueChangedCallback(evt =>
            {
                folder = evt.newValue;
                RefreshIssues();
            });

            Button folderButton = rootVisualElement.Q<Button>("folder-button");
            folderButton.text = "폴더 고르기";
            folderButton.clicked += PickFolder;

            _prefabFolderField = rootVisualElement.Q<TextField>("prefab-folder-field");
            _prefabFolderField.label = "프리팹 폴더";
            _prefabFolderField.SetValueWithoutNotify(prefabFolder);
            _prefabFolderField.RegisterValueChangedCallback(evt =>
            {
                prefabFolder = evt.newValue;
                RefreshIssues();
            });

            Button prefabFolderButton = rootVisualElement.Q<Button>("prefab-folder-button");
            prefabFolderButton.text = "폴더 고르기";
            prefabFolderButton.clicked += PickPrefabFolder;

            _prefabField = rootVisualElement.Q<ObjectField>("prefab-field");
            _prefabField.label = "손님 프리팹";
            _prefabField.objectType = typeof(GameObject);
            _prefabField.allowSceneObjects = false;
            _prefabField.RegisterValueChangedCallback(_ => RefreshIssues());

            Button poolButton = rootVisualElement.Q<Button>("pool-button");
            poolButton.text = "풀 항목 만들기";
            poolButton.clicked += CreatePoolItemFromPrefab;

            rootVisualElement.Q<Label>("pool-help").text =
                "이미 만들어 둔 프리팹이 있으면 여기 넣는다. 비워두면 아래 템플릿에서 프리팹까지 새로 만든다.";

            _templateField = rootVisualElement.Q<ObjectField>("template-field");
            _templateField.label = "템플릿 프리팹";
            _templateField.objectType = typeof(GameObject);
            _templateField.allowSceneObjects = false;
            _templateField.SetValueWithoutNotify(customerTemplate);
            _templateField.RegisterValueChangedCallback(evt =>
            {
                customerTemplate = evt.newValue as GameObject;
                RefreshIssues();
            });

            rootVisualElement.Q<Label>("template-help").text =
                "손님 프리팹이 비어 있을 때 이 템플릿으로 새 프리팹(배리언트)을 만든다.";

            _poolManagerField = rootVisualElement.Q<ObjectField>("pool-manager-field");
            _poolManagerField.label = "풀 매니저";
            _poolManagerField.objectType = typeof(PoolManagerSO);
            _poolManagerField.allowSceneObjects = false;
            _poolManagerField.SetValueWithoutNotify(poolManager);
            _poolManagerField.RegisterValueChangedCallback(evt =>
            {
                poolManager = evt.newValue as PoolManagerSO;
                RefreshIssues();
            });

            rootVisualElement.Q<Label>("manager-help").text =
                "만든 풀 항목을 여기에 등록한다. 등록하지 않으면 런타임에 Pop이 손님을 꺼내지 못한다.";

            rootVisualElement.Q<Label>("state-title").text = "행동 상태 (State)";

            _overwriteToggle = rootVisualElement.Q<Toggle>("state-overwrite-toggle");
            _overwriteToggle.label = "기존 상태 덮어쓰기";
            _overwriteToggle.SetValueWithoutNotify(overwriteStates);
            _overwriteToggle.RegisterValueChangedCallback(evt =>
            {
                overwriteStates = evt.newValue;
                RefreshIssues();
            });

            rootVisualElement.Q<Label>("state-help").text =
                "여기서 정한 행동이 만들어진 프리팹의 CustomerFSMModule에 그대로 구워진다. "
                + "대상이 이미 시퀀스를 들고 있으면 위 항목을 켰을 때만 덮어쓴다.";

            _issueList = rootVisualElement.Q<VisualElement>("issue-list");
            _existingList = rootVisualElement.Q<VisualElement>("existing-list");

            Button refreshButton = rootVisualElement.Q<Button>("refresh-button");
            refreshButton.text = "새로고침";
            refreshButton.clicked += RefreshExisting;

            _createButton = rootVisualElement.Q<Button>("create-button");
            _createButton.text = "손님 만들기";
            _createButton.clicked += CreateAsset;

            BuildDataFields();
            BuildStateFields();
            RefreshIssues();
            RefreshExisting();
        }

        private void OnDisable()
        {
            // DontSave라 씬·에셋에는 안 남지만, 창을 닫을 때 직접 치우지 않으면 메모리에 계속 떠 있다.
            if (_draft != null)
            {
                DestroyImmediate(_draft);
                _draft = null;
            }

            _draftSo?.Dispose();
            _draftSo = null;

            // 숨겨 뒀다고 저절로 사라지지 않는다. 안 치우면 창을 열 때마다 씬에 하나씩 쌓인다.
            if (_draftModuleHost != null)
            {
                DestroyImmediate(_draftModuleHost);
                _draftModuleHost = null;
                _draftModule = null;
            }

            _moduleSo?.Dispose();
            _moduleSo = null;
        }

        private void EnsureDraft()
        {
            if (_draft != null)
            {
                return;
            }

            _draft = CreateInstance<CustomerDataSO>();
            _draft.hideFlags = HideFlags.DontSave;
            _draftSo = new SerializedObject(_draft);
        }

        private void EnsureDraftModule()
        {
            if (_draftModule != null)
            {
                return;
            }

            _draftModuleHost = new GameObject("__CustomerDataCreatorDraft")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _draftModule = _draftModuleHost.AddComponent<CustomerFSMModule>();
            _moduleSo = new SerializedObject(_draftModule);
        }

        /// <summary>CustomerDataSO의 필드를 하나씩 나열하지 않고 그대로 그린다. 항목이 늘거나 이름이 바뀌어도 여기는 그대로다.</summary>
        private void BuildDataFields()
        {
            VisualElement container = rootVisualElement.Q<VisualElement>("data-fields");
            container.Clear();

            SerializedProperty property = _draftSo.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                // 초안은 에셋이 아니라 스크립트 칸을 보여줄 이유가 없다.
                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                PropertyField field = new(property.Copy());
                field.RegisterValueChangeCallback(_ => RefreshIssues());
                container.Add(field);
            }

            container.Bind(_draftSo);
        }

        /// <summary>CustomerFSMModule의 필드를 그대로 그린다. CustomerStatePickerDrawer가 자동으로 붙어 종류 드롭다운까지 따라온다.</summary>
        private void BuildStateFields()
        {
            EnsureDraftModule();

            VisualElement container = rootVisualElement.Q<VisualElement>("state-fields");
            container.Clear();

            SerializedProperty property = _moduleSo.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (IsInternalProperty(property.propertyPath))
                {
                    continue;
                }

                PropertyField field = new(property.Copy());
                field.RegisterValueChangeCallback(_ => RefreshIssues());
                container.Add(field);
            }

            container.Bind(_moduleSo);
        }

        /// <summary>MonoBehaviour가 늘 달고 다니는 칸. 초안에서는 보여 줄 것도, 옮길 것도 없다.</summary>
        private static bool IsInternalProperty(string propertyPath)
        {
            return propertyPath is "m_Script" or "m_Enabled" or "m_GameObject" or "m_ObjectHideFlags" or "m_Name"
                or "m_EditorHideFlags" or "m_EditorClassIdentifier" or "m_CorrespondingSourceObject"
                or "m_PrefabInstance" or "m_PrefabAsset";
        }

        private void PickFolder()
        {
            if (TryPickFolder("손님 데이터를 저장할 폴더", folder, out string picked))
            {
                folder = picked;
                _folderField.SetValueWithoutNotify(folder);
                RefreshIssues();
            }
        }

        private void PickPrefabFolder()
        {
            if (TryPickFolder("손님 프리팹을 저장할 폴더", prefabFolder, out string picked))
            {
                prefabFolder = picked;
                _prefabFolderField.SetValueWithoutNotify(prefabFolder);
                RefreshIssues();
            }
        }

        private static bool TryPickFolder(string title, string current, out string relative)
        {
            relative = null;

            string start = AssetDatabase.IsValidFolder(current) ? current : "Assets";
            string picked = EditorUtility.OpenFolderPanel(title, start, string.Empty);

            if (string.IsNullOrEmpty(picked))
            {
                return false;
            }

            relative = ToProjectRelativePath(picked);
            if (relative != null)
            {
                return true;
            }

            EditorUtility.DisplayDialog("손님 만들기", "이 프로젝트의 Assets 폴더 안을 골라야 합니다.", "확인");
            return false;
        }

        /// <summary>절대 경로를 Assets/... 형태로 바꾼다. 프로젝트 밖이면 null.</summary>
        private static string ToProjectRelativePath(string absolute)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
            string normalized = absolute.Replace('\\', '/');

            if (projectRoot == null || !normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string relative = normalized[(projectRoot.Length + 1)..];

            return relative.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? relative : null;
        }

        private SerializedProperty PoolItemProperty => _draftSo.FindProperty("poolItem");

        /// <summary>이미 있는 프리팹으로 풀 항목만 따로 만든다. 데이터를 굽지 않고 풀만 먼저 준비할 때 쓴다.</summary>
        private void CreatePoolItemFromPrefab()
        {
            GameObject prefab = _prefabField.value as GameObject;
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("손님 만들기", "먼저 손님 프리팹을 넣어야 합니다.", "확인");
                return;
            }

            if (prefab.GetComponent<AbstractCustomer>() == null)
            {
                EditorUtility.DisplayDialog("손님 만들기",
                    $"{prefab.name}에 AbstractCustomer가 없습니다. 손님 프리팹이 맞는지 확인하세요.", "확인");
                return;
            }

            PoolItemSO poolItem = CreatePoolItem(prefab, prefab.name);
            if (poolItem == null)
            {
                return;
            }

            _draftSo.Update();
            PoolItemProperty.objectReferenceValue = poolItem;
            _draftSo.ApplyModifiedPropertiesWithoutUndo();

            RegisterInPoolManager(poolItem);

            AssetDatabase.SaveAssets();
            RefreshIssues();
            EditorGUIUtility.PingObject(poolItem);
        }

        /// <summary>풀 항목 에셋을 만들고 프리팹의 PoolItem 칸까지 채워 준다. 이걸 빼먹으면 Push가 조용히 실패해 손님이 회수되지 않는다.</summary>
        /// <param name="overwriteBinding">템플릿에서 갓 구운 배리언트면 true. 물려받은 값은 템플릿의 잔재라 반드시 덮어야 한다.</param>
        private PoolItemSO CreatePoolItem(GameObject prefab, string itemName, bool overwriteBinding = false)
        {
            if (!EnsureFolder(folder))
            {
                return null;
            }

            PoolItemSO poolItem = CreateInstance<PoolItemSO>();
            poolItem.poolingName = itemName;
            poolItem.prefab = prefab;
            poolItem.initCount = 5;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{itemName} Pool Item.asset");
            AssetDatabase.CreateAsset(poolItem, path);

            BindPoolItemToPrefab(prefab, poolItem, overwriteBinding);

            return poolItem;
        }

        /// <summary>프리팹의 AbstractCustomer.PoolItem을 채운다. 런타임 Push는 이 값으로 어느 풀에 돌려줄지 찾는다.</summary>
        private static void BindPoolItemToPrefab(GameObject prefab, PoolItemSO poolItem, bool overwrite)
        {
            if (!prefab.TryGetComponent(out AbstractCustomer customer))
            {
                return;
            }

            if (customer.PoolItem == poolItem)
            {
                return;
            }

            // 사용자가 직접 넣은 프리팹이면 이미 물려 있는 설정을 덮지 않는다.
            // 반면 템플릿에서 갓 구운 배리언트가 들고 있는 값은 남의 설정이 아니라 템플릿의 잔재다.
            // 그걸 그대로 두면 새 손님이 전부 템플릿의 풀로 반납되어 종류가 뒤섞인다.
            if (!overwrite && customer.PoolItem != null)
            {
                return;
            }

            customer.PoolItem = poolItem;
            EditorUtility.SetDirty(customer);
        }

        /// <summary>초안의 행동 상태를 프리팹 루트의 CustomerFSMModule로 옮긴다. 실제로 옮겼으면 true.</summary>
        private bool ApplyStatesTo(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            // ModuleOwner가 GetComponentsInChildren로 모듈을 모으므로 자식에 붙어 있을 수 있다.
            CustomerFSMModule target = root.GetComponentInChildren<CustomerFSMModule>(true);

            if (target == null)
            {
                target = root.AddComponent<CustomerFSMModule>();
                Debug.LogWarning($"[손님 만들기] {root.name}에 CustomerFSMModule이 없어 루트에 새로 붙였습니다.", root);
            }

            if (!overwriteStates && HasStates(target))
            {
                Debug.LogWarning($"[손님 만들기] {root.name}에 이미 행동 상태가 있어 그대로 두었습니다. "
                                 + "바꾸려면 '기존 상태 덮어쓰기'를 켜세요.", root);
                return false;
            }

            _moduleSo.Update();

            SerializedObject targetSo = new(target);
            SerializedProperty property = _moduleSo.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (IsInternalProperty(property.propertyPath))
                {
                    continue;
                }

                SerializedPropertyCopier.Copy(property, targetSo.FindProperty(property.propertyPath));
            }

            targetSo.ApplyModifiedPropertiesWithoutUndo();
            targetSo.Dispose();
            EditorUtility.SetDirty(target);

            return true;
        }

        /// <summary>이미 있는 프리팹 에셋을 열어 상태를 심고 되굽는다.</summary>
        private void BakeStatesIntoPrefabAsset(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[손님 만들기] {prefab.name}은 프리팹 에셋이 아니라 행동 상태를 심지 못했습니다.", prefab);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            try
            {
                // 아무것도 안 바꿨는데 되구우면 파일만 괜히 건드린다.
                if (ApplyStatesTo(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool HasStates(CustomerFSMModule module)
        {
            SerializedObject so = new(module);
            SerializedProperty sequences = so.FindProperty(SequencesField);
            bool has = sequences != null && sequences.arraySize > 0;

            so.Dispose();

            return has;
        }

        /// <summary>상태를 구울 대상 프리팹. 풀 항목 → 지정 프리팹 → 템플릿 순으로 본다.</summary>
        private GameObject ResolveStateTarget()
        {
            if (PoolItemProperty?.objectReferenceValue is PoolItemSO item && item.prefab != null)
            {
                return item.prefab;
            }

            return _prefabField?.value as GameObject ?? customerTemplate;
        }

        /// <summary>템플릿으로 새 손님 프리팹을 만든다. 씬에 잠깐 올렸다가 반드시 치운다.</summary>
        private GameObject CreateCustomerPrefab(string prefabName)
        {
            if (customerTemplate == null)
            {
                return null;
            }

            if (!EnsureFolder(prefabFolder))
            {
                return null;
            }

            // InstantiatePrefab으로 올린 인스턴스를 저장하면 템플릿의 배리언트가 된다.
            // 템플릿을 고치면 손님들이 함께 따라오라는 뜻이다. 독립된 사본을 원하면 Instantiate를 쓴다.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(customerTemplate);
            if (instance == null)
            {
                Debug.LogError($"[손님 만들기] {customerTemplate.name}을(를) 인스턴스화하지 못했습니다.", customerTemplate);
                return null;
            }

            try
            {
                instance.name = prefabName;

                // 저장 전에 심어야 오버라이드가 배리언트 고유값으로 굳는다. 저장 뒤에 심으면 한 번 더 구워야 한다.
                ApplyStatesTo(instance);

                // CreateAsset은 GameObject를 받지 않는다. 프리팹은 반드시 이 API로 굽는다.
                string path = AssetDatabase.GenerateUniqueAssetPath($"{prefabFolder}/{prefabName}.prefab");
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);

                if (!success || saved == null)
                {
                    Debug.LogError($"[손님 만들기] {path} 에 프리팹을 저장하지 못했습니다.");
                    return null;
                }

                return saved;
            }
            finally
            {
                // 안 치우면 손님을 만들 때마다 씬에 하나씩 쌓인다.
                DestroyImmediate(instance);
            }
        }

        /// <summary>풀 매니저의 목록에 넣는다. 여기 없으면 InitializePool이 풀을 만들지 않아 Pop이 아무것도 돌려주지 않는다.</summary>
        private void RegisterInPoolManager(PoolItemSO poolItem)
        {
            if (poolManager == null || poolItem == null || poolManager.itemList.Contains(poolItem))
            {
                return;
            }

            Undo.RecordObject(poolManager, "Register Pool Item");
            poolManager.itemList.Add(poolItem);
            EditorUtility.SetDirty(poolManager);
        }

        private static bool EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("손님 만들기", $"저장 폴더는 Assets 아래여야 합니다: {path}", "확인");
                return false;
            }

            // 중간 폴더가 없으면 AssetDatabase.CreateAsset이 조용히 실패한다. 한 단계씩 만들어 둔다.
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            AssetDatabase.Refresh();

            return AssetDatabase.IsValidFolder(path);
        }

        private void CreateAsset()
        {
            if (!EnsureFolder(folder))
            {
                return;
            }

            _draftSo.ApplyModifiedPropertiesWithoutUndo();
            _moduleSo.ApplyModifiedPropertiesWithoutUndo();

            string fileName = string.IsNullOrWhiteSpace(assetName) ? "New Customer" : assetName.Trim();

            // 풀 항목을 직접 지정했으면 그걸 존중한다. 비어 있을 때만 프리팹부터 만들어 채운다.
            PoolItemSO poolItem = PoolItemProperty.objectReferenceValue as PoolItemSO;
            GameObject prefab = poolItem != null ? poolItem.prefab : _prefabField.value as GameObject;

            // 갓 만든 프리팹인지 기억해 둔다. 템플릿에서 물려받은 풀 항목을 덮어도 되는지가 여기서 갈린다.
            bool freshPrefab = false;

            if (prefab != null)
            {
                // 이미 있는 프리팹은 에셋을 열어 심고 되굽는다.
                BakeStatesIntoPrefabAsset(prefab);
            }
            else
            {
                // 새로 만드는 쪽은 CreateCustomerPrefab이 저장 직전에 심는다.
                prefab = CreateCustomerPrefab(fileName);
                freshPrefab = prefab != null;
            }

            if (poolItem == null && prefab != null)
            {
                poolItem = CreatePoolItem(prefab, fileName, freshPrefab);

                _draftSo.Update();
                PoolItemProperty.objectReferenceValue = poolItem;
                _draftSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 초안을 그대로 구우면 창이 계속 그 에셋을 편집하게 된다. 복사본을 만들어 넘긴다.
            CustomerDataSO asset = Instantiate(_draft);
            asset.hideFlags = HideFlags.None;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            AssetDatabase.CreateAsset(asset, path);

            RegisterInPoolManager(poolItem);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;

            RefreshExisting();
            RefreshIssues();

            Debug.Log($"[손님 만들기] {path} 를 만들었습니다.", asset);
        }

        private void RefreshIssues()
        {
            if (_issueList == null)
            {
                return;
            }

            _issueList.Clear();
            _draftSo.Update();

            bool blocked = false;

            if (string.IsNullOrWhiteSpace(assetName))
            {
                AddIssue("에셋 이름이 비어 있습니다.", true);
                blocked = true;
            }

            blocked |= CheckFolder(folder, "데이터 폴더");

            PoolItemSO item = PoolItemProperty?.objectReferenceValue as PoolItemSO;
            GameObject prefab = _prefabField?.value as GameObject;

            if (item != null)
            {
                // 이미 정해진 풀 항목이 있으면 프리팹·템플릿은 쓰이지 않는다.
                if (item.prefab == null)
                {
                    AddIssue($"{item.name}에 프리팹이 없습니다.", true);
                    blocked = true;
                }
                else if (item.prefab.GetComponent<AbstractCustomer>() == null)
                {
                    AddIssue($"{item.prefab.name}에 AbstractCustomer가 없습니다.", true);
                    blocked = true;
                }
            }
            else if (prefab != null)
            {
                if (prefab.GetComponent<AbstractCustomer>() == null)
                {
                    AddIssue($"{prefab.name}에 AbstractCustomer가 없습니다. 손님 프리팹이 맞는지 확인하세요.", true);
                    blocked = true;
                }
            }
            else if (customerTemplate != null)
            {
                blocked |= CheckFolder(prefabFolder, "프리팹 폴더");

                if (customerTemplate.GetComponent<AbstractCustomer>() == null)
                {
                    AddIssue($"{customerTemplate.name}에 AbstractCustomer가 없습니다. 손님 템플릿이 맞는지 확인하세요.", true);
                    blocked = true;
                }
            }
            else
            {
                AddIssue("프리팹도 템플릿도 없어 풀 항목 없이 데이터만 만들어집니다. "
                         + "이대로 두면 VisitDirector가 이 손님을 꺼내지 못합니다.", false);
            }

            if (poolManager == null)
            {
                AddIssue("풀 매니저가 비어 있습니다. 만든 풀 항목이 등록되지 않아 런타임에 Pop이 실패합니다.", false);
            }

            SerializedProperty weight = _draftSo.FindProperty("spawnWeight");
            if (weight != null && weight.floatValue <= 0f)
            {
                AddIssue("스폰 가중치가 0이라 이 손님은 뽑히지 않습니다.", false);
            }

            blocked |= CheckStates();

            _createButton?.SetEnabled(!blocked);
        }

        /// <summary>행동 상태 쪽 문제를 훑는다. 막아야 하면 true를 돌려준다.</summary>
        private bool CheckStates()
        {
            if (_moduleSo == null)
            {
                return false;
            }

            _moduleSo.Update();

            SerializedProperty sequences = _moduleSo.FindProperty(SequencesField);

            if (sequences == null || sequences.arraySize == 0)
            {
                // 행동을 아예 안 정했으면 지금까지처럼 프리팹에서 직접 꽂겠다는 뜻이다.
                return false;
            }

            bool blocked = false;
            HashSet<VisitPhase> seenPhases = new();
            HashSet<string> stateTypes = new();
            bool hasBoarding = false;

            for (int i = 0; i < sequences.arraySize; i++)
            {
                SerializedProperty element = sequences.GetArrayElementAtIndex(i);
                SerializedProperty phase = element.FindPropertyRelative(PhaseField);
                SerializedProperty states = element.FindPropertyRelative(StatesField);

                if (phase == null || states == null)
                {
                    continue;
                }

                VisitPhase value = (VisitPhase)phase.intValue;

                if (!seenPhases.Add(value))
                {
                    AddIssue($"{value} 단계가 두 번 등록되어 있습니다. 나중 것이 앞의 시퀀스를 통째로 지웁니다.", true);
                    blocked = true;
                }

                if (value == VisitPhase.Boarding && states.arraySize > 0)
                {
                    hasBoarding = true;
                }

                for (int j = 0; j < states.arraySize; j++)
                {
                    string typeName = states.GetArrayElementAtIndex(j).managedReferenceFullTypename;

                    if (string.IsNullOrEmpty(typeName))
                    {
                        AddIssue($"{value} 단계 {j + 1}번째 칸의 종류가 비어 있습니다. 런타임에는 그냥 넘어갑니다.", false);
                        continue;
                    }

                    stateTypes.Add(typeName);
                }
            }

            SerializedProperty mapData = _moduleSo.FindProperty(MapDataField);

            if (mapData != null && mapData.objectReferenceValue == null && NeedsMapData(stateTypes))
            {
                AddIssue($"{nameof(MoveToNearestPointState)}·{nameof(OilingState)}는 MapData가 있어야 목적지를 찾습니다. "
                         + "행동 상태의 MapData 칸을 채우세요.", true);
                blocked = true;
            }

            if (!hasBoarding)
            {
                AddIssue("Boarding 단계 시퀀스가 없어 손님이 차로 돌아오지 않습니다.", false);
            }

            GameObject target = ResolveStateTarget();

            if (target != null)
            {
                CustomerFSMModule module = target.GetComponentInChildren<CustomerFSMModule>(true);

                if (module == null)
                {
                    AddIssue($"{target.name}에 CustomerFSMModule이 없습니다. 만들 때 루트에 새로 붙입니다.", false);
                }
                else if (!overwriteStates && HasStates(module))
                {
                    AddIssue($"{target.name}에 이미 행동 상태가 있어 여기 설정은 무시됩니다. "
                             + "바꾸려면 '기존 상태 덮어쓰기'를 켜세요.", false);
                }
            }

            return blocked;
        }

        /// <summary>MapData 없이는 목적지를 못 찾는 상태가 섞여 있는지.</summary>
        private static bool NeedsMapData(HashSet<string> typeNames)
        {
            foreach (string typeName in typeNames)
            {
                if (typeName.EndsWith(nameof(MoveToNearestPointState), StringComparison.Ordinal) ||
                    typeName.EndsWith(nameof(OilingState), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>폴더 경로를 검사하고 막아야 하면 true를 돌려준다.</summary>
        private bool CheckFolder(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue($"{label}는 Assets 아래여야 합니다.", true);
                return true;
            }

            if (!AssetDatabase.IsValidFolder(path))
            {
                AddIssue($"{path} 폴더가 아직 없습니다. 만들 때 함께 생성됩니다.", false);
            }

            return false;
        }

        private void AddIssue(string message, bool isError)
        {
            Label label = new(message);
            label.AddToClassList("cd-issue");
            label.AddToClassList(isError ? "cd-issue--error" : "cd-issue--warning");

            _issueList.Add(label);
        }

        private void RefreshExisting()
        {
            if (_existingList == null)
            {
                return;
            }

            _existingList.Clear();

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CustomerDataSO)}");

            if (guids.Length == 0)
            {
                Label empty = new("아직 만들어 둔 손님이 없습니다.");
                empty.AddToClassList("cd-empty");
                _existingList.Add(empty);
                return;
            }

            List<CustomerDataSO> assets = new(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CustomerDataSO asset = AssetDatabase.LoadAssetAtPath<CustomerDataSO>(path);

                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            assets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < assets.Count; i++)
            {
                _existingList.Add(BuildExistingRow(assets[i], poolManager));
            }
        }

        private static VisualElement BuildExistingRow(CustomerDataSO asset, PoolManagerSO manager)
        {
            VisualElement row = new();
            row.AddToClassList("cd-existing-row");

            Label name = new(asset.name);
            name.AddToClassList("cd-existing-row__name");
            row.Add(name);

            Label meta = new($"{asset.customerType} · 가중치 {asset.SpawnWeight:0.##}");
            meta.AddToClassList("cd-existing-row__meta");
            row.Add(meta);

            if (asset.PoolItem == null || asset.PoolItem.prefab == null)
            {
                AddBadge(row, "풀 없음");
            }
            else if (manager != null && !manager.itemList.Contains(asset.PoolItem))
            {
                AddBadge(row, "미등록");
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                EditorGUIUtility.PingObject(asset);

                if (evt.clickCount > 1)
                {
                    Selection.activeObject = asset;
                }
            });

            return row;
        }

        private static void AddBadge(VisualElement row, string text)
        {
            Label badge = new(text);
            badge.AddToClassList("cd-badge");
            badge.AddToClassList("cd-badge--missing");
            row.Add(badge);
        }

        /// <summary>이름과 확장자로 틀 자산을 찾는다. Editor 폴더를 옮겨도 따라온다.</summary>
        private static T FindAsset<T>(string extension) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"{AssetName} t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(AssetName + extension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }
    }
}
