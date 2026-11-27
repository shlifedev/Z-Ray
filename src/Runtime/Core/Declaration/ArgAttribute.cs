using System;

namespace Lumera.ZRay
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class ArgAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; set; } = "";
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public double Step { get; set; } = double.NaN;

        public ArgAttribute(string name)
        {
            Name = name;
        }
    }
}
