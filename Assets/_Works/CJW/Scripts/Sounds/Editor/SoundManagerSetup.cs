using System.Text;
using DevLib.EventChannelSystem;
using DevLib.ObjectPool.Runtime;
using DevLib.SoundSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Works.CJW.Scripts.Sounds.Editor
{
    /// <summary>손님·차가 사운드 채널에 올린 소리를 실제로 틀도록 열린 씬에 SoundManager를 놓는다.
    /// SoundPlayer 프리팹과 풀 항목이 없으면 만들고, 씬의 PoolInitializer가 쓰는 풀 매니저에 등록한다. 이미 있는 것은 건너뛴다.</summary>
    public static class SoundManagerSetup
    {
        private const string ChannelPath = "Assets/_Works/CJW/Data/Event Channel/SoundChannel.asset";
        private const string PlayerPrefabFolder = "Assets/_Works/CJW/Prefabs/Sounds";
        private const string PlayerPrefabPath = PlayerPrefabFolder + "/SoundPlayer.prefab";
        private const string PoolItemPath = "Assets/_Works/CJW/Data/Sounds/SoundPlayer Pool Item.asset";

        [MenuItem("Tools/CJW/Setup Sound Manager In Scene")]
        public static void Setup()
        {
            var log = new StringBuilder();

            EventChannelSO channel = AssetDatabase.LoadAssetAtPath<EventChannelSO>(ChannelPath);
            if (channel == null)
            {
                Debug.LogError($"[SoundManagerSetup] {ChannelPath}가 없습니다. Tools/CJW/Setup Sound Emitters를 먼저 실행하세요.");
                return;
            }

            PoolInitializer initializer = Object.FindFirstObjectByType<PoolInitializer>();
            if (initializer == null || initializer.PoolManager == null)
            {
                Debug.LogError("[SoundManagerSetup] 열린 씬에 풀 매니저가 물린 PoolInitializer가 없습니다.");
                return;
            }

            PoolManagerSO poolManager = initializer.PoolManager;
            PoolItemSO poolItem = GetOrCreatePoolItem(log);

            if (!poolManager.itemList.Contains(poolItem))
            {
                poolManager.itemList.Add(poolItem);
                EditorUtility.SetDirty(poolManager);
                log.AppendLine($"풀 매니저 {poolManager.name}에 SoundPlayer 등록");
            }

            if (Object.FindFirstObjectByType<SoundManager>() == null)
            {
                var go = new GameObject("SoundManager");
                SoundManager manager = go.AddComponent<SoundManager>();
                var so = new SerializedObject(manager);
                so.FindProperty("poolManager").objectReferenceValue = poolManager;
                so.FindProperty("soundItem").objectReferenceValue = poolItem;
                so.FindProperty("<SoundChannel>k__BackingField").objectReferenceValue = channel;
                so.ApplyModifiedPropertiesWithoutUndo();
                Undo.RegisterCreatedObjectUndo(go, "Create SoundManager");
                EditorSceneManager.MarkSceneDirty(go.scene);
                log.AppendLine("씬에 SoundManager 추가 " + go.scene.name);
            }
            else
            {
                log.AppendLine("씬에 SoundManager가 이미 있음");
            }

            AssetDatabase.SaveAssets();
            foreach (string line in log.ToString().Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                Debug.Log("[SoundManagerSetup] " + line);
            }
        }

        private static PoolItemSO GetOrCreatePoolItem(StringBuilder log)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                if (!AssetDatabase.IsValidFolder(PlayerPrefabFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Works/CJW/Prefabs", "Sounds");
                }

                var temp = new GameObject("SoundPlayer");
                AudioSource source = temp.AddComponent<AudioSource>();
                source.playOnAwake = false;
                // 손님·차 자리에서 나는 소리라 거리에 따라 작아지게 3D로 튼다.
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                source.maxDistance = 40f;
                temp.AddComponent<SoundPlayer>();

                prefab = PrefabUtility.SaveAsPrefabAsset(temp, PlayerPrefabPath);
                Object.DestroyImmediate(temp);
                log.AppendLine("SoundPlayer 프리팹 생성 " + PlayerPrefabPath);
            }

            PoolItemSO item = AssetDatabase.LoadAssetAtPath<PoolItemSO>(PoolItemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<PoolItemSO>();
                item.poolingName = "SoundPlayer";
                item.prefab = prefab;
                item.initCount = 16;
                AssetDatabase.CreateAsset(item, PoolItemPath);
                log.AppendLine("풀 항목 생성 " + PoolItemPath);
            }

            // 풀에서 꺼낸 플레이어가 돌아갈 풀을 알아야 Push가 제자리로 간다.
            SoundPlayer player = prefab.GetComponent<SoundPlayer>();
            if (player.PoolItem != item)
            {
                player.PoolItem = item;
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssetIfDirty(prefab);
            }

            return item;
        }
    }
}
