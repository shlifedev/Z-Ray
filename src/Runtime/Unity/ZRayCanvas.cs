using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public enum EDebuggerOverlay
    {
        None,
        Console,
        Menu
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ZRayCanvas : MonoBehaviour
    {
        private const int ATLAS_SIZE = 1024;
        private const float EVENT_SYSTEM_CHECK_INTERVAL = 0.5f;
        private const float CHECKER_CELL = 4.0f;

        [SerializeField] private ConsoleView _console = null;
        [SerializeField] private CheatMenuView _menu = null;
        [SerializeField] private ToastView _toast = null;
        [SerializeField, Tooltip("툴킷 글꼴(갈무리11). 실행할 때 비트맵 TMP 글꼴로 만든다.")] private Font _font = null;
        [SerializeField] private Shader _bitmapShader = null;

        private ZRayHost _host;
        private MultiplayerEventSystem _fallbackEventSystemOrNull;
        private float _nextEventSystemCheckAt;
        private CanvasScaler _scaler;
        private TMP_FontAsset _fontAssetOrNull;
        private Texture2D _checkerOrNull;
        private EDebuggerOverlay _current = EDebuggerOverlay.None;

        public ConsoleView Console => _console;
        public ToastView Toast => _toast;
        public bool IsOpen => _current != EDebuggerOverlay.None;
        public EDebuggerOverlay Current => _current;
        public float Scale { get; private set; } = 1.0f;
        public Texture CheckerTexture => _checkerOrNull;

        public void Initialize(ZRayHost host)
        {
            _host = host;
            _scaler = GetComponent<CanvasScaler>();
            _fontAssetOrNull = CreateFontAssetOrNull();
            _checkerOrNull = CreateChecker();
            ApplyFont(gameObject);
            ApplyScale();
            _console.Initialize(host, this);
            _menu.Initialize(host, this);
            _console.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);
            _toast.Hide();
        }

        public void Toggle(EDebuggerOverlay overlay)
        {
            Show(_current == overlay ? EDebuggerOverlay.None : overlay);
        }

        public void Show(EDebuggerOverlay overlay)
        {
            if (_current == overlay)
            {
                return;
            }

            if (_current == EDebuggerOverlay.Console)
            {
                _console.Close();
            }
            else if (_current == EDebuggerOverlay.Menu)
            {
                _menu.Close();
            }

            _current = overlay;
            UpdateEventSystem();
            if (overlay == EDebuggerOverlay.Console)
            {
                _console.Open();
            }
            else if (overlay == EDebuggerOverlay.Menu)
            {
                _menu.Open();
            }
        }

        public void HandleEscape()
        {
            bool bHandled = false;
            if (_current == EDebuggerOverlay.Console)
            {
                bHandled = _console.HandleEscape();
            }
            else if (_current == EDebuggerOverlay.Menu)
            {
                bHandled = _menu.HandleEscape();
            }

            if (!bHandled)
            {
                Show(EDebuggerOverlay.None);
            }
        }

        public Rect GetCheckerUV(Vector2 size)
        {
            return new Rect(0.0f, 0.0f, size.x / (CHECKER_CELL * 2.0f), size.y / (CHECKER_CELL * 2.0f));
        }

        public T Create<T>(T prefab, Transform parent) where T : Component
        {
            T instance = Instantiate(prefab, parent, false);
            ApplyFont(instance.gameObject);
            return instance;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseFallbackEventSystem();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _nextEventSystemCheckAt = 0.0f;
            UpdateEventSystem();
        }

        private void UpdateEventSystem()
        {
            if (!IsOpen)
            {
                ReleaseFallbackEventSystem();
                return;
            }

            if (EventSystem.current == null)
            {
                GameObject fallback = new GameObject("ZRay EventSystem", typeof(MultiplayerEventSystem), typeof(InputSystemUIInputModule));
                fallback.transform.SetParent(transform, false);
                _fallbackEventSystemOrNull = fallback.GetComponent<MultiplayerEventSystem>();
                _fallbackEventSystemOrNull.playerRoot = gameObject;
                _nextEventSystemCheckAt = Time.unscaledTime + EVENT_SYSTEM_CHECK_INTERVAL;
                return;
            }

            if (_fallbackEventSystemOrNull == null)
            {
                return;
            }

            if (EventSystem.current != _fallbackEventSystemOrNull)
            {
                ReleaseFallbackEventSystem();
                return;
            }

            if (Time.unscaledTime >= _nextEventSystemCheckAt)
            {
                _nextEventSystemCheckAt = Time.unscaledTime + EVENT_SYSTEM_CHECK_INTERVAL;
                foreach (EventSystem candidate in FindObjectsByType<EventSystem>())
                {
                    if (candidate != _fallbackEventSystemOrNull && candidate.isActiveAndEnabled)
                    {
                        ReleaseFallbackEventSystem();
                        return;
                    }
                }
            }
        }

        private void ReleaseFallbackEventSystem()
        {
            if (_fallbackEventSystemOrNull == null)
            {
                return;
            }

            GameObject fallback = _fallbackEventSystemOrNull.gameObject;
            GameObject selected = _fallbackEventSystemOrNull.currentSelectedGameObject;
            _fallbackEventSystemOrNull = null;
            if (fallback.activeInHierarchy)
            {
                fallback.SetActive(false);
            }

            Destroy(fallback);
            if (IsOpen && EventSystem.current != null && selected != null && selected.transform.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(selected);
            }
        }

        private void Update()
        {
            UpdateEventSystem();
            ApplyScale();
        }

        private void OnDestroy()
        {
            Destroy(_checkerOrNull);
            _checkerOrNull = null;
            if (_fontAssetOrNull == null)
            {
                return;
            }

            Destroy(_fontAssetOrNull.atlasTexture);
            Destroy(_fontAssetOrNull.material);
            Destroy(_fontAssetOrNull);
            _fontAssetOrNull = null;
        }

        public void ApplyScale()
        {
            Scale = _host.UiScale > 0.0f ? _host.UiScale : Mathf.Max(1, Mathf.FloorToInt(Screen.height / ZRayTheme.SCALE_UNIT_HEIGHT));
            if (!Mathf.Approximately(_scaler.scaleFactor, Scale))
            {
                _scaler.scaleFactor = Scale;
            }

            if (_fontAssetOrNull == null)
            {
                return;
            }

            FilterMode filter = Mathf.Approximately(Scale, Mathf.Round(Scale)) ? FilterMode.Point : FilterMode.Bilinear;
            Texture2D atlas = _fontAssetOrNull.atlasTexture;
            if (atlas.filterMode != filter)
            {
                atlas.filterMode = filter;
            }
        }

        private static Texture2D CreateChecker()
        {
            Texture2D checker = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "ZRayChecker",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            checker.SetPixels32(new Color32[] { ZRayTheme.CHECKER_LIGHT, ZRayTheme.CHECKER_DARK, ZRayTheme.CHECKER_DARK, ZRayTheme.CHECKER_LIGHT });
            checker.Apply(false, true);
            return checker;
        }

        private TMP_FontAsset CreateFontAssetOrNull()
        {
            if (_font == null || _bitmapShader == null)
            {
                Debug.LogWarning("[ZRay] 캔버스 프리팹에 글꼴이나 셰이더가 없어 TMP 기본 글꼴을 씁니다. Tools/Lumera/ZRay/Build Prefabs를 다시 실행하세요.", this);
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(_font, ZRayTheme.FONT_SAMPLING_SIZE, 2, GlyphRenderMode.RASTER, ATLAS_SIZE, ATLAS_SIZE, AtlasPopulationMode.Dynamic, false);
            if (fontAsset == null)
            {
                Debug.LogWarning("[ZRay] 툴킷 글꼴로 TMP 글꼴을 만들지 못해 TMP 기본 글꼴을 씁니다.", this);
                return null;
            }

            fontAsset.name = "ZRay Galmuri11 (Runtime)";
            fontAsset.material.shader = _bitmapShader;
            fontAsset.atlasTexture.filterMode = FilterMode.Point;
            return fontAsset;
        }

        private void ApplyFont(GameObject root)
        {
            if (_fontAssetOrNull == null)
            {
                return;
            }

            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                input.fontAsset = _fontAssetOrNull;
                input.pointSize = ZRayTheme.FONT_SIZE;
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = _fontAssetOrNull;
                text.fontSharedMaterial = _fontAssetOrNull.material;
            }
        }
    }
}
