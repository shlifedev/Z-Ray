using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public sealed class CheatDraftStore
    {
        private readonly Dictionary<string, string[]> _drafts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        public string[] Get(CheatDefinition definition)
        {
            if (_drafts.TryGetValue(definition.Id, out string[] drafts) && drafts.Length == definition.Parameters.Count)
            {
                return drafts;
            }

            drafts = new string[definition.Parameters.Count];
            for (int index = 0; index < drafts.Length; index++)
            {
                drafts[index] = definition.Parameters[index].FormatDefault();
            }

            _drafts[definition.Id] = drafts;
            return drafts;
        }

        public void Remember(CheatDefinition definition, IReadOnlyList<string> argumentTexts)
        {
            string[] drafts = Get(definition);
            for (int index = 0; index < argumentTexts.Count && index < drafts.Length; index++)
            {
                drafts[index] = argumentTexts[index];
            }
        }
    }
}
