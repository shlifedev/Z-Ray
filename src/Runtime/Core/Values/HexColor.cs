namespace Lumera.ZRay
{
    public static class HexColor
    {
        public const string USAGE = "#RRGGBB 또는 #RRGGBBAA 꼴의 색을 입력하세요. 예: #FF8800, #FF880080";

        private const int SHORT_RGB = 3;
        private const int SHORT_RGBA = 4;
        private const int LONG_RGB = 6;
        private const int LONG_RGBA = 8;
        private const int HEX_BASE = 16;
        private const int SHORT_DIGIT_SCALE = 17;
        private const byte OPAQUE = 255;

        public static bool TryParse(string? text, out byte red, out byte green, out byte blue, out byte alpha)
        {
            red = 0;
            green = 0;
            blue = 0;
            alpha = OPAQUE;
            if (text == null)
            {
                return false;
            }

            int start = text.Length > 0 && text[0] == '#' ? 1 : 0;
            int length = text.Length - start;
            bool bShort = length == SHORT_RGB || length == SHORT_RGBA;
            if (!bShort && length != LONG_RGB && length != LONG_RGBA)
            {
                return false;
            }

            int digits = bShort ? 1 : 2;
            if (!TryReadChannel(text, start, digits, out red) || !TryReadChannel(text, start + digits, digits, out green)
                || !TryReadChannel(text, start + (digits * 2), digits, out blue))
            {
                return false;
            }

            bool bHasAlpha = length == SHORT_RGBA || length == LONG_RGBA;
            return !bHasAlpha || TryReadChannel(text, start + (digits * 3), digits, out alpha);
        }

        public static string Format(byte red, byte green, byte blue, byte alpha)
        {
            return $"#{red:X2}{green:X2}{blue:X2}{alpha:X2}";
        }

        private static bool TryReadChannel(string text, int start, int digits, out byte channel)
        {
            int number = 0;
            for (int index = 0; index < digits; index++)
            {
                int digit = GetDigit(text[start + index]);
                if (digit < 0)
                {
                    channel = 0;
                    return false;
                }

                number = (number * HEX_BASE) + digit;
            }

            channel = (byte)(digits == 1 ? number * SHORT_DIGIT_SCALE : number);
            return true;
        }

        private static int GetDigit(char character)
        {
            if (character >= '0' && character <= '9')
            {
                return character - '0';
            }

            if (character >= 'a' && character <= 'f')
            {
                return character - 'a' + 10;
            }

            return character >= 'A' && character <= 'F' ? character - 'A' + 10 : -1;
        }
    }
}
