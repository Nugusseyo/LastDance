using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace _Works.CJW.Scripts.Sounds.Editor
{
    /// <summary>Demo 씬에 배경음(BGM) AudioSource를 놓는다. Master 믹서의 BGM 그룹으로 내보내므로 설정 창의 BGM 볼륨이 그대로 먹는다.
    /// 다시 돌려도 된다(있는 BGM 오브젝트를 고쳐 쓴다).</summary>
    public static class DemoBgmSetup
    {
        private const string ScenePath = "Assets/_Works/NHW/Demo.unity";
        private const string ClipPath = "Assets/_Works/CJW/Edvard-Grieg-Peer-Gynt-Morning-Mood-mp3ify.mp3";
        private const string MixerPath = "Assets/_Works/JYG/Sound/Master.mixer";
        private const string GroupName = "BGM";
        private const string ObjectName = "BGM";

        [MenuItem("Tools/CJW/Setup Demo BGM")]
        private static void Setup()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            AudioMixerGroup[] groups = mixer != null ? mixer.FindMatchingGroups(GroupName) : null;
            if (clip == null || groups == null || groups.Length == 0)
            {
                Debug.LogError($"[DemoBgm] 클립({ClipPath}) 또는 믹서 그룹({MixerPath}/{GroupName})을 찾지 못했습니다.");
                return;
            }

            StreamClip();

            // 지금 열린 씬(저장 안 된 변경 포함)은 건드리지 않고 Demo만 옆에 열어 고친 뒤 닫는다.
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            GameObject go = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == ObjectName)
                {
                    go = root;
                }
            }

            if (go == null)
            {
                go = new GameObject(ObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            if (!go.TryGetComponent(out AudioSource source))
            {
                source = go.AddComponent<AudioSource>();
            }

            source.clip = clip;
            source.outputAudioMixerGroup = groups[0];
            source.playOnAwake = true;
            source.loop = true;
            // 배경음이라 위치와 상관없이 똑같이 들리게 2D로 튼다.
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0.6f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log($"[DemoBgm] {scene.name}에 BGM 설정 완료 — {clip.name} → {mixer.name}/{groups[0].name}, loop");
        }

        /// <summary>곡이 길어 메모리에 통째로 풀지 않고 스트리밍으로 읽게 한다.</summary>
        private static void StreamClip()
        {
            if (AssetImporter.GetAtPath(ClipPath) is not AudioImporter importer)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming)
            {
                return;
            }

            settings.loadType = AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
