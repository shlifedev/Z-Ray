using System;

namespace Lumera.ZRay
{
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
