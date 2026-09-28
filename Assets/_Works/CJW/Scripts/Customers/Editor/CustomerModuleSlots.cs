using System;
using System.Collections.Generic;
using System.Linq;
using _Works.CJW.Scripts.Customers.Movement;
using DevLib.ModuleSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>손님 모듈은 루트가 아니라 모듈마다 자식 오브젝트 하나에 붙인다(ModuleOwner가 자식까지 모은다).
    /// 설정 메뉴가 모듈을 찾거나 새로 붙일 때 이걸 거쳐야 루트에 모듈이 다시 쌓이지 않는다.</summary>
    public static class CustomerModuleSlots
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";

        private static readonly Dictionary<string, string> ChildNames = new()
        {
            { "NavMeshBoardingModule", "BoardingModule" },
            { "NavigationMover", "MoveModule" },
            { "OddGaitModule", "GaitModule" },
            { "SoundEmitterModule", "SoundModule" },
        };

        public static T Find<T>(GameObject root) where T : Component
        {
            return root != null ? root.GetComponentInChildren<T>(true) : null;
        }

        public static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T module = Find<T>(root);
            return module != null ? module : Add<T>(root);
        }

        public static T Add<T>(GameObject root) where T : Component
        {
            return (T)Add(root, typeof(T));
        }

        /// <summary>모듈 이름의 자식을 만들어 붙인다. 모듈이 루트에 있던 때처럼 이동 모듈(MoveModule)보다 먼저 틱을 돌도록 그 앞에 둔다.</summary>
        public static Component Add(GameObject root, Type moduleType)
        {
            var child = new GameObject(ChildName(moduleType));
            child.layer = 0;
            child.transform.SetParent(root.transform, false);
            child.transform.SetSiblingIndex(InsertIndex(root.transform));
            return child.AddComponent(moduleType);
        }

        public static string ChildName(Type moduleType)
        {
            if (ChildNames.TryGetValue(moduleType.Name, out string name))
            {
                return name;
            }

            const string prefix = "Customer";
            return moduleType.Name.StartsWith(prefix) ? moduleType.Name.Substring(prefix.Length) : moduleType.Name;
        }

        /// <summary>루트에 붙은 모듈을 자식으로 옮긴다. 프리팹 안에서 옮긴 모듈을 가리키던 참조도 새 모듈로 바꾼다. 옮긴 개수를 돌려준다.
        /// 원본 프리팹에서 물려받은 모듈은 변형에서 뗄 수 없으니 건너뛴다 — 원본부터 처리하면 변형은 물려받는다.</summary>
        public static int SplitRootModules(GameObject root)
        {
            List<Component> modules = root.GetComponents<Component>()
                .Where(c => c is IModule && c is not ModuleOwner)
                .Where(c => !PrefabUtility.IsPartOfPrefabInstance(c) || PrefabUtility.IsAddedComponentOverride(c))
                .ToList();

            if (modules.Count == 0)
            {
                return 0;
            }

            var remap = new Dictionary<Object, Object>();
            foreach (Component module in modules)
            {
                Component moved = Add(root, module.GetType());
                EditorUtility.CopySerialized(module, moved);
                remap[module] = moved;
            }

            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || remap.ContainsKey(component))
                {
                    continue;
                }

                var so = new SerializedObject(component);
                SerializedProperty property = so.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue != null
                        && remap.TryGetValue(property.objectReferenceValue, out Object target))
                    {
                        property.objectReferenceValue = target;
                        changed = true;
                    }
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (Component module in modules)
            {
                Object.DestroyImmediate(module);
            }

            return modules.Count;
        }

        [MenuItem("Tools/JW/Customers/Split Modules Into Children")]
        private static void SplitAll()
        {
            IEnumerable<string> paths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(VariantDepth);

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<AbstractCustomer>() == null)
                    {
                        continue;
                    }

                    int moved = SplitRootModules(root);
                    if (moved > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }

                    Debug.Log($"[CustomerModuleSlots] {path}: 모듈 {moved}개를 자식으로 옮김");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static int VariantDepth(string path)
        {
            int depth = 0;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            while (prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
            {
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
                depth++;
            }

            return depth;
        }

        private static int InsertIndex(Transform root)
        {
            int leadingModules = 0;
            bool counting = true;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.GetComponent<NavigationMover>() != null)
                {
                    return i;
                }

                if (counting && child.GetComponent<IModule>() != null)
                {
                    leadingModules++;
                }
                else
                {
                    counting = false;
                }
            }

            return leadingModules;
        }
    }
}
