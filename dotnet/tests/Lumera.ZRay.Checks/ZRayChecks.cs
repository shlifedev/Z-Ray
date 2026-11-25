using System;
using System.Collections.Generic;
using static Lumera.ZRay.Checks.Checker;

namespace Lumera.ZRay.Checks;

// ZRay의 Core(화면을 모르는 부분)를 게임 없이 검사한다. 대상은 TestCheats다.
// 화면(Unity 쪽)은 여기서 검사한 계산 결과를 그리기만 한다.
internal static partial class ZRayChecks
{
    public static void RunAll()
    {
        Run(CheckTokens);
        Run(CheckTokenAtCaret);
        Run(CheckQuoteRoundTrip);
        Run(CheckMatchKinds);
        Run(CheckMatchSpans);
        Run(CheckSearchRanks);
        Run(CheckRegistration);
        Run(CheckBadDeclarations);
        Run(CheckStaticRegistration);
        Run(CheckCustomValueType);

        Run(CheckCommandSuggestions);
        Run(CheckArgumentSuggestions);
        Run(CheckApplySuggestion);
        Run(CheckHint);
        Run(CheckPlaceholderAndGhost);
        Run(CheckMissingRequired);

        Run(CheckExecuteAction);
        Run(CheckExecuteErrors);
        Run(CheckExecuteValue);
        Run(CheckBuiltIns);
        Run(CheckMenuPath);

        Run(CheckHistory);
        Run(CheckStorage);

        Run(CheckSessionCompletion);
        Run(CheckSessionSelection);
        Run(CheckSessionSubmit);
        Run(CheckSessionHistory);
        Run(CheckIndependentHistoryNavigation);
        Run(CheckHintFormatter);
        Run(CheckTranscript);
        Run(CheckMenuModel);
        Run(CheckMenuDrafts);
        Run(CheckNumericLimits);
        Run(CheckDraftStore);
    }

    private sealed class Fixture
    {
        public readonly List<string> Problems = new List<string>();
        public readonly TestCheats Cheats = new TestCheats();
        public readonly CommandHistory History = new CommandHistory();
        public readonly CheatRegistry Registry;
        public readonly ConsoleAssist Assist;
        public readonly CheatExecutor Executor;
        public readonly IDisposable Registration;

        public Fixture()
        {
            ValueTypeRegistry valueTypes = new ValueTypeRegistry();
            valueTypes.Register(typeof(TestPoint), new TestPointValueType());
            Registry = new CheatRegistry(valueTypes, Problems.Add);
            BuiltInCheats.Register(Registry, History);
            Registration = Registry.Register(Cheats);
            Assist = new ConsoleAssist(Registry, History);
            Executor = new CheatExecutor(Registry, History);
        }

        // 커서를 줄 끝에 두고 분석한다.
        public AssistResult Analyze(string line)
        {
            return Assist.Analyze(line, line.Length);
        }
    }

    private static void CheckTokens()
    {
        IReadOnlyList<CommandToken> plain = CommandLine.Parse("  give  potion 2 ").Tokens;
        Check(plain.Count == 3 && plain[0].Text == "give" && plain[1].Text == "potion" && plain[2].Text == "2", "공백으로 나누고 앞뒤·연속 공백은 버린다.");
        Check(plain[1].Start == 8 && plain[1].End == 14 && !plain[1].IsQuoted, "토큰은 원문에서의 범위를 갖는다.");

        IReadOnlyList<CommandToken> quoted = CommandLine.Parse("give \"long bow\" 2").Tokens;
        Check(quoted.Count == 3 && quoted[1].Text == "long bow" && quoted[1].IsQuoted, "따옴표 안의 공백은 나누지 않는다.");
        Check(quoted[1].Start == 5 && quoted[1].End == 15, "따옴표 토큰의 범위는 따옴표까지 포함한다.");

        IReadOnlyList<CommandToken> escaped = CommandLine.Parse("say \"he said \\\"hi\\\"\" c:\\temp").Tokens;
        Check(escaped.Count == 3 && escaped[1].Text == "he said \"hi\"", "\\\"는 따옴표 글자다.");
        Check(escaped[2].Text == "c:\\temp", "그 밖의 역슬래시는 글자 그대로 둔다.");

        IReadOnlyList<CommandToken> open = CommandLine.Parse("give \"long b").Tokens;
        Check(open.Count == 2 && open[1].Text == "long b" && open[1].End == 12, "닫지 않은 따옴표는 줄 끝까지 한 토큰이다.");

        IReadOnlyList<CommandToken> middle = CommandLine.Parse("a\"b c\"d").Tokens;
        Check(middle.Count == 1 && middle[0].Text == "ab cd", "따옴표는 토큰 중간에 와도 된다.");
        Check(CommandLine.Parse(null).Tokens.Count == 0 && CommandLine.Parse("   ").Tokens.Count == 0, "빈 줄에는 토큰이 없다.");
    }

