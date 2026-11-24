using System.Collections.Generic;

namespace Lumera.ZRay
{
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

    public sealed class SuggestionContext
    {
        public CheatDefinition Definition { get; }
        public CheatParameter Parameter { get; }
        public string Prefix { get; }
        public IReadOnlyList<string> PreviousArgs { get; }
        public object? TargetOrNull => Definition.TargetOrNull;

        internal SuggestionContext(CheatDefinition definition, CheatParameter parameter, string prefix, IReadOnlyList<string> previousArgs)
        {
            Definition = definition;
            Parameter = parameter;
            Prefix = prefix;
            PreviousArgs = previousArgs;
        }
    }

    public interface ISuggestionProvider
    {
        void AddCandidates(SuggestionContext context, List<CandidateValue> output);
    }
}
