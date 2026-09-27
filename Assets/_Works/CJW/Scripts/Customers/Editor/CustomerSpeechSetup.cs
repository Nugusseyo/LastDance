using System.Linq;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using DevLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>모든 손님 프리팹에 말풍선(SpeechState)을 단다. 이미 달린 프리팹은 건드리지 않고,
    /// 없는 프리팹은 Waiting 시퀀스 맨 앞에 행동과 동시에 도는(runAlongside) SpeechState를 넣는다. 여러 번 눌러도 안전하다.</summary>
    public static class CustomerSpeechSetup
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string PoolManagerGuid = "c5baccba6f7390e45915d59f83b4381e";
        private const string BubbleItemGuid = "2a00163c0ed52214a828e00935dbf2e5";

        [MenuItem("Tools/JW/Customers/Add Speech To All Customers")]
        private static void AddToAll()
        {
            var poolManager = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(AssetDatabase.GUIDToAssetPath(PoolManagerGuid));
            var bubbleItem = AssetDatabase.LoadAssetAtPath<PoolItemSO>(AssetDatabase.GUIDToAssetPath(BubbleItemGuid));
            if (poolManager == null || bubbleItem == null)
            {
                Debug.LogError("[CustomerSpeechSetup] 말풍선 풀 매니저나 풀 아이템을 찾지 못했습니다.");
                return;
            }

            // 변형 프리팹이 원본의 변경을 먼저 받도록 원본부터 처리한다.
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant)
                .ToArray();

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (TryAdd(root, poolManager, bubbleItem, out string result))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }

                    Debug.Log($"[CustomerSpeechSetup] {root.name}: {result}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        /// <summary>기획서의 손님별 대사 index. 없는 손님은 말하지 않는다. index가 둘이면 방문마다 갈래로 하나를 고른다.</summary>
        private static readonly (string prefab, int[] lines, bool matchDance)[] Lines =
        {
            ("New Customer", new[] { 1 }, false),
            ("Refueling Customer", new[] { 1 }, false),
            ("Odd Walk Customer", new[] { 1 }, false),
            ("Stay In Car Customer", new[] { 1 }, false),
            ("Car Talker Customer", new[] { 3 }, false),
            ("Vending Visitor Customer", new[] { 8 }, false),
            ("Car Basher Customer", new[] { 2, 4 }, false),
            ("Negotiator Customer", new[] { 5 }, false),
            ("Vending Basher Customer", new[] { 7 }, false),
            ("Brawler Customer", new[] { 9, 10 }, false),
            // 춤 클립 순서(PlayAnimationState.clips)와 같은 순서로 대사가 짝지어진다.
            ("Dancer Customer", new[] { 6, 11 }, true),
            ("Circler Customer", new int[0], false),
            ("Entrance Blocker Customer", new int[0], false),
        };

        [MenuItem("Tools/JW/Customers/Apply Speech Lines")]
        private static void ApplyLines()
        {
            // 변형 프리팹이 원본의 변경을 먼저 받도록 원본부터 처리한다.
            foreach (var (name, lines, matchDance) in Lines.OrderBy(l => l.prefab != "New Customer"))
            {
                string path = $"{PrefabFolder}/{name}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var fsm = root.GetComponentInChildren<CustomerFSMModule>(true);
                    var so = new SerializedObject(fsm);
                    SerializedProperty sequences = so.FindProperty("sequences");
                    int speechCount = 0, danceCount = 0;

                    for (int i = 0; i < sequences.arraySize; i++)
                    {
                        SerializedProperty states = sequences.GetArrayElementAtIndex(i).FindPropertyRelative("States");
                        for (int j = 0; j < states.arraySize; j++)
                        {
                            SerializedProperty state = states.GetArrayElementAtIndex(j);
                            switch (state.managedReferenceValue)
                            {
                                case SpeechState:
                                    SerializedProperty indices = state.FindPropertyRelative("lineIndices");
                                    indices.arraySize = lines.Length;
                                    for (int k = 0; k < lines.Length; k++)
                                    {
                                        indices.GetArrayElementAtIndex(k).intValue = lines[k];
                                    }

                                    speechCount++;
                                    break;

                                case PlayAnimationState when matchDance:
                                    state.FindPropertyRelative("matchVariant").boolValue = true;
                                    danceCount++;
                                    break;
                            }
                        }
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CustomerSpeechSetup] {name}: 대사 [{string.Join(", ", lines)}] → SpeechState {speechCount}개, 춤 짝 맞춤 {danceCount}개");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static bool TryAdd(GameObject root, PoolManagerSO poolManager, PoolItemSO bubbleItem, out string result)
        {
            var fsm = root.GetComponentInChildren<CustomerFSMModule>(true);
            if (fsm == null)
            {
                result = "FSM 없음, 건너뜀";
                return false;
            }

            var so = new SerializedObject(fsm);
            SerializedProperty sequences = so.FindProperty("sequences");
            SerializedProperty waiting = null;

            for (int i = 0; i < sequences.arraySize; i++)
            {
                SerializedProperty seq = sequences.GetArrayElementAtIndex(i);
                SerializedProperty states = seq.FindPropertyRelative("States");

                for (int j = 0; j < states.arraySize; j++)
                {
                    if (states.GetArrayElementAtIndex(j).managedReferenceValue is SpeechState)
                    {
                        result = "이미 있음";
                        return false;
                    }
                }

                if (seq.FindPropertyRelative("Phase").intValue == (int)VisitPhase.Waiting)
                {
                    waiting = states;
                }
            }

            if (waiting == null)
            {
                result = "Waiting 시퀀스가 없어 건너뜀";
                return false;
            }

            waiting.InsertArrayElementAtIndex(0);
            SerializedProperty speech = waiting.GetArrayElementAtIndex(0);
            // new로 만들어 필드 초기값(offset 등)을 그대로 받는다. 0으로 들어가는 함정을 피한다.
            speech.managedReferenceValue = new SpeechState();
            speech.FindPropertyRelative("poolManager").objectReferenceValue = poolManager;
            speech.FindPropertyRelative("bubbleItem").objectReferenceValue = bubbleItem;
            speech.FindPropertyRelative("runAlongside").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            result = $"Waiting 맨 앞에 추가 (상태 {waiting.arraySize}개)";
            return true;
        }
    }
}
