using System.Collections.Generic;
using System.Linq;
using _Works.CJW.Scripts.Customers.Appearance;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>모든 손님이 스폰마다 무작위 겉모습으로 나오게 한다.
    /// 캐릭터 프리팹(npc_csl_00_character_*)을 모아 겉모습 목록을 만들고, 손님 프리팹마다 겉모습 모듈을 붙여 그 목록을 꽂는다.
    /// 베이스 프리팹부터 처리해 변형은 모듈을 물려받게 한다. 이미 붙어 있으면 목록만 다시 꽂는다. 여러 번 눌러도 안전하다.</summary>
    public static class CustomerAppearanceSetup
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string LookFolder = "Assets/Charactor/Prefabs";
        private const string LookFilter = "npc_csl_00_character_";
        private const string LookSetPath = "Assets/_Works/CJW/Data/Customers/Customer Look Set.asset";

        [MenuItem("Tools/JW/Customers/Setup Random Appearance")]
        private static void Setup()
        {
            CustomerLookSetSO lookSet = CreateLookSet();

            // 변형은 베이스의 모듈을 물려받는다. 변형부터 붙이면 베이스에 붙인 것과 겹치므로 얕은 것부터 처리한다.
            List<string> prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(VariantDepth)
                .ToList();

            foreach (string path in prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<AbstractCustomer>() == null)
                    {
                        continue;
                    }

                    var module = root.GetComponent<CustomerAppearanceModule>();
                    bool added = module == null;
                    if (added)
                    {
                        module = root.AddComponent<CustomerAppearanceModule>();
                    }

                    var so = new SerializedObject(module);
                    so.FindProperty("lookSet").objectReferenceValue = lookSet;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CustomerAppearanceSetup] {path}: {(added ? "모듈 추가" : "목록만 갱신")}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static CustomerLookSetSO CreateLookSet()
        {
            var lookSet = AssetDatabase.LoadAssetAtPath<CustomerLookSetSO>(LookSetPath);
            if (lookSet == null)
            {
                lookSet = ScriptableObject.CreateInstance<CustomerLookSetSO>();
                AssetDatabase.CreateAsset(lookSet, LookSetPath);
            }

            var so = new SerializedObject(lookSet);
            SerializedProperty looks = so.FindProperty("looks");
            List<GameObject> found = AssetDatabase.FindAssets("t:Prefab", new[] { LookFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileName(p).StartsWith(LookFilter))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .ToList();

            looks.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                looks.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lookSet);
            Debug.Log($"[CustomerAppearanceSetup] 겉모습 후보 {found.Count}개: {string.Join(", ", found.Select(g => g.name))}");
            return lookSet;
        }

        private static int VariantDepth(string path)
        {
            int depth = 0;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            while (go != null && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant)
            {
                go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                depth++;
            }

            return depth;
        }
    }
}
