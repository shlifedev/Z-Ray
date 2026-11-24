using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Lumera.ZRay
{
    // 씬의 치트를 등록하고 실행한다. 캔버스 프리팹을 연결하면 기본 콘솔과 치트 메뉴도 제공한다.
    // 게임은 CurrentOrNull로 찾아 인스턴스 치트를 Register하고, IsBlockingGameInput이 true인 동안 자기 입력을 읽지 않는다.
    // 치트 정의, 입력 기록, 즐겨찾기는 모두 이 컴포넌트가 갖는다. 정적 상태는 CurrentOrNull 하나뿐이고 플레이를 시작할 때마다 비운다.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ZRayHost : MonoBehaviour
    {
        private const string MENU_SOURCE_LABEL = "[메뉴] ";
        private const string SCALE_KEY = "Lumera.ZRay.UiScale.v2";
        // 배율은 0.25 단위다. 0은 자동(화면 높이에 맞춘 정수 배율)이고, 직접 고를 때는 MIN_SCALE~MAX_SCALE다.
        private const float MIN_SCALE = 0.5f;
        private const float MAX_SCALE = 4.0f;
        private const float SCALE_STEP = 0.25f;
        // 트랙패드는 한 번 쓸어도 휠 이벤트가 프레임마다 온다. 배율이 한꺼번에 뛰지 않게 한 단계 사이에 간격을 둔다.
        private const float ZOOM_INTERVAL = 0.1f;

        // 콘솔을 처음 열면 보이는 짧은 안내.
        private const string WELCOME = "Lumera ZRay — help 명령 목록 · props 프로퍼티 목록";

        [SerializeField, Tooltip("기본 콘솔과 치트 메뉴를 사용할 때 연결한다. 커스텀 GUI만 사용할 때는 비워 둔다.")] private ZRayCanvas _canvasPrefab = null;
        [SerializeField, Range(0.0f, MAX_SCALE), Tooltip("화면 배율의 기본값. 0이면 화면 높이 540마다 1배씩 정수로 키운다(픽셀 글꼴이 가장 또렷하다). 플레이 중에 바꾼 배율(zray.scale, Ctrl/⌘+휠)은 PlayerPrefs에 남아 이 값보다 앞선다.")] private float _uiScale = 0.0f;
        [SerializeField] private InputAction _toggleConsole = new InputAction("ToggleConsole", InputActionType.Button, "<Keyboard>/backquote");
        [SerializeField] private InputAction _toggleMenu = new InputAction("ToggleMenu", InputActionType.Button, "<Keyboard>/f1");
        [SerializeField] private InputAction _close = new InputAction("Close", InputActionType.Button, "<Keyboard>/escape");

        private readonly PlayerPrefsStore _store = new PlayerPrefsStore();
        private ZRayCanvas _canvas;
        private bool _bWasBlocking;
        // 배율 조절(Ctrl/⌘ + 휠, +, -, 0). 관례로 굳은 조합이라 Inspector에 내놓지 않고 코드에서 만든다.
        private InputAction _zoomWheel;
        private InputAction _zoomIn;
        private InputAction _zoomOut;
        private InputAction _zoomReset;
        private float _nextZoomAt;

        public static ZRayHost CurrentOrNull { get; private set; }

        public CheatRegistry Registry { get; private set; }
        public ConsoleAssist Assist { get; private set; }
        public CheatExecutor Executor { get; private set; }
        public CommandHistory History { get; private set; }
        public FavoriteSet Favorites { get; private set; }
        // 치트마다 마지막으로 입력한 인자 값. 메뉴, 에디터 창, 콘솔 실행이 함께 쓴다. 저장하지 않아서 플레이가 끝나면 사라진다.
        public CheatDraftStore Drafts { get; private set; }
        // 콘솔 기록. 런타임 콘솔과 에디터 창이 같은 기록을 그린다.
        public ConsoleTranscript Transcript { get; private set; }

        // 콘솔·메뉴가 열려 있을 때 Ctrl/⌘+휠, Ctrl/⌘ +/-로도 바꾸고 Ctrl/⌘+0으로 자동으로 되돌린다. 바꾼 값은 다음 플레이에도 남는다.
        [CheatProperty("zray.scale", Category = "ZRay", Name = "UI 배율", Description = "ZRay 화면 배율(0.25 단위). 0이면 화면 높이에 맞춰 자동 정수 배율. Ctrl/⌘+휠로도 바꾼다.", Order = 0, Min = 0, Max = MAX_SCALE, Step = SCALE_STEP)]
        public float UiScale
        {
            get => _uiScale;
            set
            {
                _uiScale = NormalizeScale(value);
                PlayerPrefs.SetFloat(SCALE_KEY, _uiScale);
            }
        }

        // 여닫는 키의 이름(예: "`", "F1"). 화면의 안내와 버튼에 쓴다.
        public string ConsoleKeyLabel => _toggleConsole.GetBindingDisplayString();
        public string MenuKeyLabel => _toggleMenu.GetBindingDisplayString();

        // 콘솔이나 메뉴가 열려 있다. 이 동안 게임은 키보드와 마우스 입력을 읽지 않아야 한다.
        public bool IsBlockingGameInput => _canvas != null && _canvas.IsOpen;
        public event Action<bool> BlockingChanged;
        // 콘솔이든 메뉴든, 명령을 실행할 때마다 불린다(실패 포함). 게임이 화면을 다시 그리는 데 쓴다.
        public event Action<ExecutionResult> Executed;

        // 도메인 리로드 없이 플레이를 다시 시작하면 이전 플레이의 (이미 파괴된) 호스트가 남아 있을 수 있다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentOrNull = null;
        }

        // 객체의 인스턴스 치트를 등록한다. 돌려받은 핸들을 Dispose하면 빠진다.
        public IDisposable Register(object target)
        {
            return Registry.Register(target);
        }

        // 메뉴가 치트를 실행하는 경로. 콘솔과 같은 실행기를 쓰고, 콘솔 기록에는 출처를 붙여 남긴다.
        public ExecutionResult ExecuteFromMenu(string line)
        {
            return Executor.Execute(line, MENU_SOURCE_LABEL);
        }

        // 커스텀 GUI에서 고른 치트와 입력값을 선언 순서로 넘긴다.
        public ExecutionResult ExecuteFromMenu(CheatDefinition definition, IReadOnlyList<string> argumentTexts)
        {
            return ExecuteFromMenu(CheatExecutor.BuildLine(definition, argumentTexts));
        }

        public void SaveFavorites()
        {
            Favorites.Save(_store);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (CurrentOrNull != null && CurrentOrNull != this)
            {
                Destroy(gameObject);
                return;
            }

            CurrentOrNull = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            History = new CommandHistory();
            History.Load(_store);
            Favorites = new FavoriteSet();
            Favorites.Load(_store);
            Registry = new CheatRegistry(null, static problem => Debug.LogWarning($"[ZRay] {problem}"));
            BuiltInCheats.Register(Registry, History);
            Registry.Register(this);
            Registry.RegisterStatic(GetLoadedAssemblies());
            Assist = new ConsoleAssist(Registry, History);
            Drafts = new CheatDraftStore();
            Executor = new CheatExecutor(Registry, History, Drafts);
            Executor.Executed += OnExecuted;
            Transcript = new ConsoleTranscript();
            Transcript.Write(WELCOME, EConsoleLineKind.Muted);
            if (PlayerPrefs.HasKey(SCALE_KEY))
            {
                _uiScale = NormalizeScale(PlayerPrefs.GetFloat(SCALE_KEY));
            }

            if (_canvasPrefab == null)
            {
                return;
            }

            _zoomWheel = CreateModifiedAction("ZoomWheel", InputActionType.Value, "<Mouse>/scroll/y");
            _zoomIn = CreateModifiedAction("ZoomIn", InputActionType.Button, "<Keyboard>/equals");
            _zoomOut = CreateModifiedAction("ZoomOut", InputActionType.Button, "<Keyboard>/minus");
            _zoomReset = CreateModifiedAction("ZoomReset", InputActionType.Button, "<Keyboard>/0");

            _canvas = Instantiate(_canvasPrefab, transform);
            _canvas.Initialize(this);
        }

        private void OnEnable()
        {
            if (_canvas == null)
            {
                return;
            }

            _toggleConsole.Enable();
            _toggleMenu.Enable();
            _close.Enable();
            EnableZoomActions(true);
        }

        private void OnDisable()
        {
            _toggleConsole.Disable();
            _toggleMenu.Disable();
            _close.Disable();
            EnableZoomActions(false);
            if (_canvas != null)
            {
                _canvas.Show(EDebuggerOverlay.None);
            }

            UpdateBlockingState();
        }

        private void OnDestroy()
        {
            if (CurrentOrNull != this)
            {
                return;
            }

            CurrentOrNull = null;
            Executor.Executed -= OnExecuted;
            History.Save(_store);
            PlayerPrefs.Save();
            _zoomWheel?.Dispose();
            _zoomIn?.Dispose();
            _zoomOut?.Dispose();
            _zoomReset?.Dispose();
        }

        private void Update()
        {
            if (_canvas == null)
            {
                return;
            }

            // 단축키와 Esc는 어느 입력칸에 포커스가 있든 여기서만 처리한다. 입력의 주인이 하나여야 창이 겹쳐 열리지 않는다.
            if (_toggleConsole.WasPressedThisFrame())
            {
                EnsureEventSystem();
                _canvas.Toggle(EDebuggerOverlay.Console);
            }
            else if (_toggleMenu.WasPressedThisFrame())
            {
                EnsureEventSystem();
                _canvas.Toggle(EDebuggerOverlay.Menu);
            }
            else if (_close.WasPressedThisFrame())
            {
                _canvas.HandleEscape();
            }

            UpdateZoom();
            UpdateBlockingState();
        }

        private void UpdateBlockingState()
        {
            bool bBlocking = IsBlockingGameInput;
            if (bBlocking != _bWasBlocking)
            {
                _bWasBlocking = bBlocking;
                BlockingChanged?.Invoke(bBlocking);
            }
        }

        // 콘솔이나 메뉴가 열려 있을 때만 배율을 바꾼다. 닫혀 있을 때의 Ctrl+휠은 게임의 것이다.
        private void UpdateZoom()
        {
            if (!_canvas.IsOpen)
            {
                return;
            }

            if (_zoomReset.WasPressedThisFrame())
            {
                SetScale(0.0f);
                return;
            }

            int step = 0;
            if (_zoomIn.WasPressedThisFrame())
            {
                step = 1;
            }
            else if (_zoomOut.WasPressedThisFrame())
            {
                step = -1;
            }
            else if (_zoomWheel.WasPerformedThisFrame() && Time.unscaledTime >= _nextZoomAt)
            {
                float wheel = _zoomWheel.ReadValue<float>();
                step = wheel > 0.0f ? 1 : (wheel < 0.0f ? -1 : 0);
            }

            if (step != 0)
            {
                _nextZoomAt = Time.unscaledTime + ZOOM_INTERVAL;
                // 자동(0)일 때도 지금 목표 배율에서 한 단계 움직인다.
                SetScale(Mathf.Clamp(_canvas.Scale + (step * SCALE_STEP), MIN_SCALE, MAX_SCALE));
            }
        }

        private void SetScale(float scale)
        {
            UiScale = scale;
            _canvas.ApplyScale();
            _canvas.Toast.Show(_uiScale <= 0.0f ? $"화면 배율 자동 ({_canvas.Scale:0.##}x)" : $"화면 배율 {_canvas.Scale:0.##}x  ·  Ctrl/⌘+0 자동", true);
        }

        // 0.25 단위로 맞춘다. 0에 가까우면 자동(0)이고, 그 밖은 고를 수 있는 범위 안으로 넣는다.
        private static float NormalizeScale(float scale)
        {
            float rounded = Mathf.Round(scale / SCALE_STEP) * SCALE_STEP;
            return rounded < MIN_SCALE * 0.5f ? 0.0f : Mathf.Clamp(rounded, MIN_SCALE, MAX_SCALE);
        }

        // Awake보다 OnEnable이 먼저 불리지는 않지만, 호스트가 중복이라 Awake에서 바로 파괴될 때는 액션이 없다.
        private void EnableZoomActions(bool bEnabled)
        {
            if (_zoomWheel == null)
            {
                return;
            }

            if (bEnabled)
            {
                _zoomWheel.Enable();
                _zoomIn.Enable();
                _zoomOut.Enable();
                _zoomReset.Enable();
            }
            else
            {
                _zoomWheel.Disable();
                _zoomIn.Disable();
                _zoomOut.Disable();
                _zoomReset.Disable();
            }
        }

        // Ctrl 또는 ⌘(Meta)를 누른 채 path를 조작할 때만 동작하는 액션.
        private static InputAction CreateModifiedAction(string name, InputActionType type, string path)
        {
            InputAction action = new InputAction(name, type);
            action.AddCompositeBinding("OneModifier").With("Modifier", "<Keyboard>/ctrl").With("Binding", path);
            action.AddCompositeBinding("OneModifier").With("Modifier", "<Keyboard>/leftMeta").With("Binding", path);
            action.AddCompositeBinding("OneModifier").With("Modifier", "<Keyboard>/rightMeta").With("Binding", path);
            return action;
        }

        private void OnExecuted(ExecutionResult result)
        {
            if (result.ExceptionOrNull != null)
            {
                Debug.LogException(result.ExceptionOrNull, this);
            }

            Transcript.Append(result);
            History.Save(_store);
            Executed?.Invoke(result);
        }

        // Unity 6000.5부터는 AppDomain 대신 Unity의 목록을 쓴다. AppDomain은 이미 내려간 어셈블리까지 돌려줄 수 있다.
        private static IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER
            return UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        // 입력칸과 버튼은 EventSystem이 있어야 동작한다. 씬에 없으면 하나 만든다.
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }
    }
}
