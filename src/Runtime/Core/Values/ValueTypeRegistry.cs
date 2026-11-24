using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public sealed class ValueTypeRegistry
    {
        private readonly Dictionary<Type, IValueType> _types = new Dictionary<Type, IValueType>();

        public ValueTypeRegistry()
        {
            Register(typeof(string), new StringValueType());
            Register(typeof(bool), new BoolValueType());
            Register(typeof(int), new IntValueType());
            Register(typeof(long), new LongValueType());
            Register(typeof(float), new FloatValueType());
            Register(typeof(double), new DoubleValueType());
        }

        public void Register(Type type, IValueType valueType)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (valueType == null)
            {
                throw new ArgumentNullException(nameof(valueType));
            }

            _types[type] = valueType;
        }

        public IValueType? FindOrNull(Type type)
        {
            if (_types.TryGetValue(type, out IValueType valueType))
            {
                return valueType;
            }

            if (!type.IsEnum || type.IsDefined(typeof(FlagsAttribute), false))
            {
                return null;
            }

            EnumValueType enumType = new EnumValueType(type);
            _types.Add(type, enumType);
            return enumType;
        }

        internal static bool IsNumber(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double);
        }
    }
}
