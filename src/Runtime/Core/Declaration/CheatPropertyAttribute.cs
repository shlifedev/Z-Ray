using System;

namespace Lumera.ZRay
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class CheatPropertyAttribute : Attribute
    {
        public string Id { get; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public int Order { get; set; }
        public bool IsShownInMenu { get; set; } = true;
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public double Step { get; set; } = double.NaN;

        public CheatPropertyAttribute(string id)
        {
            Id = id;
        }
    }
}
