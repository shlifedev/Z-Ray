using UnityEngine;

namespace Lumera.DebuggerKit
{
    // 디버거 킷 화면의 색과 크기. 프리팹 빌더와 런타임 뷰가 같은 값을 쓰도록 한 곳에 둔다.
    //
    // 글꼴은 갈무리11(픽셀 글꼴)이다. 12픽셀에서 글자가 정확히 픽셀 격자에 맞는다.
    // 캔버스는 정수 배율로만 키우므로(DebuggerKitCanvas) 크기와 위치는 모두 정수로 둔다.
    // 프로젝트가 선형 색 공간이라 반투명 회색·테두리는 체감 대비가 크게 떨어진다.
    // 면과 테두리는 불투명 색으로 두고, 글자는 어두운 배경에서도 또렷하게 읽히도록 높은 명도로 맞춘다.
    public static class DebuggerKitTheme
    {
        public const int FONT_SIZE = 12;
        public const int FONT_SAMPLING_SIZE = 12;
        public const float LINE_HEIGHT = 18.0f;
        public const float ROW_HEIGHT = 22.0f;
        public const float MENU_ROW_HEIGHT = 28.0f;
        public const float GROUP_HEIGHT = 24.0f;
        public const float WIDGET_HEIGHT = 20.0f;
        public const int MAX_VISIBLE_SUGGESTIONS = 8;
        // 캔버스 배율을 정하는 기준 높이. 화면 높이가 이 값의 몇 배인지(내림)가 배율이다(최소 1).
        public const float SCALE_UNIT_HEIGHT = 540.0f;

        // TMP 리치 텍스트 태그에 넣는 색(#RRGGBBAA).
        public const string HEX_TEXT = "#F2F5FAFF";
        public const string HEX_MUTED = "#A6B2C7FF";
        public const string HEX_ACCENT = "#7EC2FFFF";
        public const string HEX_MATCH = "#FFD873FF";
        public const string HEX_ERROR = "#FF867DFF";
        public const string HEX_GHOST = "#8593ABFF";
        public const string HEX_INVISIBLE = "#00000000";

        // 글자 (선형 색 공간의 1px 픽셀 글꼴에서도 바로 눈에 들어오도록 고대비로 둔다)
        public static readonly Color TEXT = new Color32(244, 247, 252, 255);
        public static readonly Color MUTED = new Color32(174, 186, 208, 255);
        public static readonly Color PLACEHOLDER = new Color32(148, 162, 186, 255);
        public static readonly Color ACCENT = new Color32(126, 194, 255, 255);
        public static readonly Color STAR = new Color32(255, 216, 115, 255);
        public static readonly Color SUCCESS = new Color32(130, 238, 158, 255);
        public static readonly Color WARNING = new Color32(255, 212, 110, 255);
        public static readonly Color ERROR = new Color32(255, 134, 125, 255);
        public static readonly Color ECHO = new Color32(162, 210, 255, 255);

        // 면과 선 (불투명 색 계층으로 구분한다)
        public static readonly Color PANEL = new Color32(17, 20, 27, 255);
        public static readonly Color SURFACE = new Color32(13, 16, 22, 255);
        public static readonly Color HEADER = new Color32(26, 31, 42, 255);
        public static readonly Color FIELD = new Color32(10, 12, 18, 255);
        public static readonly Color HINT_BG = new Color32(22, 26, 36, 255);
        public static readonly Color SIDEBAR = new Color32(21, 25, 34, 255);
        public static readonly Color GROUP = new Color32(31, 37, 50, 255);
        public static readonly Color POPUP = new Color32(23, 28, 38, 255);
        public static readonly Color BADGE = new Color32(31, 39, 54, 255);
        public static readonly Color ECHO_BG = new Color32(24, 34, 50, 255);
        public static readonly Color LINE = new Color32(40, 47, 62, 255);
        public static readonly Color BORDER = new Color32(66, 78, 100, 255);
        public static readonly Color BORDER_ACCENT = new Color32(94, 152, 224, 255);
        public static readonly Color DIM = new Color32(0, 0, 0, 150);
        public static readonly Color TRANSPARENT = new Color32(0, 0, 0, 0);
        public static readonly Color ROW_HOVER = new Color32(32, 38, 52, 255);
        public static readonly Color ROW_SELECTED = new Color32(43, 74, 117, 255);

        // 버튼: 실행은 초록, 입력·값 바꾸기는 파랑, 그 밖은 슬레이트 회색.
        public static readonly Color RUN = new Color32(38, 88, 54, 255);
        public static readonly Color RUN_TEXT = new Color32(178, 255, 196, 255);
        // 키보드 포커스가 [실행] 버튼에 있을 때(Space로 실행)
        public static readonly Color RUN_FOCUS = new Color32(64, 150, 88, 255);
        public static readonly Color EDIT = new Color32(37, 69, 112, 255);
        public static readonly Color EDIT_TEXT = new Color32(194, 222, 255, 255);
        public static readonly Color BUTTON = new Color32(46, 54, 70, 255);
        public static readonly Color BUTTON_TEXT = new Color32(222, 228, 240, 255);
        public static readonly Color SWITCH_OFF = new Color32(58, 66, 82, 255);
        public static readonly Color SWITCH_ON = new Color32(64, 140, 236, 255);
        public static readonly Color KNOB = new Color32(242, 245, 250, 255);

        public static Color GetLineColor(EConsoleLineKind kind)
        {
            switch (kind)
            {
                case EConsoleLineKind.Echo:
                    return ECHO;
                case EConsoleLineKind.Muted:
                    return MUTED;
                case EConsoleLineKind.Success:
                    return SUCCESS;
                case EConsoleLineKind.Warning:
                    return WARNING;
                case EConsoleLineKind.Error:
                    return ERROR;
                default:
                    return TEXT;
            }
        }

        public static string GetHex(ETextStyle style)
        {
            switch (style)
            {
                case ETextStyle.Muted:
                    return HEX_MUTED;
                case ETextStyle.Accent:
                    return HEX_ACCENT;
                case ETextStyle.Error:
                    return HEX_ERROR;
                default:
                    return HEX_TEXT;
            }
        }
    }
}