    private static void CheckTokenAtCaret()
    {
        CommandLine line = CommandLine.Parse("give po 2");
        TokenPosition end = line.FindTokenAt(7);
        Check(end.Index == 1 && end.Prefix == "po" && end.ReplaceStart == 5 && end.ReplaceEnd == 7, "커서가 토큰 끝이면 그 토큰 전체가 접두어다.");
        TokenPosition inside = line.FindTokenAt(6);
        Check(inside.Index == 1 && inside.Prefix == "p" && inside.ReplaceEnd == 7, "커서가 토큰 중간이면 커서 앞까지만 접두어고, 바꾸는 범위는 토큰 전체다.");
        TokenPosition start = line.FindTokenAt(5);
        Check(start.Index == 1 && start.Prefix == "", "커서가 토큰 맨 앞이면 접두어가 비어 있다.");

        TokenPosition trailing = CommandLine.Parse("give ").FindTokenAt(5);
        Check(trailing.Index == 1 && trailing.Prefix == "" && trailing.ReplaceStart == 5 && trailing.ReplaceEnd == 5, "줄 끝 공백 뒤는 아직 없는 새 토큰이다.");
        TokenPosition gap = CommandLine.Parse("a   b").FindTokenAt(2);
        Check(gap.Index == 1 && gap.ReplaceStart == 2 && gap.ReplaceEnd == 2, "토큰 사이 공백 위는 그 자리에 들어갈 새 토큰이다.");

        TokenPosition quoted = CommandLine.Parse("give \"long b").FindTokenAt(12);
        Check(quoted.Index == 1 && quoted.Prefix == "long b" && quoted.ReplaceStart == 5, "따옴표 안에서도 접두어는 푼 값이다.");
        TokenPosition quotedInside = CommandLine.Parse("give \"long bow\"").FindTokenAt(9);
        Check(quotedInside.Prefix == "lon" && quotedInside.ReplaceEnd == 15, "따옴표 토큰 중간의 접두어도 커서 앞까지만이다.");
    }

    private static void CheckQuoteRoundTrip()
    {
        string[] values = { "plain", "long bow", "", "he\"y", "c:\\temp", "tab\there", "끝 \\" };
        for (int index = 0; index < values.Length; index++)
        {
            string quoted = CommandLine.Quote(values[index]);
            IReadOnlyList<CommandToken> tokens = CommandLine.Parse($"cmd {quoted} tail").Tokens;
            Check(tokens.Count == 3 && tokens[1].Text == values[index], $"Quote한 값은 다시 읽어도 같다: [{values[index]}] → {quoted}");
        }

        Check(CommandLine.Quote("plain") == "plain", "따옴표가 필요 없는 값은 그대로 둔다.");
    }

