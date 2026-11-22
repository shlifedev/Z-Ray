using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    // C# 타입과 IValueType을 잇는다. 여기에 없는 타입을 쓰는 치트는 등록할 때 거부된다.
    // 새 타입(예: 좌표 구조체)은 치트를 등록하기 전에 Register로 더한다.
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

        // 열거형은 처음 찾을 때 만들어 둔다. [Flags] 열거형은 지원하지 않는다.
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
