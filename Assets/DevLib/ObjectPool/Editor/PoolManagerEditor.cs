using System;
using System.Collections.Generic;
using System.IO;
using DevLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DevLib.ObjectPool.Editor
{
    public class PoolManagerEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset editorView = default;
        [SerializeField] private PoolManagerSO poolManager = default;
        [SerializeField] private VisualTreeAsset itemAsset = default;

        private string _rootFolder = "Assets/ObjectPool";

        private Button _createBtn;
        private ScrollView _itemView;

        private List<PoolItemView> _itemList;
        private PoolItemView _selectedItem; //현재 선택된 아이템

        private UnityEditor.Editor _cachedEditor; //재활용할 캐싱 에디터
        private VisualElement _inspector;
        

        [MenuItem("Tools/PoolManager")]
        public static void OpenWindow()
        {
            PoolManagerEditor wnd = GetWindow<PoolManagerEditor>();
            wnd.titleContent = new GUIContent("PoolManagerEditor");
        }

        private string GetCurrentDirectory()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            return System.IO.Path.GetDirectoryName(scriptPath);
        }

        private void InitializeRootFolder()
        {
            string dirName = GetCurrentDirectory();
            DirectoryInfo parentDirectory = Directory.GetParent(dirName); //현재 디렉토리의 부모를 가져오고
            Debug.Assert(parentDirectory != null, $"Parent directory is null! Check path : {dirName}");

            string dataPath = Application.dataPath;
            _rootFolder = parentDirectory.FullName.Replace('\\', '/'); //역슬래시를 슬래시로 변경해준다.
            if (_rootFolder.StartsWith(dataPath))
            {
                _rootFolder = "Assets" + _rootFolder.Substring(dataPath.Length); //앞쪽에서 절대경로 제거한다.
            }
        }
        
        public void CreateGUI()
        {
            InitializeRootFolder();
            
            VisualElement root = rootVisualElement;

            if (editorView == null)
            {
                string dirName = GetCurrentDirectory();
                editorView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{dirName}/PoolManagerEditor.uxml");
            }
            editorView.CloneTree(root);

            InitializeItems(root);
            GeneratePoolingItemUI();
        }

        private void InitializeItems(VisualElement root)
        {
            _createBtn = root.Q<Button>("CreateBtn");
            _createBtn.clicked += HandleCreateItem;
            _itemView = root.Q<ScrollView>("ItemView");
            
            _itemList = new List<PoolItemView>();
            _inspector = root.Q<VisualElement>("InspectorView");
        }

        private void GeneratePoolingItemUI()
        {
            _itemView.Clear();
            _itemList.Clear();
            _inspector.Clear();

            if (poolManager == null) //풀매니저가 없다면 만들어서 넣어준다.
            {
                string poolManagerFilePath = $"{_rootFolder}/PoolManager.asset";
                poolManager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(poolManagerFilePath);
                if (poolManager == null)
                {
                    Debug.LogWarning("풀매니저 데이터가 존재하지 않습니다. 새것을 만듭니다.");
                    poolManager = ScriptableObject.CreateInstance<PoolManagerSO>(); //새것으로 마들어서 할당.
                    AssetDatabase.CreateAsset(poolManager, poolManagerFilePath); //새로만든 SO를 파일로 저장.
                }
            }

            if (itemAsset == null)
            {
                string dirName = GetCurrentDirectory();
                itemAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{dirName}/PoolItemView.uxml");
            }

            foreach (PoolItemSO item in poolManager.itemList)
            {
                TemplateContainer itemUI = itemAsset.Instantiate(); //이걸해야 스타일까지 같이 들어간다.
                PoolItemView itemView = new PoolItemView(itemUI, item);
                
                _itemView.Add(itemUI); //스크롤뷰에 추가한다.
                _itemList.Add(itemView); //리스트에도 넣어서 차후 관리할 수 있도록 만들어준다.

                itemView.Name = item.name; //우리가 설정했던 프로퍼티 셋터를 이용해서 UI를 깔끔하게 셋팅할 수 있다.
                itemView.IsEmpty = item.prefab == null; //아이템의 프리팹을 설정하지 않았다면 워닝 라벨이 뜰 수 있도록
                itemView.IsActive = false;

                itemView.OnSelectEvent += HandleSelectEvent;
                itemView.OnDeleteEvent += HandleDeleteEvent;
            }
        }

        private void HandleSelectEvent(PoolItemView targetView)
        {
            if (_selectedItem != null)
                _selectedItem.IsActive = false;
            _selectedItem = targetView;
            _selectedItem.IsActive = true;
            
            _inspector.Clear();
            //_cachedEditor에 이미 에디터가 생성되어 있다면 그것을 재활용하고, 없다면 새로 만들어준다.
            UnityEditor.Editor.CreateCachedEditor(_selectedItem.TargetItem, null, ref _cachedEditor);
            VisualElement inspectorElement = _cachedEditor.CreateInspectorGUI(); 
            //해당 아이템을 클릭했을 때 나오는 인스펙터를 자동으로 만들어준다.

            SerializedObject serializedObject = new SerializedObject(_selectedItem.TargetItem); //선택된 SO를 직렬화하고
            inspectorElement.Bind(serializedObject);
            
            //여기에 추가적으로 변경시 추적할 수 있는 방법이 필요하다. (요거는 다음에 한다)
            
            inspectorElement.TrackSerializedObjectValue(serializedObject, so =>
            {
                _selectedItem.Name = so.FindProperty("poolingName").stringValue;
                _selectedItem.IsEmpty = so.FindProperty("prefab").objectReferenceValue == null;
            });
            
            _inspector.Add(inspectorElement);
        }

        private void HandleDeleteEvent(PoolItemView targetView)
        {
            poolManager.itemList.Remove(targetView.TargetItem);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(targetView.TargetItem));
            EditorUtility.SetDirty(poolManager);
            
            AssetDatabase.SaveAssets();

            if (targetView == _selectedItem)
            {
                _selectedItem = null;
                //인스펙터도 클리어해야하지만 아직 인스펙터를 만들지 않았기 때문에 여기는 비워둔다.
            }
            GeneratePoolingItemUI(); //지워졌으니 갱신.
        }

        private void HandleCreateItem()
        {
            Guid itemGuid = Guid.NewGuid(); //새로운 인스턴스 아이디를 가져온다. 
            PoolItemSO newItem = ScriptableObject.CreateInstance<PoolItemSO>();
            newItem.poolingName = itemGuid.ToString();

            //아이템들을 저장하는 폴더가 없다면 만들어서 넣어준다.
            if (Directory.Exists($"{_rootFolder}/Items") == false)
            {
                Directory.CreateDirectory($"{_rootFolder}/Items");
            }
            
            AssetDatabase.CreateAsset(newItem, $"{_rootFolder}/Items/{newItem.poolingName}.asset"); //SO파일 만들어준다.
            poolManager.itemList.Add(newItem);
            
            EditorUtility.SetDirty(poolManager);
            AssetDatabase.SaveAssets();
            
            GeneratePoolingItemUI(); //추가된 것에 따라 UI새로 생성
        }
    }
}
