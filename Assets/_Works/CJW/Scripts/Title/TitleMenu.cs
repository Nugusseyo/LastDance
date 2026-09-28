using _Works.JYG._Scripts.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace _Works.CJW.Scripts.Title
{
    /// <summary>타이틀 UI(UI Toolkit)의 버튼 동작. Start는 게임 씬으로, Setting은 UGUI 설정 창을 띄우고, Exit은 게임을 끈다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class TitleMenu : MonoBehaviour
    {
        private const string ShownClass = "title-root--shown";
        private const string MenuHiddenClass = "menu--hidden";

        [Tooltip("Start를 누르면 불러올 씬 이름. Build Profiles의 씬 목록에 들어 있어야 한다.")]
        [SerializeField] private string startSceneName = "Demo";

        [Tooltip("Setting을 누르면 켤 UGUI 설정 창(씬 오브젝트 또는 프리팹). 창이 스스로 꺼지면(SetActive(false)) 타이틀 버튼이 다시 나타난다.")]
        [SerializeField] private GameObject settingsPanel;

        private VisualElement _root;
        private VisualElement _menu;
        private Button _start;
        private Button _setting;
        private Button _exit;
        private bool _settingsOpen;
        private PanelUI[] _panels;

        private void OnEnable()
        {
            VisualElement doc = GetComponent<UIDocument>().rootVisualElement;
            _root = doc.Q("root");
            _menu = doc.Q("menu");
            _start = doc.Q<Button>("start-button");
            _setting = doc.Q<Button>("setting-button");
            _exit = doc.Q<Button>("exit-button");

            if (_root == null || _start == null || _setting == null || _exit == null)
            {
                Debug.LogError("[TitleMenu] UXML에서 root·버튼을 찾지 못했습니다. UIDocument의 Source Asset을 확인하세요.", this);
                return;
            }

            _start.clicked += OnStart;
            _setting.clicked += OnSetting;
            _exit.clicked += OnExit;

            // 첫 프레임에 클래스를 붙이면 전환(transition)이 안 걸려서 한 박자 늦게 붙인다.
            _root.schedule.Execute(() => _root.AddToClassList(ShownClass)).StartingIn(100);
        }

        private void OnDisable()
        {
            if (_start != null)
            {
                _start.clicked -= OnStart;
                _setting.clicked -= OnSetting;
                _exit.clicked -= OnExit;
            }
        }

        private void Start()
        {
            if (settingsPanel == null)
            {
                return;
            }

            // 프리팹 에셋이 연결돼 있으면 켜도 화면에 안 나온다. 씬에 미리 만들어 둔다.
            // PanelUI는 Awake에서 스스로 숨고 Start에서 열기·닫기 리스너를 붙이므로, 누르기 전에 한 프레임 이상 먼저 만들어 둬야 열린다.
            if (!settingsPanel.scene.IsValid())
            {
                settingsPanel = Instantiate(settingsPanel);
            }

            settingsPanel.SetActive(true);

            // UGUI 오버레이 캔버스와 UI Toolkit 패널은 정렬 순서로 앞뒤가 정해진다. 설정 창이 타이틀 문서보다 위에 그려지고 클릭도 먼저 받게 한다.
            PanelSettings titlePanel = GetComponent<UIDocument>().panelSettings;
            int above = (titlePanel != null ? Mathf.RoundToInt(titlePanel.sortingOrder) : 0) + 1;
            foreach (Canvas canvas in settingsPanel.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.isRootCanvas)
                {
                    canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, above);
                }
            }

            _panels = settingsPanel.GetComponentsInChildren<PanelUI>(true);
            foreach (PanelUI panel in _panels)
            {
                panel.Close.AddListener(CloseSettings);
            }
        }

        private void OnDestroy()
        {
            if (_panels == null)
            {
                return;
            }

            foreach (PanelUI panel in _panels)
            {
                if (panel != null)
                {
                    panel.Close.RemoveListener(CloseSettings);
                }
            }
        }

        private void LateUpdate()
        {
            // 설정 창(PanelUI)은 숨을 때 플레이어 입력을 켜면서 커서를 잠그고 숨긴다(게임 플레이용).
            // 타이틀에선 마우스로 버튼을 눌러야 하므로 잠기면 바로 푼다.
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            // PanelUI가 아닌 창이 스스로 꺼진 경우에도 타이틀 버튼을 되돌린다.
            if (_settingsOpen && (settingsPanel == null || !settingsPanel.activeInHierarchy))
            {
                CloseSettings();
            }
        }

        /// <summary>설정 창을 닫고 타이틀 버튼을 다시 보인다. 설정 창 안 PanelUI 중 하나라도 닫히면 불린다.</summary>
        public void CloseSettings()
        {
            if (!_settingsOpen)
            {
                return;
            }

            _settingsOpen = false;

            // 닫기 버튼은 안쪽 패널만 닫으므로 바깥 패널(루트)까지 닫아 투명한 창이 클릭을 막지 않게 한다.
            if (_panels != null && _panels.Length > 0)
            {
                foreach (PanelUI panel in _panels)
                {
                    panel.InvokeClose();
                }
            }
            else if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            _menu?.RemoveFromClassList(MenuHiddenClass);
        }

        private void OnStart()
        {
            if (!Application.CanStreamedLevelBeLoaded(startSceneName))
            {
                Debug.LogError($"[TitleMenu] '{startSceneName}' 씬이 Build Profiles 씬 목록에 없어 불러올 수 없습니다.", this);
                return;
            }

            // 불러오는 동안 여러 번 눌리지 않게 막는다.
            _start.SetEnabled(false);
            _setting.SetEnabled(false);
            _exit.SetEnabled(false);
            SceneManager.LoadSceneAsync(startSceneName);
        }

        private void OnSetting()
        {
            if (settingsPanel == null)
            {
                Debug.LogWarning("[TitleMenu] 설정 창(settingsPanel)이 아직 연결되지 않았습니다.", this);
                return;
            }

            settingsPanel.SetActive(true);
            _settingsOpen = true;

            // PanelUI는 CanvasGroup 투명도로 숨어 있어 켜기만 해서는 안 보인다. 루트 패널을 열면 안쪽 패널도 따라 열린다.
            if (_panels != null && _panels.Length > 0)
            {
                _panels[0].InvokeOpen();
            }

            _menu?.AddToClassList(MenuHiddenClass);
        }

        private void OnExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