    private static void CheckMatchKinds()
    {
        const string ID = "player.gold.add";
        CheckEqual(EMatchKind.Prefix, TextMatcher.Match(ID, "pla"), "처음부터 일치.");
        CheckEqual(EMatchKind.Prefix, TextMatcher.Match(ID, "PLAYER.G"), "대소문자를 무시한다.");
        CheckEqual(EMatchKind.SegmentPrefix, TextMatcher.Match(ID, "gold"), "점 뒤 조각의 처음부터 일치.");
        CheckEqual(EMatchKind.SegmentPrefix, TextMatcher.Match(ID, "add"), "마지막 조각도 조각 일치다.");
        CheckEqual(EMatchKind.Substring, TextMatcher.Match(ID, "old"), "조각 중간의 일치.");
        CheckEqual(EMatchKind.Initials, TextMatcher.Match(ID, "pga"), "조각 머리글자.");
        CheckEqual(EMatchKind.Initials, TextMatcher.Match(ID, "plgo"), "머리글자는 한 조각에서 여러 글자를 이어 써도 된다.");
        CheckEqual(EMatchKind.None, TextMatcher.Match(ID, "pag"), "머리글자의 순서가 다르면 일치하지 않는다.");
        CheckEqual(EMatchKind.None, TextMatcher.Match(ID, "lyr"), "조각 중간에서 띄엄띄엄 고른 글자는 받지 않는다.");
        CheckEqual(EMatchKind.Prefix, TextMatcher.Match(ID, ""), "빈 검색어는 모든 후보와 일치한다.");
        CheckEqual(EMatchKind.None, TextMatcher.Match("", "a"), "빈 후보는 일치하지 않는다.");
        CheckEqual(EMatchKind.SegmentPrefix, TextMatcher.Match("골드 추가", "추가"), "한글 이름도 공백으로 조각을 나눈다.");
        CheckEqual(EMatchKind.SegmentPrefix, TextMatcher.Match("플레이어/재화", "재화"), "분류는 /로 조각을 나눈다.");
    }

    private static void CheckMatchSpans()
    {
        MatchSpan[] segment = TextMatcher.FindSpans("player.gold.add", "gold");
        Check(segment.Length == 1 && segment[0].Start == 7 && segment[0].Length == 4, "조각 일치는 그 조각의 구간을 돌려준다.");

        // 같은 글자가 앞에서 중간 일치로 먼저 나와도, 조각 처음에서 시작하는 일치를 고른다.
        MatchSpan[] later = TextMatcher.FindSpans("bold.old", "old");
        Check(later.Length == 1 && later[0].Start == 5, "중간 일치보다 조각 처음 일치를 먼저 고른다.");

        MatchSpan[] initials = TextMatcher.FindSpans("player.gold.add", "plgo");
        Check(initials.Length == 2 && initials[0].Start == 0 && initials[0].Length == 2 && initials[1].Start == 7 && initials[1].Length == 2, "머리글자 일치는 조각마다 구간을 돌려준다.");
        Check(TextMatcher.FindSpans("player.gold.add", "zzz").Length == 0 && TextMatcher.FindSpans("player", "").Length == 0, "일치하지 않거나 검색어가 비면 구간이 없다.");

        // 일치에 실패하면 받은 목록을 건드리지 않는다.
        List<MatchSpan> spans = new List<MatchSpan> { new MatchSpan(9, 9) };
        TextMatcher.Match("player.gold.add", "pgz", spans);
        Check(spans.Count == 1 && spans[0].Start == 9, "실패한 머리글자 일치는 더하던 구간을 되돌린다.");
    }

    private static void CheckSearchRanks()
    {
        Fixture fixture = new Fixture();
        CheatDefinition addGold = fixture.Registry.FindOrNull("player.gold.add")!;
        int byId = CheatSearch.Rank(addGold, "player");
        int byName = CheatSearch.Rank(addGold, "골드");
        int byCategory = CheatSearch.Rank(addGold, "재화");
        int byDescription = CheatSearch.Rank(addGold, "더한다");
        Check(byId < byName && byName < byCategory && byCategory < byDescription, "id, 이름, 분류, 설명 순으로 앞선다.");
        Check(CheatSearch.Rank(addGold, "pla") < CheatSearch.Rank(addGold, "gold") && CheatSearch.Rank(addGold, "gold") < CheatSearch.Rank(addGold, "old"), "같은 칸에서는 처음부터, 조각 처음부터, 중간 순이다.");
        Check(CheatSearch.Rank(addGold, "old") < CheatSearch.Rank(addGold, "pga"), "머리글자 일치가 가장 뒤다.");
        CheckEqual(CheatSearch.NO_MATCH, CheatSearch.Rank(addGold, "없는말"), "어느 칸에도 없으면 일치하지 않는다.");
    }

