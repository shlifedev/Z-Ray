using System;
using System.Numerics;

namespace Lumera.ZRay
{
    public sealed class CheatParameter
    {
        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Type Type { get; }
        public IValueType ValueType { get; }
        public bool HasDefault { get; }
        public object? DefaultValue { get; }
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
            const double MAX_DISTANCE = 18446744073709551616.0;
            return new BigInteger(Math.Max(-MAX_DISTANCE, Math.Min(number, MAX_DISTANCE)));
        }

        public string FormatDefault()
        {
            return HasDefault ? ValueType.Format(DefaultValue) : "";
        }
    }
}
