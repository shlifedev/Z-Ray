namespace Lumera.ZRay
{
    public static class CheatSearch
    {
        public const int NO_MATCH = int.MaxValue;

        private static readonly int[] ID_RANKS = { 0, 20, 300, 400 };
        private static readonly int[] NAME_RANKS = { 80, 100, 380, 450 };
        private static readonly int[] CATEGORY_RANKS = { 180, 200, 480, -1 };
        private static readonly int[] DESCRIPTION_RANKS = { 700, 720, 760, -1 };

        public static int Rank(CheatDefinition definition, string query)
        {
            int rank = RankField(definition.Id, query, ID_RANKS);
            rank = Min(rank, RankField(definition.Name, query, NAME_RANKS));
            rank = Min(rank, RankField(definition.Category, query, CATEGORY_RANKS));
            return Min(rank, RankField(definition.Description, query, DESCRIPTION_RANKS));
        }

        internal static int RankValue(string value, string description, string query)
        {
            return Min(RankField(value, query, ID_RANKS), RankField(description, query, NAME_RANKS));
        }

        private static int RankField(string text, string query, int[] ranks)
        {
            EMatchKind kind = TextMatcher.Match(text, query);
            if (kind == EMatchKind.None)
            {
                return NO_MATCH;
            }

            int rank = ranks[(int)kind - 1];
            return rank < 0 ? NO_MATCH : rank;
        }

        private static int Min(int left, int right)
        {
            return left < right ? left : right;
        }
    }
}
