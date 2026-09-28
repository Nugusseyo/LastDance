using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

        [Tooltip("Setting을 누르면 켤 UGUI 설정 창. 창이 스스로 꺼지면(SetActive(false)) 타이틀 버튼이 다시 나타난다.")]
        [SerializeField] private GameObject settingsPanel;

        private VisualElement _root;
        private VisualElement _menu;
        private Button _start;
        private Button _setting;
        private Button _exit;
        private bool _settingsOpen;

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

        private void Update()
        {
            // 설정 창이 자기 닫기 버튼으로 꺼졌으면 타이틀 버튼을 되돌린다.
            if (_settingsOpen && (settingsPanel == null || !settingsPanel.activeInHierarchy))
            {
                CloseSettings();
            }
        }

        /// <summary>설정 창을 닫고 타이틀 버튼을 다시 보인다. UGUI 닫기 버튼의 OnClick에 연결해도 된다.</summary>
        public void CloseSettings()
        {
            _settingsOpen = false;
            if (settingsPanel != null)
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
