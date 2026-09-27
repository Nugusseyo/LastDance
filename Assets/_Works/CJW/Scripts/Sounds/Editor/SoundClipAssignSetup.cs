using System.Collections.Generic;
using System.Text;
using _Works.CJW.Scripts.Cars;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Sounds.Editor
{
    /// <summary>Sounds 폴더의 오디오 파일마다 SoundClipSo를 만들고, 파일 이름에 맞는 손님·차 프리팹의 사운드 필드에 넣는다.
    /// 이미 무언가 들어 있는 필드는 건드리지 않는다 — 손으로 바꾼 소리를 다시 돌려놓지 않게 한다.</summary>
    public static class SoundClipAssignSetup
    {
        private const string AudioFolder = "Assets/_Works/CJW/Sounds";
        private const string ClipFolder = "Assets/_Works/CJW/Data/Sounds";
        private const string CustomerFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string CarFolder = "Assets/_Works/CJW/Prefabs/Cars";
        private const string BaseCustomer = CustomerFolder + "/New Customer.prefab";

        /// <summary>오디오 파일 이름 → 반복 여부, 음량, 음높이 흔들기.</summary>
        private static readonly Dictionary<string, (bool loop, float volume, bool randomPitch)> ClipSettings = new()
        {
            { "Drive", (true, 0.6f, false) },
            { "Refueling", (true, 0.8f, false) },
            { "Customer Fight", (true, 0.7f, false) },
            { "GangNam Dance", (true, 0.8f, false) },
            { "Car Crash", (false, 1f, true) },
            { "Attack", (false, 1f, true) },
            { "Attack VendingMachine", (false, 1f, true) },
            { "Kick", (false, 1f, true) },
            { "Hit", (false, 1f, true) },
        };

        /// <summary>어느 프리팹(이름에 포함된 글자, null이면 전부)의 어느 타입(컴포넌트·상태) 필드에 어떤 소리를 넣을지.</summary>
        private static readonly (string prefab, string owner, string field, string clip)[] Rules =
        {
            // 차
            (null, "CarDriveSoundModule", "driveSound", "Drive"),
            (null, "CarFuelSoundModule", "fuelSound", "Refueling"),
            (null, "CarHitDetectorModule", "hitSound", "Car Crash"),

            // 손님 공통 — 맞았을 때
            (null, "CustomerHealthModule", "damagedSound", "Hit"),

            // 싸움 손님 — 깔리는 몸싸움 소리 + 차례마다 발차기·막기
            (null, "MeetUpState", "fightLoopSound", "Customer Fight"),
            (null, "MeetUpState", "attackSound", "Kick"),
            (null, "MeetUpState", "blockSound", "Hit"),

            // 춤 손님
            ("Dancer", "PlayAnimationState", "startSound", "GangNam Dance"),

            // 부수는 손님 — 대상에 따라 소리가 다르다
            ("Vending Basher", "VandalizeState", "hitSound", "Attack VendingMachine"),
            ("Car Basher", "VandalizeState", "hitSound", "Attack"),
        };

        [MenuItem("Tools/CJW/Assign Sounds From Folder")]
        public static void Setup()
        {
            var log = new StringBuilder();
            Dictionary<string, SoundClipSo> clips = CreateClips(log);

            // 기반 프리팹을 먼저 처리해야 변형 프리팹이 물려받아 쓸데없는 오버라이드가 생기지 않는다.
            var paths = new List<string> { BaseCustomer };
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CustomerFolder, CarFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            foreach (string path in paths)
            {
                AssignPrefab(path, clips, log);
            }

            AssetDatabase.SaveAssets();
            foreach (string line in log.ToString().Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                Debug.Log("[SoundClipAssignSetup] " + line);
            }
        }

        private static Dictionary<string, SoundClipSo> CreateClips(StringBuilder log)
        {
            var clips = new Dictionary<string, SoundClipSo>();

            if (!AssetDatabase.IsValidFolder(ClipFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Works/CJW/Data", "Sounds");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string audioPath = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip audio = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                string clipPath = $"{ClipFolder}/{audio.name}.asset";

                SoundClipSo clip = AssetDatabase.LoadAssetAtPath<SoundClipSo>(clipPath);
                if (clip == null)
                {
                    clip = ScriptableObject.CreateInstance<SoundClipSo>();
                    clip.audioType = AudioTypes.Sfx;
                    clip.clip = audio;

                    if (ClipSettings.TryGetValue(audio.name, out var setting))
                    {
                        clip.loop = setting.loop;
                        clip.volume = setting.volume;
                        clip.randomizePitch = setting.randomPitch;
                    }

                    AssetDatabase.CreateAsset(clip, clipPath);
                    log.AppendLine($"클립 생성 {clipPath} ({audio.length:F1}초, loop={clip.loop})");
                }

                clips[audio.name] = clip;
            }

            return clips;
        }

        private static void AssignPrefab(string path, Dictionary<string, SoundClipSo> clips, StringBuilder log)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<ModuleOwner>() == null)
                {
                    return;
                }

                bool changed = false;

                // 주유 소리 모듈은 새로 생겼으므로 차에 없으면 붙인다.
                if (root.GetComponent<Car>() != null && root.GetComponentInChildren<CarFuelSoundModule>(true) == null)
                {
                    root.AddComponent<CarFuelSoundModule>();
                    log.AppendLine("주유 소리 모듈 추가 " + path);
                    changed = true;
                }

                string prefabName = System.IO.Path.GetFileNameWithoutExtension(path);

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    var so = new SerializedObject(behaviour);
                    SerializedProperty it = so.GetIterator();
                    bool soChanged = false;

                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null)
                        {
                            continue;
                        }

                        string ownerType = OwnerTypeName(so, it, behaviour);
                        string clipName = FindRule(prefabName, ownerType, it.name);
                        if (clipName == null || !clips.TryGetValue(clipName, out SoundClipSo clip))
                        {
                            continue;
                        }

                        it.objectReferenceValue = clip;
                        soChanged = true;
                        log.AppendLine($"{prefabName}: {ownerType}.{it.name} = {clipName}");
                    }

                    if (soChanged)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>필드를 가진 타입 이름. 상태처럼 [SerializeReference]로 담긴 객체 안의 필드면 그 객체의 타입, 아니면 컴포넌트 타입이다.</summary>
        private static string OwnerTypeName(SerializedObject so, SerializedProperty property, MonoBehaviour behaviour)
        {
            string path = property.propertyPath;
            int dot = path.LastIndexOf('.');
            if (dot > 0)
            {
                SerializedProperty parent = so.FindProperty(path.Substring(0, dot));
                if (parent != null && parent.propertyType == SerializedPropertyType.ManagedReference && parent.managedReferenceValue != null)
                {
                    return parent.managedReferenceValue.GetType().Name;
                }
            }

            return behaviour.GetType().Name;
        }

        private static string FindRule(string prefabName, string ownerType, string field)
        {
            foreach (var rule in Rules)
            {
                if (rule.owner == ownerType && rule.field == field && (rule.prefab == null || prefabName.Contains(rule.prefab)))
                {
                    return rule.clip;
                }
            }

            return null;
        }
    }
}
