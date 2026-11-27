using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumera.ZRay
{
    public sealed class ColorValueType : IValueType
    {
        private readonly bool _bBytes;

        public string Label => "color";

        private ColorValueType(bool bBytes)
        {
            _bBytes = bBytes;
        }

        public static void Register(ValueTypeRegistry registry)
        {
            registry.Register(typeof(Color), new ColorValueType(false));
            registry.Register(typeof(Color32), new ColorValueType(true));
        }

        public static bool IsColor(Type type)
        {
            return type == typeof(Color) || type == typeof(Color32);
        }

        public static bool TryParseColor(string text, out Color32 color)
        {
            bool bParsed = HexColor.TryParse(text, out byte red, out byte green, out byte blue, out byte alpha);
            color = new Color32(red, green, blue, alpha);
            return bParsed;
        }

        public static string FormatColor(Color32 color)
        {
            return HexColor.Format(color.r, color.g, color.b, color.a);
        }

        public bool TryParse(string text, out object value, out string errorOrNull)
        {
            if (!TryParseColor(text, out Color32 color))
            {
                value = null;
                errorOrNull = HexColor.USAGE;
                return false;
            }

            errorOrNull = null;
            if (_bBytes)
            {
                value = color;
            }
            else
            {
                value = (Color)color;
            }

            return true;
        }

        public string Format(object value)
        {
            if (value is Color color)
            {
                return FormatColor(color);
            }

            return value is Color32 bytes ? FormatColor(bytes) : "";
        }

        public void AddCandidates(List<string> candidates)
        {
        }
    }
}
