using DevLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DevLib.ObjectPool.Editor
{
    [CustomEditor(typeof(PoolItemSO))]
    public class PoolItemSOEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset editorView = default;
        
        private TextField _nameField;
        private Button _changeButton;
        private ObjectField _prefabField;
        
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            editorView.CloneTree(root); //클론을 먼저하고 그다음에 기본을 채워야. 이름 필드가 가장위에 올라온다.
            // InspectorElement.FillDefaultInspector(root, serializedObject, this);
            
            _nameField = root.Q<TextField>("PoolingName");
            _changeButton = root.Q<Button>("ChangeBtn");
            _prefabField = root.Q<ObjectField>("PrefabField");

            _changeButton.clicked += HandleChangeButtonClick;
            _nameField.RegisterCallback<KeyDownEvent>(HandleKeydownEvent);
            _prefabField.RegisterValueChangedCallback(HandlePrefabChangeEvent);
            
            return root;
        }

        private void HandlePrefabChangeEvent(ChangeEvent<Object> evt)
        {
            if (evt.newValue == null) return;
            
            GameObject newObject = evt.newValue as GameObject; 
            Debug.Assert(newObject != null, "새롭게 할당된 프리팹은 게임오브젝트가 아닙니다.");

            PoolItemSO item = target as PoolItemSO;

            if (!newObject.TryGetComponent(out IPoolable poolable))
            {
                item.prefab = null;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Error", "Poolable 콤포넌트를 찾을 수 없습니다.", "OK");
                return;
            }

            poolable.PoolItem = item; //이거 셋터 넣어야 한다.
            EditorUtility.SetDirty(newObject);
            AssetDatabase.SaveAssetIfDirty(newObject);

        }

        private void HandleChangeButtonClick()
        {
            string newName = _nameField.text;
            if (string.IsNullOrEmpty(newName))
            {
                EditorUtility.DisplayDialog("Error", "Name is empty", "OK");
                return;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(target);
            
            //이 함수는 성공적으로 변환시에는 null을 리턴하고, 그렇지 않으면 에러내용을 리턴한다.
            string message = AssetDatabase.RenameAsset(assetPath, newName);
            if (string.IsNullOrEmpty(message))
            {
                target.name = newName;
            }
            else
            {
                EditorUtility.DisplayDialog("Error", message, "OK");
            }
        }

        private void HandleKeydownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return)
            {
                HandleChangeButtonClick();
            }
        }
    }
}