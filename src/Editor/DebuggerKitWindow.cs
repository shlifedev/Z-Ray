using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumera.DebuggerKit.Editor
{
    // 디버거 킷 에디터 창(Tools/Lumera/DebuggerKit/Window, Shift+F1). 게임 화면을 가리지 않고 치트 메뉴와 콘솔을 쓴다.
    // 플레이 중인 씬의 DebuggerKitHost에 붙어서 런타임 화면과 같은 치트, 기록, 즐겨찾기를 쓴다.
    // 무엇을 보일지와 키 동작은 런타임과 같은 Core 모델(CheatMenuModel, ConsoleSession)이 정한다. 이 창은 UI Toolkit으로 그리기만 한다.
    public sealed class DebuggerKitWindow : EditorWindow
    {
        private const string STYLE_PATH = "Packages/com.lumera.debuggerkit/Editor/DebuggerKitWindow.uss";
        private const long TICK_MILLISECONDS = 50;

        [SerializeField] private int _tab;

        private DebuggerKitHost _hostOrNull;
        private CheatMenuPane _menu;
        private ConsolePane _console;
        private VisualElement _idle;
        private VisualElement _pages;
        private Button _menuTab;
        private Button _consoleTab;
        private Button _clearButton;
        private VisualElement _statusDot;
        private Label _status;
        private IVisualElementScheduledItem _tick;

        [MenuItem("Tools/Lumera/DebuggerKit/Window")]
        public static void Open()
        {
            GetWindow<DebuggerKitWindow>().Show();
        }

        [Shortcut("Lumera/DebuggerKit Window", KeyCode.F1, ShortcutModifiers.Shift)]
        private static void OpenFromShortcut()
        {
            Open();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("디버거 킷");
            minSize = new Vector2(440.0f, 260.0f);
        }

        private void OnDisable()
        {
            _tick?.Pause();
            Bind(null);
        }

        public void CreateGUI()
        {
            _tick?.Pause();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH));
            root.AddToClassList("kit");
            root.EnableInClassList("light-theme", !EditorGUIUtility.isProSkin);

            VisualElement header = new VisualElement();
            header.AddToClassList("topbar");

            VisualElement tabs = new VisualElement();
            tabs.AddToClassList("tabs");
            _menuTab = new Button(() => SelectTab(0)) { text = "치트 메뉴" };
            _menuTab.AddToClassList("tab");
            _consoleTab = new Button(() => SelectTab(1)) { text = "콘솔" };
            _consoleTab.AddToClassList("tab");
            tabs.Add(_menuTab);
            tabs.Add(_consoleTab);
            header.Add(tabs);

            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("topbar-spacer");
            header.Add(spacer);

            _clearButton = new Button(OnClearClicked) { text = "지우기", tooltip = "콘솔 기록 비우기" };
            _clearButton.AddToClassList("topbar-action");
            header.Add(_clearButton);

            VisualElement statusGroup = new VisualElement();
            statusGroup.AddToClassList("status-group");
            _statusDot = new VisualElement();
            _statusDot.AddToClassList("status-dot");
            _status = new Label();
            _status.AddToClassList("toolbar-status");
            statusGroup.Add(_statusDot);
            statusGroup.Add(_status);
            header.Add(statusGroup);
            root.Add(header);

            _idle = new VisualElement();
            _idle.AddToClassList("idle");
            VisualElement idleCard = new VisualElement();
            idleCard.AddToClassList("idle-card");
            Label idleTitle = new Label("플레이 중이 아닙니다");
            idleTitle.AddToClassList("idle-title");
            Label idleText = new Label("플레이를 시작하면 씬의 DebuggerKitHost에 자동으로 연결됩니다.");
            idleText.AddToClassList("idle-text");
            Button playButton = new Button(() => EditorApplication.isPlaying = true) { text = "플레이 시작" };
            playButton.AddToClassList("run");
            playButton.AddToClassList("idle-play");
            idleCard.Add(idleTitle);
            idleCard.Add(idleText);
            idleCard.Add(playButton);
            _idle.Add(idleCard);
            root.Add(_idle);

            _pages = new VisualElement();
            _pages.AddToClassList("grow");
            _menu = new CheatMenuPane();
            _console = new ConsolePane();
            _pages.Add(_menu.Root);
            _pages.Add(_console.Root);
            root.Add(_pages);

            _hostOrNull = null;
            SelectTab(_tab);
            Tick();
            _tick = root.schedule.Execute(Tick).Every(TICK_MILLISECONDS);
        }

        private void SelectTab(int tab)
        {
            _tab = tab;
            _menuTab.EnableInClassList("active", tab == 0);
            _consoleTab.EnableInClassList("active", tab == 1);
            _menu.Root.style.display = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _console.Root.style.display = tab == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateHeaderActions();
            if (tab == 1)
            {
                _console.Focus();
            }
        }

        private void OnClearClicked()
        {
            if (_hostOrNull != null)
            {
                _hostOrNull.Transcript.Clear();
                _console.Tick();
                _console.Focus();
            }
        }

        // 플레이를 시작하거나 멈추면 호스트가 바뀐다. 바뀐 호스트에 창을 다시 붙인다.
        private void Tick()
        {
            DebuggerKitHost host = EditorApplication.isPlaying ? DebuggerKitHost.CurrentOrNull : null;
            if (host != _hostOrNull)
            {
                Bind(host);
            }

            bool bBound = _hostOrNull != null;
            _idle.style.display = bBound ? DisplayStyle.None : DisplayStyle.Flex;
            _pages.style.display = bBound ? DisplayStyle.Flex : DisplayStyle.None;
            _statusDot.EnableInClassList("live", bBound);
            UpdateHeaderActions();
            if (!bBound)
            {
                _status.text = EditorApplication.isPlaying ? "호스트 없음" : "대기 중";
                return;
            }

            CountMenuItems(_hostOrNull.Registry, out int totalCount, out int propertyCount);
            _status.text = $"치트 {totalCount} · 프로퍼티 {propertyCount}";
            if (_tab == 0)
            {
                _menu.Tick();
            }
            else
            {
                _console.Tick();
            }
        }

        private void UpdateHeaderActions()
        {
            if (_clearButton != null)
            {
                bool bShowClear = _hostOrNull != null && _tab == 1;
                _clearButton.style.display = bShowClear ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void Bind(DebuggerKitHost hostOrNull)
        {
            _hostOrNull = hostOrNull;
            _menu?.Bind(hostOrNull);
            _console?.Bind(hostOrNull);
        }

        private static void CountMenuItems(CheatRegistry registry, out int totalCount, out int propertyCount)
        {
            totalCount = 0;
            propertyCount = 0;
            foreach (CheatDefinition definition in registry.Definitions)
            {
                if (!definition.IsShownInMenu)
                {
                    continue;
                }

                totalCount++;
                if (definition.Kind == ECheatKind.Value)
                {
                    propertyCount++;
                }
            }
        }
    }
}
