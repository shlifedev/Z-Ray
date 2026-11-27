using System;

namespace Lumera.ZRay
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CheatAttribute : Attribute
    {
        public string Id { get; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public int Order { get; set; }
        public bool IsShownInMenu { get; set; } = true;

        public CheatAttribute(string id)
        {
            Id = id;
        }
    }
}
