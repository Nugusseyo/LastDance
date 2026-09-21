using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DevLib.SoundSystem.Editor
{
    [CustomEditor(typeof(SoundClipSo))]
    public class SoundClipSoEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset uiAsset;
        private const int   WaveformHeight = 80;
        private const float MinDuration    = 0.1f;
        private const float HandleGrabW    = 15f;

        private Texture2D      _waveformTex;
        private AudioClip      _cachedClip;
        private bool           _draggingStart;
        private bool           _draggingEnd;
        private bool           _isPlaying;
        private float          _playEndClipTime;

        private Label          _startLabel;
        private Label          _endLabel;
        private Button         _playBtn;
        private VisualElement  _waveformSection;
        private IMGUIContainer _waveformContainer;

        // 에디터 전용 프리뷰 AudioSource (HideAndDontSave — 씬에 저장되지 않음)
        private static GameObject  _previewGo;
        private static AudioSource _previewSrc;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopPreview();
            _isPlaying = false;
            if (_waveformTex != null)
                DestroyImmediate(_waveformTex);
        }

        // ── CreateInspectorGUI ────────────────────────────────────────────

        public override VisualElement CreateInspectorGUI()
        {
            var so = (SoundClipSo)target;

            if (uiAsset == null)
            {
                Debug.LogError($"[SoundClipSoEditor] UXML을 찾을 수 없음");
                return base.CreateInspectorGUI();
            }

            var root = uiAsset.CloneTree();
            root.Bind(serializedObject);

            _startLabel      = root.Q<Label>("start-label");
            _endLabel        = root.Q<Label>("end-label");
            _playBtn         = root.Q<Button>("play-btn");
            _waveformSection = root.Q<VisualElement>("waveform-section");

            // 웨이브폼은 IMGUI로 렌더링 (드래그 핸들 처리를 위해 IMGUIContainer 사용)
            _waveformContainer = new IMGUIContainer(() => OnWaveformGUI(so));
            _waveformContainer.style.height = WaveformHeight;
            root.Q<VisualElement>("waveform-slot").Add(_waveformContainer);

            // 클립 변경 감지
            root.Q<PropertyField>("clip-field")
                .RegisterValueChangeCallback(evt => OnClipFieldChanged(so, evt));

            // 재생 버튼
            _playBtn.clicked += () => OnPlayButtonClicked(so);

            // 초기 상태
            _cachedClip = so.clip;
            bool hasClip = so.clip != null;
            _waveformSection.style.display = hasClip ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasClip) UpdateLabels(so);

            return root;
        }

        // ── Clip 변경 처리 ────────────────────────────────────────────────

        private void OnClipFieldChanged(SoundClipSo so, SerializedPropertyChangeEvent evt)
        {
            var newClip = evt.changedProperty.objectReferenceValue as AudioClip;
            if (newClip == _cachedClip) return;

            _cachedClip = newClip;

            if (_waveformTex != null) { DestroyImmediate(_waveformTex); _waveformTex = null; }

            if (newClip != null)
            {
                // 클립 교체 시 startTime = 0, endTime = clip 전체 길이로 초기화
                serializedObject.Update();
                serializedObject.FindProperty("startTime").floatValue = 0f;
                serializedObject.FindProperty("endTime").floatValue   = Mathf.Clamp(newClip.length - 1f, MinDuration, newClip.length);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            _waveformSection.style.display = newClip != null ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateLabels(so);
            _waveformContainer?.MarkDirtyRepaint();
        }

        // ── Editor Update (재생 종료 감지 + Repaint) ──────────────────────

        private void OnEditorUpdate()
        {
            if (!_isPlaying) return;

            bool srcStopped = _previewSrc == null || !_previewSrc.isPlaying;
            bool reachedEnd = _previewSrc != null  && _previewSrc.time >= _playEndClipTime;

            if (srcStopped || reachedEnd)
            {
                StopPreview();
                _isPlaying    = false;
                if (_playBtn != null) _playBtn.text = "▶  Play";
            }

            _waveformContainer?.MarkDirtyRepaint();
        }

        // ── Waveform GUI (IMGUIContainer 콜백) ────────────────────────────

        private void OnWaveformGUI(SoundClipSo so)
        {
            if (so == null || so.clip == null) return;

            Rect waveRect = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.Height(WaveformHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                int w = Mathf.Max(1, (int)waveRect.width);
                if (_waveformTex == null || _waveformTex.width != w)
                    _waveformTex = BuildWaveform(so.clip, w, WaveformHeight);
            }

            if (_waveformTex != null)
                DrawWaveformAndHandles(waveRect, so);

            // 드래그 후 레이블 갱신
            if (Event.current.type == EventType.Repaint)
                UpdateLabels(so);
        }

        // ── Waveform + Handles ────────────────────────────────────────────

        private void DrawWaveformAndHandles(Rect rect, SoundClipSo so)
        {
            float duration = so.clip.length;
            float startX   = rect.x + (so.startTime / duration) * rect.width;
            float endX     = rect.x + (so.endTime   / duration) * rect.width;

            GUI.DrawTexture(rect, _waveformTex, ScaleMode.StretchToFill);

            // 범위 밖 dim
            var dim = new Color(0f, 0f, 0f, 0.5f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, startX - rect.x,  rect.height), dim);
            EditorGUI.DrawRect(new Rect(endX,   rect.y, rect.xMax - endX, rect.height), dim);

            // 재생 헤드 (흰색)
            if (_isPlaying && _previewSrc != null && _previewSrc.clip == so.clip)
            {
                float headX = rect.x + (_previewSrc.time / duration) * rect.width;
                EditorGUI.DrawRect(new Rect(headX - 1, rect.y, 2, rect.height), Color.white);
            }

            // start / end 커서 선 + 상단 탭
            EditorGUI.DrawRect(new Rect(startX - 1, rect.y, 2, rect.height), new Color(0.25f, 0.9f, 0.25f));
            EditorGUI.DrawRect(new Rect(endX   - 1, rect.y, 2, rect.height), new Color(0.95f, 0.35f, 0.2f));
            EditorGUI.DrawRect(new Rect(startX - 5, rect.y, 10, 12), new Color(0.25f, 0.9f, 0.25f));
            EditorGUI.DrawRect(new Rect(endX   - 5, rect.y, 10, 12), new Color(0.95f, 0.35f, 0.2f));

            var startGrab = new Rect(startX - HandleGrabW * 0.5f, rect.y, HandleGrabW, rect.height);
            var endGrab   = new Rect(endX   - HandleGrabW * 0.5f, rect.y, HandleGrabW, rect.height);
            EditorGUIUtility.AddCursorRect(startGrab, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(endGrab,   MouseCursor.ResizeHorizontal);

            HandleDrag(rect, so, startGrab, endGrab, duration);
        }

        private void HandleDrag(Rect rect, SoundClipSo so,
                                 Rect startGrab, Rect endGrab, float duration)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    if (startGrab.Contains(e.mousePosition))    { _draggingStart = true; e.Use(); }
                    else if (endGrab.Contains(e.mousePosition)) { _draggingEnd   = true; e.Use(); }
                    break;

                case EventType.MouseUp:
                    _draggingStart = _draggingEnd = false;
                    break;

                case EventType.MouseDrag when _draggingStart || _draggingEnd:
                {
                    float t = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width) * duration;
                    serializedObject.Update();

                    if (_draggingStart)
                    {
                        t = Mathf.Clamp(t, 0f, so.endTime - MinDuration);
                        serializedObject.FindProperty("startTime").floatValue = t;
                    }
                    else
                    {
                        t = Mathf.Clamp(t, so.startTime + MinDuration, duration);
                        serializedObject.FindProperty("endTime").floatValue = t;
                    }

                    serializedObject.ApplyModifiedProperties();
                    UpdateLabels(so);
                    e.Use();
                    break;
                }
            }
        }

        // ── Play Button ───────────────────────────────────────────────────

        private void OnPlayButtonClicked(SoundClipSo so)
        {
            if (_isPlaying)
            {
                StopPreview();
                _isPlaying    = false;
                _playBtn.text = "▶  Play";
            }
            else
            {
                float pitch = so.pitch;
                if (so.randomizePitch)
                    pitch = Mathf.Clamp(
                        pitch + Random.Range(-so.randomPitchModifier, so.randomPitchModifier),
                        0.1f, 3f);

                _playEndClipTime = so.endTime;
                PlayPreview(so.clip, so.startTime, pitch);
                _isPlaying    = true;
                _playBtn.text = "■  Stop";
            }
        }

        private void UpdateLabels(SoundClipSo so)
        {
            if (so == null) return;
            if (_startLabel != null) _startLabel.text = $"Start: {so.startTime:F3} s";
            if (_endLabel   != null) _endLabel.text   = $"End: {so.endTime:F3} s";
        }

        // ── Preview AudioSource ───────────────────────────────────────────

        private static void PlayPreview(AudioClip clip, float startTime, float pitch)
        {
            var src         = EnsurePreviewSrc();
            src.clip        = clip;
            src.pitch       = pitch;
            src.timeSamples = Mathf.RoundToInt(startTime * clip.frequency);
            src.Play();
        }

        private static void StopPreview()
        {
            if (_previewSrc != null) _previewSrc.Stop();
        }

        private static AudioSource EnsurePreviewSrc()
        {
            if (_previewSrc != null) return _previewSrc;

            _previewGo  = EditorUtility.CreateGameObjectWithHideFlags(
                "~SoundPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
            _previewSrc = _previewGo.GetComponent<AudioSource>();
            return _previewSrc;
        }

        // ── Waveform Texture ──────────────────────────────────────────────

        private static Texture2D BuildWaveform(AudioClip clip, int width, int height)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            int     total  = clip.samples;
            int     chans  = clip.channels;
            var     bg     = new Color(0.13f, 0.13f, 0.13f, 1f);
            var     wc     = new Color(0.38f, 0.68f, 1f,   1f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg; //전부 하얀색으로 칠하고.

            // 텍스처의 각 픽셀 열(x)을 순회 — 열 하나가 오디오 샘플 구간 하나에 대응
            for (int x = 0; x < width; x++)
            {
                // 이 열이 담당하는 샘플 배열의 시작/끝 인덱스 계산
                // (픽셀 너비 : 전체 샘플 수 = x열 : s0~s1 구간)
                // * chans 는 스테레오처럼 채널이 여럿일 때 채널 수만큼 건너뛰기 위함
                int s0 = (int)((float)x       / width * total) * chans;
                int s1 = (int)((float)(x + 1) / width * total) * chans;

                // 배열 범위 초과 방지
                s1 = Mathf.Min(s1, samples.Length);
                // 구간 폭이 0이면 최소 샘플 1개는 읽도록 보정
                if (s0 >= s1) s1 = s0 + 1;

                // 이 구간에서 파형의 최솟값(lo)과 최댓값(hi)을 탐색
                // 오디오 샘플 값의 범위는 -1.0 ~ +1.0
                float lo = 0f, hi = 0f;
                for (int s = s0; s < s1; s++)
                {
                    if (samples[s] < lo) lo = samples[s];
                    if (samples[s] > hi) hi = samples[s];
                }

                // 샘플 값(-1~+1)을 픽셀 y좌표(0~height)로 변환
                // lo * 0.5 + 0.5 → 0.0~0.5 (아래쪽), hi * 0.5 + 0.5 → 0.5~1.0 (위쪽)
                int yLo = Mathf.Clamp((int)((lo * 0.5f + 0.5f) * height), 0, height - 1);
                int yHi = Mathf.Clamp((int)((hi * 0.5f + 0.5f) * height), 0, height - 1);

                // yLo ~ yHi 사이의 픽셀을 파형 색으로 채워 막대 모양의 파형을 그림
                for (int y = yLo; y <= yHi; y++)
                    pixels[y * width + x] = wc;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}