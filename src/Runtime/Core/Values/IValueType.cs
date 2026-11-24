using System.Collections.Generic;

namespace Lumera.ZRay
{
    public interface IValueType
    {
        string Label { get; }

        bool TryParse(string text, out object? value, out string? errorOrNull);

        string Format(object? value);

        void AddCandidates(List<string> candidates);
    }
}
