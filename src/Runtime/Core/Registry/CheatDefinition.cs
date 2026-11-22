using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public enum ECheatKind
    {
        // 실행하는 치트([Cheat] 메서드)
        Action,
        // 읽고 쓰는 값([CheatProperty] 프로퍼티·필드)
        Value
    }

    // 치트 하나의 정의. 실행, 검증, 도움말, 자동완성, 메뉴 행이 모두 이 정의 하나에서 나온다.
    public sealed class CheatDefinition
    {
        public const string DEFAULT_CATEGORY = "기타";

        private readonly Func<object?[], CheatResult>? _invokerOrNull;
        private readonly Func<object?>? _getterOrNull;
        private readonly Action<object?>? _setterOrNull;

        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }
        public int Order { get; }
        public bool IsShownInMenu { get; }
        public ECheatKind Kind { get; }
        public IReadOnlyList<CheatParameter> Parameters { get; }
        // 인스턴스로 등록한 치트의 대상. 자동완성 공급자가 지금 세션의 데이터를 찾는 데 쓴다.
        public object? TargetOrNull { get; }

        public bool CanWrite => _setterOrNull != null;
        public bool HasParameters => Parameters.Count > 0;

        private CheatDefinition(string id, string name, string category, string description, int order, bool bShownInMenu,
            ECheatKind kind, CheatParameter[] parameters, object? targetOrNull,
            Func<object?[], CheatResult>? invokerOrNull, Func<object?>? getterOrNull, Action<object?>? setterOrNull)
        {
            Id = id;
            Name = string.IsNullOrEmpty(name) ? id : name;
            Category = string.IsNullOrEmpty(category) ? DEFAULT_CATEGORY : category;
            Description = description ?? "";
            Order = order;
            IsShownInMenu = bShownInMenu;
            Kind = kind;
            Parameters = parameters;
            TargetOrNull = targetOrNull;
            _invokerOrNull = invokerOrNull;
            _getterOrNull = getterOrNull;
            _setterOrNull = setterOrNull;
        }

        internal static CheatDefinition CreateAction(CheatAttribute attribute, CheatParameter[] parameters, object? targetOrNull, Func<object?[], CheatResult> invoker)
        {
            return new CheatDefinition(attribute.Id, attribute.Name, attribute.Category, attribute.Description, attribute.Order, attribute.IsShownInMenu,
                ECheatKind.Action, parameters, targetOrNull, invoker, null, null);
        }

        internal static CheatDefinition CreateValue(CheatPropertyAttribute attribute, CheatParameter valueParameter, object? targetOrNull, Func<object?> getter, Action<object?>? setterOrNull)
        {
            return new CheatDefinition(attribute.Id, attribute.Name, attribute.Category, attribute.Description, attribute.Order, attribute.IsShownInMenu,
                ECheatKind.Value, new CheatParameter[] { valueParameter }, targetOrNull, null, getter, setterOrNull);
        }

        // 값 치트의 지금 값. 메뉴가 화면에 보이는 행을 주기적으로 다시 읽을 때 쓴다.
        public object? ReadValue()
        {
            if (_getterOrNull == null)
            {
                throw new InvalidOperationException($"'{Id}'는 값 치트가 아니다.");
            }

            return _getterOrNull();
        }

        public string FormatValue()
        {
            return Parameters[0].ValueType.Format(ReadValue());
        }

        internal CheatResult Invoke(object?[] arguments)
        {
            if (_invokerOrNull == null)
            {
                throw new InvalidOperationException($"'{Id}'는 실행하는 치트가 아니다.");
            }

            return _invokerOrNull(arguments);
        }

        internal void WriteValue(object? value)
        {
            if (_setterOrNull == null)
            {
                throw new InvalidOperationException($"'{Id}'는 읽기 전용이다.");
            }

            _setterOrNull(value);
        }
    }
}
