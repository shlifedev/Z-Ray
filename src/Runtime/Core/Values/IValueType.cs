using System.Collections.Generic;

namespace Lumera.ZRay
{
    // 치트 인자 타입 하나를 글자와 값 사이에서 바꾸는 방법. 콘솔 입력, 메뉴 위젯, 힌트, 자동완성이 모두 이것을 쓴다.
    // 기본 타입(string, bool, int, long, float, double, enum) 밖의 타입은 ValueTypeRegistry.Register로 더한다.
    public interface IValueType
    {
        // 힌트에 보이는 타입 이름(예: "int", "bool", 열거형 이름).
        string Label { get; }

        // 실패하면 errorOrNull에 사람이 읽을 이유를 담는다.
        bool TryParse(string text, out object? value, out string? errorOrNull);

        // TryParse가 다시 읽을 수 있는 글자로 쓴다.
        string Format(object? value);

        // 고를 수 있는 값이 정해진 타입(enum, bool)이면 그 값을 모두 더한다. 아니면 아무것도 하지 않는다.
        void AddCandidates(List<string> candidates);
    }
}
