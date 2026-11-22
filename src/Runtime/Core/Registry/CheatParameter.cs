using System;

namespace Lumera.DebuggerKit
{
    // 치트 인자 하나. 값 치트는 "value" 인자 하나를 갖는다(생략하면 읽기, 주면 쓰기).
    // 글자를 값으로 바꾸고 범위를 확인하는 일(TryParse)은 여기서만 한다. 콘솔과 메뉴가 같은 검증을 쓴다.
    public sealed class CheatParameter
    {
        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Type Type { get; }
        public IValueType ValueType { get; }
        public bool HasDefault { get; }
        public object? DefaultValue { get; }
        // 기본값이 없어도 생략할 수 있는 인자(값 치트의 value)는 false다.
        public bool IsRequired { get; }
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }
        public ISuggestionProvider? SuggestionProviderOrNull { get; }

        public bool HasMin => !double.IsNaN(Min);
        public bool HasMax => !double.IsNaN(Max);
        public bool HasStep => !double.IsNaN(Step);
        public bool IsNumber => ValueTypeRegistry.IsNumber(Type);

        internal CheatParameter(string name, string displayName, string description, Type type, IValueType valueType,
            bool bHasDefault, object? defaultValue, bool bRequired, double min, double max, double step, ISuggestionProvider? suggestionProviderOrNull)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            Type = type;
            ValueType = valueType;
            HasDefault = bHasDefault;
            DefaultValue = defaultValue;
            IsRequired = bRequired;
            Min = min;
            Max = max;
            Step = step;
            SuggestionProviderOrNull = suggestionProviderOrNull;
        }

        public bool TryParse(string text, out object? value, out string? errorOrNull)
        {
            if (!ValueType.TryParse(text, out value, out errorOrNull))
            {
                return false;
            }

            return IsInRange(value, out errorOrNull);
        }

        public bool IsInRange(object? value, out string? errorOrNull)
        {
            errorOrNull = null;
            if (!IsNumber || value == null || (!HasMin && !HasMax))
            {
                return true;
            }

            double number = Convert.ToDouble(value);
            if ((HasMin && number < Min) || (HasMax && number > Max))
            {
                errorOrNull = $"{CheatFormatter.FormatRange(this)} 사이의 값을 입력하세요.";
                return false;
            }

            return true;
        }

        // 숫자 인자의 ± 한 칸. 지금 값에 Step(없으면 1)을 더하거나 빼고 범위 안으로 맞춘 글자를 돌려준다.
        // 지금 값이 숫자가 아니면 하한(없으면 0)에서 시작한다. 숫자 인자가 아니면 글자를 그대로 돌려준다.
        public string StepText(string text, int direction)
        {
            if (!IsNumber)
            {
                return text;
            }

            double current = ValueType.TryParse(text ?? "", out object? parsed, out _) && parsed != null ? Convert.ToDouble(parsed) : (HasMin ? Min : 0.0);
            double next = current + (direction * (HasStep ? Step : 1.0));
            if (HasMin && next < Min)
            {
                next = Min;
            }

            if (HasMax && next > Max)
            {
                next = Max;
            }

            // 정수 인자는 소수 Step이어도 정수로 반올림한다.
            object value = Type == typeof(int) || Type == typeof(long) ? Convert.ChangeType(Math.Round(next), Type) : Convert.ChangeType(next, Type);
            return ValueType.Format(value);
        }

        public string FormatDefault()
        {
            return HasDefault ? ValueType.Format(DefaultValue) : "";
        }
    }
}
