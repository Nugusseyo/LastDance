using System.Collections.Generic;
using System.Text;
using _Works.CJW.Scripts.Cars;
using DevLib.EventChannelSystem;
using DevLib.ModuleSystem;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Sounds.Editor
{
    /// <summary>손님·차 프리팹에 사운드 모듈을 붙이고 사운드 채널을 물린다. 차에는 주행 소리 모듈도 붙인다. 이미 붙어 있는 프리팹은 건너뛴다.</summary>
    public static class SoundEmitterSetup
    {
        private const string ChannelPath = "Assets/_Works/CJW/Data/Event Channel/SoundChannel.asset";
        private static readonly string[] Folders = { "Assets/_Works/CJW/Prefabs/Customers", "Assets/_Works/CJW/Prefabs/Cars" };

        [MenuItem("Tools/CJW/Setup Sound Emitters")]
        public static void Setup()
        {
            var log = new StringBuilder();

            EventChannelSO channel = AssetDatabase.LoadAssetAtPath<EventChannelSO>(ChannelPath);
            if (channel == null)
            {
                channel = ScriptableObject.CreateInstance<EventChannelSO>();
                AssetDatabase.CreateAsset(channel, ChannelPath);
                log.AppendLine("채널 생성 " + ChannelPath);
            }

            // 기반 프리팹을 먼저 처리해야 변형 프리팹이 물려받아 중복으로 붙지 않는다.
            var paths = new List<string> { "Assets/_Works/CJW/Prefabs/Customers/New Customer.prefab" };
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<ModuleOwner>() == null)
                    {
                        log.AppendLine("건너뜀(ModuleOwner 없음) " + path);
                        continue;
                    }

                    bool changed = false;

                    if (root.GetComponentInChildren<SoundEmitterModule>(true) == null)
                    {
                        SoundEmitterModule module = root.AddComponent<SoundEmitterModule>();
                        var so = new SerializedObject(module);
                        so.FindProperty("soundChannel").objectReferenceValue = channel;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        log.AppendLine("사운드 모듈 추가 " + path);
                        changed = true;
                    }

                    // 차에는 주행 소리 모듈도 붙인다.
                    if (root.GetComponent<Car>() != null && root.GetComponentInChildren<CarDriveSoundModule>(true) == null)
                    {
                        root.AddComponent<CarDriveSoundModule>();
                        log.AppendLine("주행 소리 모듈 추가 " + path);
                        changed = true;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    else
                    {
                        log.AppendLine("이미 있음 " + path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            foreach (string line in log.ToString().Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                Debug.Log("[SoundEmitterSetup] " + line);
            }
        }
    }
}
