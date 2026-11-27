using UnityEngine;

namespace Lumera.ZRay
{
    public static class ZRayTheme
    {
        public const int FONT_SIZE = 12;
        public const int FONT_SAMPLING_SIZE = 12;
        public const float LINE_HEIGHT = 18.0f;
        public const float ROW_HEIGHT = 22.0f;
        public const float MENU_ROW_HEIGHT = 28.0f;
        public const float GROUP_HEIGHT = 24.0f;
        public const float WIDGET_HEIGHT = 20.0f;
        public const int MAX_VISIBLE_SUGGESTIONS = 8;
        public const float SCALE_UNIT_HEIGHT = 540.0f;

        public const string HEX_TEXT = "#F2F5FAFF";
        public const string HEX_MUTED = "#A6B2C7FF";
        public const string HEX_ACCENT = "#7EC2FFFF";
        public const string HEX_MATCH = "#FFD873FF";
        public const string HEX_ERROR = "#FF867DFF";
        public const string HEX_GHOST = "#8593ABFF";
        public const string HEX_INVISIBLE = "#00000000";

        public static readonly Color TEXT = new Color32(244, 247, 252, 255);
        public static readonly Color MUTED = new Color32(174, 186, 208, 255);
        public static readonly Color PLACEHOLDER = new Color32(148, 162, 186, 255);
        public static readonly Color ACCENT = new Color32(126, 194, 255, 255);
        public static readonly Color STAR = new Color32(255, 216, 115, 255);
        public static readonly Color SUCCESS = new Color32(130, 238, 158, 255);
        public static readonly Color WARNING = new Color32(255, 212, 110, 255);
        public static readonly Color ERROR = new Color32(255, 134, 125, 255);
        public static readonly Color ECHO = new Color32(162, 210, 255, 255);

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

        public static readonly Color RUN = new Color32(38, 88, 54, 255);
        public static readonly Color RUN_TEXT = new Color32(178, 255, 196, 255);
        public static readonly Color RUN_FOCUS = new Color32(64, 150, 88, 255);
        public static readonly Color EDIT = new Color32(37, 69, 112, 255);
        public static readonly Color EDIT_TEXT = new Color32(194, 222, 255, 255);
        public static readonly Color BUTTON = new Color32(46, 54, 70, 255);
        public static readonly Color BUTTON_TEXT = new Color32(222, 228, 240, 255);
        public static readonly Color SWITCH_OFF = new Color32(58, 66, 82, 255);
        public static readonly Color SWITCH_ON = new Color32(64, 140, 236, 255);
        public static readonly Color KNOB = new Color32(242, 245, 250, 255);
        public static readonly Color32 CHECKER_LIGHT = new Color32(204, 204, 204, 255);
        public static readonly Color32 CHECKER_DARK = new Color32(140, 140, 140, 255);
        public static readonly Color CURSOR = new Color32(255, 255, 255, 255);
        public static readonly Color CURSOR_EDGE = new Color32(0, 0, 0, 255);

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