    private static void CheckRegistration()
    {
        Fixture fixture = new Fixture();
        Check(fixture.Problems.Count == 0, $"올바른 선언은 문제를 보고하지 않는다: {string.Join(" / ", fixture.Problems)}");
        Check(fixture.Registry.FindOrNull("PLAYER.gold.ADD") != null, "id는 대소문자를 무시하고 찾는다.");
        Check(fixture.Registry.FindOrNull("gold.reset") != null, "private 메서드도 등록한다.");

        CheatDefinition addGold = fixture.Registry.FindOrNull("player.gold.add")!;
        Check(addGold.Kind == ECheatKind.Action && addGold.Name == "골드 추가" && addGold.Category == "플레이어/재화" && addGold.TargetOrNull == fixture.Cheats, "특성의 값과 대상 객체가 정의에 담긴다.");
        CheatParameter amount = addGold.Parameters[0];
        CheatParameter reason = addGold.Parameters[1];
        Check(amount.Name == "amount" && amount.DisplayName == "양" && amount.IsRequired && amount.Min == 1 && amount.Max == 99999 && !amount.HasStep, "[Arg]의 이름과 범위가 인자에 담긴다.");
        Check(reason.DisplayName == "reason" && !reason.IsRequired && reason.HasDefault && (string?)reason.DefaultValue == "cheat", "C# 기본값이 인자의 기본값이 된다.");

        CheatParameter element = fixture.Registry.FindOrNull("world.element")!.Parameters[0];
        Check(element.DefaultValue is ETestElement defaultElement && defaultElement == ETestElement.Ice && element.FormatDefault() == "Ice", "열거형 기본값은 정수가 아니라 열거형 값이다.");

        CheatDefinition hp = fixture.Registry.FindOrNull("player.hp")!;
        CheatDefinition gold = fixture.Registry.FindOrNull("player.gold")!;
        Check(hp.Kind == ECheatKind.Value && !hp.CanWrite && gold.CanWrite, "setter가 없는 값 치트는 읽기 전용이다.");
        Check(gold.Parameters.Count == 1 && !gold.Parameters[0].IsRequired && !gold.Parameters[0].HasDefault && gold.Parameters[0].Max == 1000, "값 치트는 생략할 수 있는 value 인자 하나를 갖는다.");
        Check(fixture.Registry.FindOrNull("world.element")!.Category == "월드" && new CheatAttribute("x").Category == "", "분류를 비우면 정의에서 기본 분류가 된다.");

        // 분류, 순서, id 순으로 정렬한다. gold.reset은 Order가 -1이라 같은 분류의 맨 앞이다.
        IReadOnlyList<CheatDefinition> definitions = fixture.Registry.Definitions;
        int resetIndex = IndexOf(definitions, "gold.reset");
        int addIndex = IndexOf(definitions, "player.gold.add");
        Check(resetIndex >= 0 && resetIndex + 1 == IndexOf(definitions, "player.gold") && addIndex == resetIndex + 2, "같은 분류 안에서는 Order, 그다음 id 순이다.");

        // 같은 id는 먼저 등록한 것만 남는다. 핸들을 Dispose하면 그 핸들이 등록한 것만 빠진다.
        int versionBefore = fixture.Registry.Version;
        IDisposable duplicate = fixture.Registry.Register(new DuplicateCheats());
        Check(fixture.Problems.Count == 1 && fixture.Registry.FindOrNull("player.gold.add") == addGold, "중복 id는 거부하고 이유를 알린다.");
        Check(fixture.Registry.FindOrNull("unique.one") != null && fixture.Registry.Version == versionBefore + 1, "중복이 아닌 것은 등록되고 Version이 오른다.");
        duplicate.Dispose();
        Check(fixture.Registry.FindOrNull("unique.one") == null && fixture.Registry.FindOrNull("player.gold.add") == addGold, "Dispose는 자기가 등록한 것만 뺀다.");
        duplicate.Dispose();
        Check(fixture.Registry.Version == versionBefore + 2, "두 번 Dispose해도 한 번만 뺀다.");

        ISuggestionProvider? originalProvider = fixture.Registry.FindOrNull("item.give")!.Parameters[0].SuggestionProviderOrNull;
        int countBefore = fixture.Registry.Definitions.Count;
        fixture.Registration.Dispose();
        Check(fixture.Registry.FindOrNull("player.gold.add") == null && fixture.Registry.Definitions.Count < countBefore && fixture.Registry.FindOrNull("help") != null, "대상의 등록을 풀어도 내장 명령은 남는다.");
        using IDisposable registration = fixture.Registry.Register(fixture.Cheats);
        Check(originalProvider != null && originalProvider == fixture.Registry.FindOrNull("item.give")!.Parameters[0].SuggestionProviderOrNull, "재등록할 때 같은 타입의 공급자를 재사용한다.");
        CheckThrows<ArgumentNullException>(() => fixture.Registry.Register(null!), "null 대상은 거부한다.");
    }

