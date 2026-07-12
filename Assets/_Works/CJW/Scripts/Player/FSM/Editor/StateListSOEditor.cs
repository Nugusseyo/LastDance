using System.IO;
using System.Linq;
using _Works.CJW.Scripts.Player.FSM;
using _Works.CJW.Scripts.Player.FSM.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Agents.FSM.Editor
{
    [CustomEditor(typeof(StateListSO))]
    public class StateListSOEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset editorView = default;

        private Button _folderBtn;
        private Button _generateBtn;
        private Label _folderPathLabel;

        private string _folderPath;
        private StateListSO _targetData;
        
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            editorView.CloneTree(root);

            _folderBtn = root.Q<Button>("FolderBtn");
            _generateBtn = root.Q<Button>("GenerateBtn");
            _folderPathLabel = root.Q<Label>("SelectedFolderLabel");
            _folderPathLabel.text = "No folder selected";
            
            _targetData = target as StateListSO;
            _folderBtn.clicked += HandleFolderSelectBtn;
            _generateBtn.clicked += HandleGenerateBtn;

            if (_targetData != null && !string.IsNullOrEmpty(_targetData.generatePath))
            {
                _folderPath = _targetData.generatePath;
                _folderPathLabel.text = FileUtil.GetProjectRelativePath(_targetData.generatePath);
            }
            
            return root;
        }

        private void HandleGenerateBtn()
        {
            if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
            {
                EditorUtility.DisplayDialog("Folder Not Found", "경로 설정이 올바르지 않습니다.", "ok");
                return;
            }

            int index = 0;
            string enumString = string.Join(",", _targetData.states.Select(so =>
            {
                so.assetIndex = index;
                EditorUtility.SetDirty(so);
                return $"{so.stateName} = {index++}";
            })); // so의 스테이트 이름 = 인덱스++을 ,를 기준으로 합쳐야함.
            // 네임스페이스는 경로 기반으로 자동으로 추측되어야함.
            string nameSpace = FileUtil.GetProjectRelativePath(_folderPath).Substring("Assets/".Length);
            if (nameSpace.StartsWith("Scripts/"))
            {
                nameSpace = nameSpace.Substring("Scripts/".Length);
            }

            nameSpace = nameSpace.Replace("/", ".");
            
            string code = string.Format(CodeFormat.EnumFormat, nameSpace, _targetData.enumName, enumString);
            File.WriteAllText($"{_folderPath}/{_targetData.enumName}.cs", code);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(); // 컴파일 새로 함
        }

        private void HandleFolderSelectBtn()
        {
            _folderPath = EditorUtility.OpenFolderPanel("폴더를 선택", _folderPath, "");
            if(!string.IsNullOrEmpty(_folderPath))
            {
                _folderPathLabel.text = FileUtil.GetProjectRelativePath(_folderPath);
                if(_targetData != null)
                {
                    _targetData.generatePath = _folderPath;
                    EditorUtility.SetDirty(_targetData);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}