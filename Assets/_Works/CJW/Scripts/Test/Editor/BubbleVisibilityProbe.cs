using System.IO;
using System.Text;
using _Works.JYG._Scripts.UI.SpeechBubble;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>플레이 중 떠 있는 말풍선이 메인 카메라에 실제로 보일 수 있는지 몇 번 찍어 적는다(레이어·컬링 마스크·캔버스·크기·화면 안).
    /// 결과는 Temp/CustomerSim/bubble_visibility.txt. 확인이 끝나면 지워도 된다.</summary>
    [InitializeOnLoad]
    public static class BubbleVisibilityProbe
    {
        private const string OutPath = "Temp/CustomerSim/bubble_visibility.txt";
        private const string RunningKey = "JW.BubbleVisibility.Running";
        private const float TimeScale = 3f;
        private static readonly float[] SnapshotAt = { 20f, 35f, 50f, 65f, 80f };

        private static int _next;
        private static float _start;
        private static readonly StringBuilder Log = new();

        static BubbleVisibilityProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/JW/Test/Run Bubble Visibility Test")]
        public static void Run()
        {
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
            {
                _next = 0;
                _start = Time.time;
                Log.Clear();
                Time.timeScale = TimeScale;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(RunningKey, false);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || _next >= SnapshotAt.Length || Time.time - _start < SnapshotAt[_next])
            {
                return;
            }

            Snapshot(Time.time - _start);
            _next++;

            if (_next >= SnapshotAt.Length)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                File.WriteAllText(OutPath, Log.ToString());
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
            }
        }

        private static void Snapshot(float t)
        {
            Camera cam = Camera.main;
            Log.AppendLine($"== {t:F0}s camera={(cam != null ? cam.name : "없음")} cullingMask={(cam != null ? cam.cullingMask : 0)}");

            foreach (SpeechBubble b in Object.FindObjectsByType<SpeechBubble>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var canvas = b.GetComponentInChildren<Canvas>(true);
                var tmp = b.GetComponentInChildren<TMPro.TMP_Text>(true);
                var group = b.GetComponentInChildren<CanvasGroup>(true);
                int layer = tmp != null ? tmp.gameObject.layer : b.gameObject.layer;
                bool culled = cam != null && (cam.cullingMask & (1 << layer)) == 0;
                Vector3 vp = cam != null ? cam.WorldToViewportPoint(b.transform.position) : Vector3.zero;
                float dot = cam != null ? Vector3.Dot(b.transform.forward, cam.transform.forward) : 0f;

                // 가장 가까운 손님 이름
                string owner = "-";
                float best = 3f;
                foreach (var c in Object.FindObjectsByType<Customers.AbstractCustomer>(FindObjectsSortMode.None))
                {
                    Vector3 d = c.transform.position - b.transform.position;
                    d.y = 0f;
                    if (d.magnitude < best)
                    {
                        best = d.magnitude;
                        owner = $"{c.name}[{c.Fsm?.Machine?.Current?.GetType().Name}]";
                    }
                }

                Log.AppendLine($"  owner={owner} text='{tmp?.text}' tmpEnabled={tmp?.enabled} alpha={tmp?.alpha:F2} groupAlpha={(group != null ? group.alpha : -1f):F2} " +
                               $"layer={LayerMask.LayerToName(layer)} culled={culled} canvas={(canvas != null ? canvas.renderMode + "/" + canvas.enabled : "없음")} " +
                               $"scale={b.transform.lossyScale.x:F4} dist={(cam != null ? Vector3.Distance(cam.transform.position, b.transform.position) : 0f):F1} " +
                               $"vp=({vp.x:F2},{vp.y:F2},{vp.z:F1}) facingDot={dot:F2}");
            }
        }
    }
}
