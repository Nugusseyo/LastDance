using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Works.CJW.Scripts.Player.FSM.Editor
{
    [CustomEditor(typeof(StateMachineSO))]
    public class StateMachineSOEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset editorView = default;

        private StateMachineSO _targetData;

        public override VisualElement CreateInspectorGUI()
        {
            _targetData = target as StateMachineSO;

            VisualElement root = new();
            editorView.CloneTree(root);

            FillDropdownField(root);

            return root;
        }

        private void FillDropdownField(VisualElement root)
        {
            DropdownField field = root.Q<DropdownField>("ClassNameDropdown");

            Assembly machineAssembly = Assembly.GetAssembly(typeof(StateMachine));
            IEnumerable<string> choices = machineAssembly.GetTypes()
                .Where(type => !type.IsAbstract &&
                               type.IsClass &&
                               type.IsSubclassOf(typeof(StateMachine)))
                .Select(type => type.FullName);

            field.choices.AddRange(choices);

            if (_targetData != null && !string.IsNullOrEmpty(_targetData.className)
                                    && field.choices.Contains(_targetData.className))
            {
                field.value = _targetData.className;
            }
            else if (_targetData != null && field.choices.Count > 0)
            {
                _targetData.className = field.choices.First();
                EditorUtility.SetDirty(_targetData);
            }

            AssetDatabase.SaveAssetIfDirty(_targetData);
        }
    }
}
