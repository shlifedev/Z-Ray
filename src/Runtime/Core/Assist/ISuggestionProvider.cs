using System.Collections.Generic;

namespace Lumera.ZRay
{
    // 공급자가 내놓는 후보 값 하나. Description은 후보 옆에 흐리게 보이고 검색에도 걸린다(예: 아이템 id 옆의 한글 이름).
    public readonly struct CandidateValue
    {
        public string Value { get; }
        public string Description { get; }

        public CandidateValue(string value, string description = "")
        {
            Value = value;
            Description = description ?? "";
        }
    }

    // 공급자가 후보를 만들 때 보는 상황.
    public sealed class SuggestionContext
    {
        public CheatDefinition Definition { get; }
        public CheatParameter Parameter { get; }
        // 지금 입력 중인 글자. 후보를 미리 거르고 싶을 때만 쓴다(거르지 않아도 엔진이 다시 거르고 순위를 매긴다).
        public string Prefix { get; }
        // 이 인자보다 앞에 입력한 인자의 원문. 앞 인자에 따라 후보가 달라질 때 쓴다.
        public IReadOnlyList<string> PreviousArgs { get; }
        // 인스턴스로 등록한 치트의 대상 객체. 정적 상태 없이 지금 세션의 데이터를 찾는 통로다.
        public object? TargetOrNull => Definition.TargetOrNull;

        internal SuggestionContext(CheatDefinition definition, CheatParameter parameter, string prefix, IReadOnlyList<string> previousArgs)
        {
            Definition = definition;
            Parameter = parameter;
            Prefix = prefix;
            PreviousArgs = previousArgs;
        }
    }

    // 인자 자리에 띄울 자동완성 후보를 만든다. [Suggest(typeof(...))]로 붙인다.
    // 입력할 때마다 불리므로 가볍게 만든다. 예외를 던지면 그 인자의 후보만 비고 콘솔은 계속 동작한다.
    public interface ISuggestionProvider
    {
        void AddCandidates(SuggestionContext context, List<CandidateValue> output);
    }
}
