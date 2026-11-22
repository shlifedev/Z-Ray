using System;

namespace Lumera.ZRay
{
    // 프로퍼티나 필드를 값 치트로 선언한다. 콘솔에서 `id`는 값을 읽고 `id 값`은 쓴다.
    // 메뉴에서는 행 오른쪽에 타입에 맞는 위젯(토글, 숫자, 선택지, 글자)이 붙는다.
    // setter가 없거나 readonly 필드면 읽기 전용이다.
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class CheatPropertyAttribute : Attribute
    {
        public string Id { get; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public int Order { get; set; }
        public bool IsShownInMenu { get; set; } = true;
        // 숫자 범위와 메뉴 스테퍼의 한 칸. 지정하지 않으면 NaN이다.
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public double Step { get; set; } = double.NaN;

        public CheatPropertyAttribute(string id)
        {
            Id = id;
        }
    }
}