    private static void CheckBadDeclarations()
    {
        List<string> problems = new List<string>();
        CheatRegistry registry = new CheatRegistry(null, problems.Add);
        registry.Register(new BadCheats());
        Check(registry.Definitions.Count == 0, "잘못된 선언은 하나도 등록하지 않는다.");
        Check(problems.Count == 9, $"잘못된 선언마다 이유를 알린다. 받은 것: {problems.Count}개 / {string.Join(" / ", problems)}");
        Check(problems.TrueForAll(static problem => problem.Contains(nameof(BadCheats))), "문제에는 선언한 타입 이름이 들어간다.");
    }

    private static void CheckStaticRegistration()
    {
        List<string> problems = new List<string>();
        CheatRegistry byType = new CheatRegistry(null, problems.Add);
        byType.RegisterStatic(typeof(StaticTestCheats));
        Check(byType.FindOrNull("static.ping") != null && byType.FindOrNull("static.ping")!.TargetOrNull == null, "정적 치트는 대상 없이 등록된다.");
        byType.RegisterStatic(typeof(TestCheats));
        Check(byType.Definitions.Count == 1, "정적 등록은 인스턴스 멤버를 읽지 않는다.");

        // 이 검사 어셈블리는 ZRay를 참조하므로 훑고, 시스템 어셈블리는 건너뛴다.
        CheatRegistry byAssembly = new CheatRegistry(null, problems.Add);
        byAssembly.RegisterStatic(new[] { typeof(string).Assembly, typeof(ZRayChecks).Assembly });
        CheatExecutor executor = new CheatExecutor(byAssembly);
        Check(executor.Execute("static.ping").Message == "pong" && byAssembly.Definitions.Count == 1, "같은 어셈블리에 잘못된 선언이 있어도 정상적인 정적 치트는 실행된다.");
        Check(problems.Count == 4 && problems.Exists(static problem => problem.Contains(nameof(BadStaticCheats.Broken)) && problem.Contains("공급자 생성 실패")), "실패한 선언마다 이유를 알리고 공급자 생성자의 원래 오류를 남긴다.");

        byAssembly.RegisterStatic(typeof(GenericTestCheats<int>));
        Check(executor.Execute("generic.action").Message == "Int32", "닫힌 제네릭 타입을 명시적으로 등록하면 정적 메서드를 호출할 수 있다.");
        Check(executor.Execute("generic.property").IsSuccess && executor.Execute("generic.field").IsSuccess, "닫힌 제네릭 타입의 정적 프로퍼티와 필드를 읽을 수 있다.");
    }

    private static void CheckCustomValueType()
    {
        Fixture fixture = new Fixture();
        ExecutionResult moved = fixture.Executor.Execute("player.teleport 3,4");
        Check(moved.IsSuccess && fixture.Cheats.Position.X == 3 && fixture.Cheats.Position.Y == 4, "Register로 더한 타입을 인자로 쓸 수 있다.");
        ExecutionResult wrong = fixture.Executor.Execute("player.teleport here");
        Check(!wrong.IsSuccess && wrong.Message.Contains("x,y"), "더한 타입의 오류 메시지가 그대로 보인다.");
        CheckEqual("player.teleport <point:point>", CheatFormatter.FormatSignature(fixture.Registry.FindOrNull("player.teleport")!), "더한 타입의 이름이 시그니처에 보인다.");

        ValueTypeRegistry plain = new ValueTypeRegistry();
        Check(plain.FindOrNull(typeof(TestPoint)) == null && plain.FindOrNull(typeof(ETestFlags)) == null, "등록하지 않은 타입과 [Flags] 열거형은 지원하지 않는다.");
        Check(plain.FindOrNull(typeof(ETestElement)) == plain.FindOrNull(typeof(ETestElement)), "열거형 타입은 한 번 만들어 다시 쓴다.");
    }

    private static int IndexOf(IReadOnlyList<CheatDefinition> definitions, string id)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            if (definitions[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOf(IReadOnlyList<Suggestion> suggestions, string insert)
    {
        for (int index = 0; index < suggestions.Count; index++)
        {
            if (suggestions[index].Insert == insert)
            {
                return index;
            }
        }

        return -1;
    }
}
