using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumera.ZRay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ZRayHost : MonoBehaviour
    {
        private const string MENU_SOURCE_LABEL = "[메뉴] ";
        private const string SCALE_KEY = "Lumera.ZRay.UiScale.v2";
        private const float MIN_SCALE = 0.5f;
        private const float MAX_SCALE = 4.0f;
        private const float SCALE_STEP = 0.25f;
        private const float ZOOM_INTERVAL = 0.1f;

        private const string WELCOME = "Lumera ZRay — help 명령 목록 · props 프로퍼티 목록";

        [SerializeField, Tooltip("기본 콘솔과 치트 메뉴를 사용할 때 연결한다. 커스텀 GUI만 사용할 때는 비워 둔다.")] private ZRayCanvas _canvasPrefab = null;
        [SerializeField, Range(0.0f, MAX_SCALE), Tooltip("화면 배율의 기본값. 0이면 화면 높이 540마다 1배씩 정수로 키운다(픽셀 글꼴이 가장 또렷하다). 플레이 중에 바꾼 배율(zray.scale, Ctrl/⌘+휠)은 PlayerPrefs에 남아 이 값보다 앞선다.")] private float _uiScale = 0.0f;
        [SerializeField] private InputAction _toggleConsole = new InputAction("ToggleConsole", InputActionType.Button, "<Keyboard>/backquote");
        [SerializeField] private InputAction _toggleMenu = new InputAction("ToggleMenu", InputActionType.Button, "<Keyboard>/f1");
        [SerializeField] private InputAction _close = new InputAction("Close", InputActionType.Button, "<Keyboard>/escape");

        private readonly PlayerPrefsStore _store = new PlayerPrefsStore();
        private ZRayCanvas _canvas;
        private bool _bWasBlocking;
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
        public CheatDraftStore Drafts { get; private set; }
        public ConsoleTranscript Transcript { get; private set; }

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

        public string ConsoleKeyLabel => _toggleConsole.GetBindingDisplayString();
        public string MenuKeyLabel => _toggleMenu.GetBindingDisplayString();

        public bool IsBlockingGameInput => _canvas != null && _canvas.IsOpen;
        public event Action<bool> BlockingChanged;
        public event Action<ExecutionResult> Executed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentOrNull = null;
        }

        public IDisposable Register(object target)
        {
            return Registry.Register(target);
        }

        public ExecutionResult ExecuteFromMenu(string line)
        {
            return Executor.Execute(line, MENU_SOURCE_LABEL);
        }

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

            if (_toggleConsole.WasPressedThisFrame())
            {
                _canvas.Toggle(EDebuggerOverlay.Console);
            }
            else if (_toggleMenu.WasPressedThisFrame())
            {
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
                SetScale(Mathf.Clamp(_canvas.Scale + (step * SCALE_STEP), MIN_SCALE, MAX_SCALE));
            }
        }

        private void SetScale(float scale)
        {
            UiScale = scale;
            _canvas.ApplyScale();
            _canvas.Toast.Show(_uiScale <= 0.0f ? $"화면 배율 자동 ({_canvas.Scale:0.##}x)" : $"화면 배율 {_canvas.Scale:0.##}x  ·  Ctrl/⌘+0 자동", true);
        }

        private static float NormalizeScale(float scale)
        {
            float rounded = Mathf.Round(scale / SCALE_STEP) * SCALE_STEP;
            return rounded < MIN_SCALE * 0.5f ? 0.0f : Mathf.Clamp(rounded, MIN_SCALE, MAX_SCALE);
        }

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

        private static IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER
            return UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }
    }
}
