using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _Works.CJW.Scripts.Title.Editor
{
    /// <summary>Title_JW 씬에 UI Toolkit 타이틀 메뉴(UIDocument + TitleMenu)를 붙이고, Start가 불러올 Demo 씬을 빌드 씬 목록에 넣는다.
    /// 다시 돌려도 된다(있는 것은 재사용).</summary>
    public static class TitleUISetup
    {
        private const string TitleScenePath = "Assets/_Works/CJW/Scene/Title_JW.unity";
        private const string DemoScenePath = "Assets/_Works/NHW/Demo.unity";
        private const string UiFolder = "Assets/_Works/CJW/UI/Title";
        private const string UxmlPath = UiFolder + "/TitleMenu.uxml";
        private const string ThemePath = UiFolder + "/TitleTheme.tss";
        private const string PanelSettingsPath = UiFolder + "/TitlePanelSettings.asset";
        private const string UiObjectName = "Title UI";

        [MenuItem("Tools/JW/Title/Setup Title UI")]
        private static void Setup()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (uxml == null || theme == null)
            {
                Debug.LogError($"[TitleUI] {UxmlPath} 또는 {ThemePath}를 불러오지 못했습니다.");
                return;
            }

            PanelSettings panel = LoadOrCreatePanelSettings(theme);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TitleScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            }

            GameObject go = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == UiObjectName)
                {
                    go = root;
                }
            }

            if (go == null)
            {
                go = new GameObject(UiObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            // 에디터의 GetComponent는 없을 때 가짜 null을 돌려줘 ??로 거를 수 없다.
            if (!go.TryGetComponent(out UIDocument doc))
            {
                doc = go.AddComponent<UIDocument>();
            }

            doc.panelSettings = panel;
            doc.visualTreeAsset = uxml;
            if (go.GetComponent<TitleMenu>() == null)
            {
                go.AddComponent<TitleMenu>();
            }

            EnsureEventSystem(scene);
            AddToBuildScenes(TitleScenePath, DemoScenePath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TitleUI] Title UI 설정 완료 — UIDocument·TitleMenu, 빌드 씬 목록에 Title_JW·Demo");
        }

        private static PanelSettings LoadOrCreatePanelSettings(ThemeStyleSheet theme)
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }

        /// <summary>프로젝트 기본 입력(Controls.inputactions)엔 UI 맵이 없어 UI 클릭이 안 먹을 수 있다.
        /// 기본 UI 입력을 가진 InputSystemUIInputModule을 두면 UI Toolkit·UGUI 둘 다 그걸로 입력을 받는다.</summary>
        private static void EnsureEventSystem(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null)
                {
                    return;
                }
            }

            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            SceneManager.MoveGameObjectToScene(go, scene);
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>없는 씬만 목록 끝에 붙인다. 기존 순서(시작 씬)는 건드리지 않는다.</summary>
        private static void AddToBuildScenes(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in paths)
            {
                if (!scenes.Exists(s => s.path == path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
