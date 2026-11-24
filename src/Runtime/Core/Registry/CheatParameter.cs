using System;
using System.Numerics;

namespace Lumera.ZRay
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

            bool bInRange;
            if (value is long integer)
            {
                GetIntegerLimits(out BigInteger minimum, out BigInteger maximum);
                bInRange = integer >= minimum && integer <= maximum;
            }
            else
            {
                double number = Convert.ToDouble(value);
                bInRange = !(HasMin && number < Min || HasMax && number > Max);
            }

            if (!bInRange)
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

            bool bParsed = ValueType.TryParse(text ?? "", out object? parsed, out _) && parsed != null;
            double offset = direction == 0 ? 0.0 : direction * (HasStep ? Step : 1.0);
            if (Type == typeof(int) || Type == typeof(long))
            {
                GetIntegerLimits(out BigInteger minimum, out BigInteger maximum);
                if (minimum > maximum)
                {
                    return text ?? "";
                }

                BigInteger currentInteger = bParsed ? Convert.ToInt64(parsed) : (HasMin ? minimum : BigInteger.Zero);
                BigInteger nextInteger = currentInteger + ToWholeNumber(offset);
                // 정수부를 먼저 더해야 큰 long에서도 1 단위와 중간값의 짝수 반올림을 지킬 수 있다.
                double fraction = offset % 1.0;
                if (Math.Abs(fraction) > 0.5 || (Math.Abs(fraction) == 0.5 && !nextInteger.IsEven))
                {
                    nextInteger += Math.Sign(fraction);
                }

                nextInteger = BigInteger.Max(minimum, BigInteger.Min(nextInteger, maximum));
                return Type == typeof(int) ? ValueType.Format((int)nextInteger) : ValueType.Format((long)nextInteger);
            }

            double current = bParsed ? Convert.ToDouble(parsed) : (HasMin ? Min : 0.0);
            double next = current + offset;
            if (HasMin && next < Min)
            {
                next = Min;
            }

            if (HasMax && next > Max)
            {
                next = Max;
            }

            double limit = Type == typeof(float) ? float.MaxValue : double.MaxValue;
            return ValueType.Format(Convert.ChangeType(Math.Max(-limit, Math.Min(next, limit)), Type));
        }

        private void GetIntegerLimits(out BigInteger minimum, out BigInteger maximum)
        {
            minimum = Type == typeof(int) ? int.MinValue : long.MinValue;
            maximum = Type == typeof(int) ? int.MaxValue : long.MaxValue;
            if (HasMin)
            {
                minimum = BigInteger.Max(minimum, ToWholeNumber(Math.Ceiling(Min)));
            }

            if (HasMax)
            {
                maximum = BigInteger.Min(maximum, ToWholeNumber(Math.Floor(Max)));
            }
        }

        private static BigInteger ToWholeNumber(double number)
        {
            // 2^64만큼 이동하면 어느 long에서 시작해도 타입의 끝에 닿는다. 무한대도 이 범위로 제한한다.
            const double MAX_DISTANCE = 18446744073709551616.0;
            return new BigInteger(Math.Max(-MAX_DISTANCE, Math.Min(number, MAX_DISTANCE)));
        }

        public string FormatDefault()
        {
            return HasDefault ? ValueType.Format(DefaultValue) : "";
        }
    }
}
