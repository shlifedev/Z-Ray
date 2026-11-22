using System;

namespace Lumera.ZRay
{
    // 치트 매개변수의 표시 이름, 설명, 숫자 범위를 적는다. 붙이지 않으면 매개변수 이름을 그대로 쓴다.
    // 기본값은 여기 적지 않고 C# 기본 매개변수 값으로 쓴다. 안내와 실제 실행이 어긋나지 않게 하려는 것이다.
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
