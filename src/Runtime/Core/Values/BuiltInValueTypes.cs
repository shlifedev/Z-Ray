using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lumera.DebuggerKit
{
    internal sealed class StringValueType : IValueType
    {
        public string Label => "string";

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            value = text;
            errorOrNull = null;
            return true;
        }

        public string Format(object? value)
        {
            return value as string ?? "";
        }

        public void AddCandidates(List<string> candidates)
        {
        }
    }

    internal sealed class BoolValueType : IValueType
    {
        public string Label => "bool";

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            value = null;
            errorOrNull = null;
            switch (text.ToLowerInvariant())
            {
                case "true":
                case "on":
                case "yes":
                case "1":
                    value = true;
                    return true;
                case "false":
                case "off":
                case "no":
                case "0":
                    value = false;
                    return true;
                default:
                    errorOrNull = "true 또는 false를 입력하세요.";
                    return false;
            }
        }

        public string Format(object? value)
        {
            return value is bool bValue && bValue ? "true" : "false";
        }

        public void AddCandidates(List<string> candidates)
        {
            candidates.Add("true");
            candidates.Add("false");
        }
    }

    internal sealed class IntValueType : IValueType
    {
        public string Label => "int";

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                value = number;
                errorOrNull = null;
                return true;
            }

            value = null;
            errorOrNull = "정수를 입력하세요.";
            return false;
        }

        public string Format(object? value)
        {
            return value is int number ? number.ToString(CultureInfo.InvariantCulture) : "";
        }

        public void AddCandidates(List<string> candidates)
        {
        }
    }

    internal sealed class LongValueType : IValueType
    {
        public string Label => "long";

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
            {
                value = number;
                errorOrNull = null;
                return true;
            }

            value = null;
            errorOrNull = "정수를 입력하세요.";
            return false;
        }

        public string Format(object? value)
        {
            return value is long number ? number.ToString(CultureInfo.InvariantCulture) : "";
        }

        public void AddCandidates(List<string> candidates)
        {
        }
    }

    internal sealed class FloatValueType : IValueType
    {
        public string Label => "float";

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && !float.IsNaN(number) && !float.IsInfinity(number))
            {
                value = number;
                errorOrNull = null;
                return true;
            }

            value = null;
            errorOrNull = "숫자를 입력하세요.";
            return false;
        }

        public string Format(object? value)
        {
            return value is float number ? number.ToString("R", CultureInfo.InvariantCulture) : "";
        }

        public void AddCandidates(List<string> candidates)
        {
        }
    }

    internal sealed class DoubleValueType : IValueType
    {
        public string Label => "double";

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                value = number;
                errorOrNull = null;
                return true;
            }

            value = null;
            errorOrNull = "숫자를 입력하세요.";
            return false;
        }

        public string Format(object? value)
        {
            return value is double number ? number.ToString("R", CultureInfo.InvariantCulture) : "";
        }

        public void AddCandidates(List<string> candidates)
        {
        }
    }

    // 이름으로만 읽는다(대소문자 무시). 숫자를 받으면 선언되지 않은 값이 조용히 들어갈 수 있어서 받지 않는다.
    // [Flags] 열거형은 값 하나로 나타낼 수 없어서 ValueTypeRegistry가 거부한다.
    internal sealed class EnumValueType : IValueType
    {
        private readonly Type _enumType;
        private readonly string[] _names;

        public string Label => _enumType.Name;

        public EnumValueType(Type enumType)
        {
            _enumType = enumType;
            _names = Enum.GetNames(enumType);
        }

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            for (int index = 0; index < _names.Length; index++)
            {
                if (string.Equals(_names[index], text, StringComparison.OrdinalIgnoreCase))
                {
                    value = Enum.Parse(_enumType, _names[index]);
                    errorOrNull = null;
                    return true;
                }
            }

            value = null;
            errorOrNull = $"{string.Join(", ", _names)} 중 하나를 입력하세요.";
            return false;
        }

        public string Format(object? value)
        {
            return value == null ? "" : value.ToString();
        }

        public void AddCandidates(List<string> candidates)
        {
            candidates.AddRange(_names);
        }
    }
}
