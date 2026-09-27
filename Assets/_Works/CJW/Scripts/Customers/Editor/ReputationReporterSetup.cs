using _Works.CJW.Scripts.Customers.Visit;
using DevLib.EventChannelSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>열린 씬의 VisitDirector 옆에 평판 보고기를 붙이고 ReviewManager가 듣는 채널을 넣는다.</summary>
    public static class ReputationReporterSetup
    {
        private const string ReviewChannelPath = "Assets/_Works/JYG/Data/EventSO/ReviewEventSO.asset";

        [MenuItem("Tools/JW/Customers/Add Reputation Reporter To Open Scene")]
        public static void Setup()
        {
            var channel = AssetDatabase.LoadAssetAtPath<EventChannelSO>(ReviewChannelPath);

            foreach (VisitDirector director in Object.FindObjectsByType<VisitDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!director.TryGetComponent(out VisitReputationReporter reporter))
                {
                    reporter = Undo.AddComponent<VisitReputationReporter>(director.gameObject);
                }

                var so = new SerializedObject(reporter);
                so.FindProperty("visitDirector").objectReferenceValue = director;
                so.FindProperty("reviewChannel").objectReferenceValue = channel;
                so.ApplyModifiedProperties();

                // 저장은 하지 않는다. 씬에 다른 저장 안 된 수정이 있을 수 있다.
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
                Debug.Log($"[ReputationReporterSetup] {director.gameObject.scene.name}/{director.name}: channel={(channel != null ? channel.name : "없음")}");
            }
        }
    }
}
