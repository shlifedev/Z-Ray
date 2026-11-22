using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lumera.DebuggerKit
{
    // 리플렉션으로 [Cheat]·[CheatProperty] 선언을 읽어 정의를 만든다.
    // 지원하지 않는 선언(타입, 반환형, 공급자)은 정의를 만들지 않고 이유를 알린다. 잘못 붙인 특성이 조용히 사라지지 않게 하려는 것이다.
    internal sealed class CheatScanner
    {
        private readonly ValueTypeRegistry _valueTypes;
        private readonly Action<string> _reportProblem;
        private readonly Dictionary<Type, ISuggestionProvider> _providers = new Dictionary<Type, ISuggestionProvider>();

        public CheatScanner(ValueTypeRegistry valueTypes, Action<string> reportProblem)
        {
            _valueTypes = valueTypes;
            _reportProblem = reportProblem;
        }

        // targetOrNull이 null이면 정적 멤버를, 아니면 그 객체의 인스턴스 멤버를 읽는다.
        public void Scan(Type type, object? targetOrNull, List<CheatDefinition> output)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (targetOrNull == null ? BindingFlags.Static | BindingFlags.DeclaredOnly : BindingFlags.Instance);
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                CheatAttribute? attribute = method.GetCustomAttribute<CheatAttribute>();
                if (attribute != null)
                {
                    TryAdd(CreateActionOrNull(method, attribute, targetOrNull), output);
                }
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                CheatPropertyAttribute? attribute = property.GetCustomAttribute<CheatPropertyAttribute>();
                if (attribute != null)
                {
                    TryAdd(CreatePropertyValueOrNull(property, attribute, targetOrNull), output);
                }
            }

            foreach (FieldInfo field in type.GetFields(flags))
            {
                CheatPropertyAttribute? attribute = field.GetCustomAttribute<CheatPropertyAttribute>();
                if (attribute != null)
                {
                    TryAdd(CreateFieldValueOrNull(field, attribute, targetOrNull), output);
                }
            }
        }

        private static void TryAdd(CheatDefinition? definitionOrNull, List<CheatDefinition> output)
        {
            if (definitionOrNull != null)
            {
                output.Add(definitionOrNull);
            }
        }

        private CheatDefinition? CreateActionOrNull(MethodInfo method, CheatAttribute attribute, object? targetOrNull)
        {
            string where = Describe(method.DeclaringType, method.Name);
            if (!IsValidId(attribute.Id, where))
            {
                return null;
            }

            Type returnType = method.ReturnType;
            if (returnType != typeof(void) && returnType != typeof(string) && returnType != typeof(CheatResult))
            {
                _reportProblem($"{where}: 치트 반환형은 void, string, CheatResult만 된다({returnType.Name}).");
                return null;
            }

            ParameterInfo[] parameterInfos = method.GetParameters();
            CheatParameter[] parameters = new CheatParameter[parameterInfos.Length];
            for (int index = 0; index < parameterInfos.Length; index++)
            {
                CheatParameter? parameter = CreateParameterOrNull(parameterInfos[index], where);
                if (parameter == null)
                {
                    return null;
                }

                parameters[index] = parameter;
            }

            Func<object?[], CheatResult> invoker = arguments => ToResult(method.Invoke(targetOrNull, arguments));
            return CheatDefinition.CreateAction(attribute, parameters, targetOrNull, invoker);
        }

        private CheatParameter? CreateParameterOrNull(ParameterInfo info, string where)
        {
            IValueType? valueType = _valueTypes.FindOrNull(info.ParameterType);
            if (valueType == null)
            {
                _reportProblem($"{where}: 인자 '{info.Name}'의 타입 {info.ParameterType.Name}을 지원하지 않는다. ValueTypeRegistry.Register로 더할 수 있다.");
                return null;
            }

            ArgAttribute? arg = info.GetCustomAttribute<ArgAttribute>();
            bool bHasDefault = info.HasDefaultValue;
            object? defaultValue = bHasDefault ? NormalizeDefault(info.ParameterType, info.DefaultValue) : null;
            if (!TryGetProvider(info.GetCustomAttribute<SuggestAttribute>(), where, out ISuggestionProvider? provider))
            {
                return null;
            }

            return new CheatParameter(info.Name, arg?.Name ?? info.Name, arg?.Description ?? "", info.ParameterType, valueType,
                bHasDefault, defaultValue, !bHasDefault, arg?.Min ?? double.NaN, arg?.Max ?? double.NaN, arg?.Step ?? double.NaN, provider);
        }

        private CheatDefinition? CreatePropertyValueOrNull(PropertyInfo property, CheatPropertyAttribute attribute, object? targetOrNull)
        {
            string where = Describe(property.DeclaringType, property.Name);
            MethodInfo? getter = property.GetGetMethod(true);
            if (getter == null || property.GetIndexParameters().Length > 0)
            {
                _reportProblem($"{where}: 값 치트는 getter가 있고 인덱서가 아닌 프로퍼티여야 한다.");
                return null;
            }

            CheatParameter? parameter = CreateValueParameterOrNull(property.PropertyType, attribute, property, where);
            if (!IsValidId(attribute.Id, where) || parameter == null)
            {
                return null;
            }

            Action<object?>? setterOrNull = null;
            if (property.GetSetMethod(true) != null)
            {
                setterOrNull = value => property.SetValue(targetOrNull, value);
            }

            return CheatDefinition.CreateValue(attribute, parameter, targetOrNull, () => property.GetValue(targetOrNull), setterOrNull);
        }

        private CheatDefinition? CreateFieldValueOrNull(FieldInfo field, CheatPropertyAttribute attribute, object? targetOrNull)
        {
            string where = Describe(field.DeclaringType, field.Name);
            CheatParameter? parameter = CreateValueParameterOrNull(field.FieldType, attribute, field, where);
            if (!IsValidId(attribute.Id, where) || parameter == null)
            {
                return null;
            }

            Action<object?>? setterOrNull = null;
            if (!field.IsInitOnly && !field.IsLiteral)
            {
                setterOrNull = value => field.SetValue(targetOrNull, value);
            }

            return CheatDefinition.CreateValue(attribute, parameter, targetOrNull, () => field.GetValue(targetOrNull), setterOrNull);
        }

        // 값 치트의 "value" 인자. 생략하면 읽기라서 필수가 아니고 기본값도 없다.
        private CheatParameter? CreateValueParameterOrNull(Type type, CheatPropertyAttribute attribute, MemberInfo member, string where)
        {
            IValueType? valueType = _valueTypes.FindOrNull(type);
            if (valueType == null)
            {
                _reportProblem($"{where}: 값 타입 {type.Name}을 지원하지 않는다. ValueTypeRegistry.Register로 더할 수 있다.");
                return null;
            }

            if (!TryGetProvider(member.GetCustomAttribute<SuggestAttribute>(), where, out ISuggestionProvider? provider))
            {
                return null;
            }

            return new CheatParameter("value", "값", "", type, valueType, false, null, false, attribute.Min, attribute.Max, attribute.Step, provider);
        }

        // 공급자는 타입마다 하나만 만들어 같이 쓴다. 상태가 필요하면 SuggestionContext.TargetOrNull에서 찾는다.
        private bool TryGetProvider(SuggestAttribute? attributeOrNull, string where, out ISuggestionProvider? providerOrNull)
        {
            providerOrNull = null;
            if (attributeOrNull == null)
            {
                return true;
            }

            Type providerType = attributeOrNull.ProviderType;
            if (_providers.TryGetValue(providerType, out ISuggestionProvider cached))
            {
                providerOrNull = cached;
                return true;
            }

            if (providerType == null || !typeof(ISuggestionProvider).IsAssignableFrom(providerType) || providerType.GetConstructor(Type.EmptyTypes) == null)
            {
                _reportProblem($"{where}: [Suggest] 타입은 ISuggestionProvider를 구현하고 매개변수 없는 생성자가 있어야 한다.");
                return false;
            }

            ISuggestionProvider provider = (ISuggestionProvider)Activator.CreateInstance(providerType);
            _providers.Add(providerType, provider);
            providerOrNull = provider;
            return true;
        }

        private bool IsValidId(string id, string where)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                _reportProblem($"{where}: 치트 id가 비어 있다.");
                return false;
            }

            for (int index = 0; index < id.Length; index++)
            {
                if (char.IsWhiteSpace(id[index]) || id[index] == '"')
                {
                    _reportProblem($"{where}: 치트 id '{id}'에 공백이나 따옴표를 쓸 수 없다.");
                    return false;
                }
            }

            return true;
        }

        // 열거형 매개변수의 기본값은 리플렉션에서 정수로 나온다. 열거형 값으로 되돌린다.
        private static object? NormalizeDefault(Type type, object? value)
        {
            if (value != null && type.IsEnum && value.GetType() != type)
            {
                return Enum.ToObject(type, value);
            }

            return value;
        }

        private static CheatResult ToResult(object? returned)
        {
            if (returned is CheatResult result)
            {
                return result;
            }

            return CheatResult.Ok(returned as string ?? "");
        }

        private static string Describe(Type? type, string memberName)
        {
            return $"{type?.FullName}.{memberName}";
        }
    }
}
