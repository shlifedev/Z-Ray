using TMPro;
using UnityEngine;
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

    // 콘솔과 치트 메뉴를 담은 캔버스. 둘 중 하나만 열고, Esc는 열린 창의 맨 위 단계 하나만 닫는다.
    //
    // 픽셀 글꼴을 위해 두 가지를 한다.
    // - 글꼴을 실행할 때 TTF에서 비트맵(래스터) TMP 글꼴로 만든다. 글자는 12픽셀 그대로 굽는다.
    //   에셋으로 두지 않는 것은 동적 아틀라스가 글자를 더할 때마다 에셋이 바뀌어 저장소에 변경이 남기 때문이다.
    // - 배율은 기본이 정수(화면 높이 540마다 1배)다. 정수 배율에서는 아틀라스를 점 필터로 읽어 픽셀이 또렷하다.
    //   사용자가 소수 배율(0.25 단위)을 고르면 픽셀이 고르게 나뉘지 않으므로, 획 굵기가 들쭉날쭉해지지 않게 선형 필터로 바꾼다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ZRayCanvas : MonoBehaviour
    {
        private const int ATLAS_SIZE = 1024;

        [SerializeField] private ConsoleView _console = null;
        [SerializeField] private CheatMenuView _menu = null;
        [SerializeField] private ToastView _toast = null;
        [SerializeField, Tooltip("툴킷 글꼴(갈무리11). 실행할 때 비트맵 TMP 글꼴로 만든다.")] private Font _font = null;
        // 비트맵 글꼴 셰이더. TMP는 이름으로 찾는데, 빌드에 들어가려면 누군가 참조해야 해서 여기서 참조한다.
        [SerializeField] private Shader _bitmapShader = null;

        private ZRayHost _host;
        private CanvasScaler _scaler;
        private TMP_FontAsset _fontAssetOrNull;
        private EDebuggerOverlay _current = EDebuggerOverlay.None;

        public ConsoleView Console => _console;
        public ToastView Toast => _toast;
        public bool IsOpen => _current != EDebuggerOverlay.None;
        public EDebuggerOverlay Current => _current;
        // 지금 적용된 배율
        public float Scale { get; private set; } = 1.0f;

        public void Initialize(ZRayHost host)
        {
            _host = host;
            _scaler = GetComponent<CanvasScaler>();
            _fontAssetOrNull = CreateFontAssetOrNull();
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

        // 프리팹을 인스턴스로 만들고 툴킷 글꼴을 입힌다. 뷰가 줄·행을 만들 때 쓴다.
        public T Create<T>(T prefab, Transform parent) where T : Component
        {
            T instance = Instantiate(prefab, parent, false);
            ApplyFont(instance.gameObject);
            return instance;
        }

        private void Update()
        {
            ApplyScale();
        }

        private void OnDestroy()
        {
            if (_fontAssetOrNull == null)
            {
                return;
            }

            Destroy(_fontAssetOrNull.atlasTexture);
            Destroy(_fontAssetOrNull.material);
            Destroy(_fontAssetOrNull);
            _fontAssetOrNull = null;
        }

        // 호스트가 정한 배율(0이면 화면 높이에 맞춘 자동 정수 배율)을 캔버스에 바로 적용한다. 화면 크기가 바뀔 수 있어 매 프레임 확인한다.
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

            // 정수 배율에서만 글자 픽셀이 화면 픽셀에 딱 맞는다. 그때만 점 필터를 쓴다.
            FilterMode filter = Mathf.Approximately(Scale, Mathf.Round(Scale)) ? FilterMode.Point : FilterMode.Bilinear;
            Texture2D atlas = _fontAssetOrNull.atlasTexture;
            if (atlas.filterMode != filter)
            {
                atlas.filterMode = filter;
            }
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

            // 입력칸은 자기 글꼴을 따로 기억했다가 글자에 다시 입힌다. 먼저 맞춘다.
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
