using System.IO;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>플레이 중 평판 채널로 나가는 ReviewEvent를 전부 Temp/CustomerSim/reputation.txt에 적는다.
    /// 주유·피격 테스트와 함께 켜 두고 어떤 일에 얼마가 나갔는지 본다. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class ReputationTap
    {
        private const string ChannelPath = "Assets/_Works/JYG/Data/EventSO/ReviewEventSO.asset";
        private const string OutPath = "Temp/CustomerSim/reputation.txt";
        private const string EnabledKey = "JW.ReputationTap";

        private static EventChannelSO _channel;

        static ReputationTap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/JW/Test/Toggle Reputation Tap")]
        public static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(EnabledKey, false);
            EditorPrefs.SetBool(EnabledKey, on);
            Debug.Log($"[ReputationTap] {(on ? "켜짐" : "꺼짐")} → {OutPath}");
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(EnabledKey, false))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                File.WriteAllText(OutPath, "");
                _channel = AssetDatabase.LoadAssetAtPath<EventChannelSO>(ChannelPath);
                _channel?.AddListener<ReviewEvent>(OnReview);
            }
            else if (change == PlayModeStateChange.ExitingPlayMode && _channel != null)
            {
                _channel.RemoveListener<ReviewEvent>(OnReview);
                _channel = null;
            }
        }

        private static void OnReview(ReviewEvent evt)
        {
            File.AppendAllText(OutPath, $"{Time.time:F1}s {evt.ReviewType}\n");
        }
    }
}
