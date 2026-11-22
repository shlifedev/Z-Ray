namespace Lumera.ZRay
{
    // 검색어 하나로 치트 정의의 순위를 매긴다. 콘솔의 명령 자동완성과 메뉴 검색이 같은 순위를 쓴다.
    // 어느 칸에 어떻게 일치했는지로 점수를 정한다. id가 가장 앞서고, 이름, 분류, 설명 순이다.
    public static class CheatSearch
    {
        public const int NO_MATCH = int.MaxValue;

        // 칸마다 [처음부터, 조각 처음부터, 중간, 머리글자] 순서의 점수. 작을수록 위다. -1은 그 방식으로는 받지 않는다는 뜻이다.
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

        // 인자 후보 값의 순위. 값이 id 자리, 설명이 이름 자리다.
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
