using System;

namespace Lumera.DebuggerKit
{
    // 매개변수나 값 치트에 자동완성 후보 공급자를 붙인다.
    // 공급자 타입은 ISuggestionProvider를 구현하고 매개변수 없는 생성자가 있어야 한다. 등록할 때 한 번 만들어 계속 쓴다.
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class SuggestAttribute : Attribute
    {
        public Type ProviderType { get; }

        public SuggestAttribute(Type providerType)
        {
            ProviderType = providerType;
        }
    }
}
