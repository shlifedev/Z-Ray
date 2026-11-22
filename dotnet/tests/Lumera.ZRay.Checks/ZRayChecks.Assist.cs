using System.Collections.Generic;
using static Lumera.ZRay.Checks.Checker;

namespace Lumera.ZRay.Checks;

// 자동완성 후보, 후보 적용, 힌트, 인자 자리 안내.
internal static partial class ZRayChecks
{
    private static void CheckCommandSuggestions()
    {
        Fixture fixture = new Fixture();
        Check(fixture.Analyze("").Suggestions.Count == 0 && fixture.Analyze("   ").Suggestions.Count == 0, "빈 줄에는 후보가 없다(↑/↓는 기록 탐색).");

        IReadOnlyList<Suggestion> player = fixture.Analyze("pla").Suggestions;
        Check(player.Count == 6 && player[0].Insert == "player.god" && player[1].Insert == "player.gold", "처음부터 일치한 후보는 글자 순으로 나온다.");
        Check(player[0].Kind == ESuggestionKind.Value && player[0].Detail == "무적" && player[0].Note == "플레이어", "후보에는 종류, 이름, 분류가 담긴다.");
        Check(player[0].InsertSpans.Length == 1 && player[0].InsertSpans[0].Length == 3, "일치한 구간을 함께 돌려준다.");

        // 점수가 종류보다 먼저다. id가 처음부터 일치한 것, 조각이 일치한 것, 이름만 일치한 것 순이다.
        IReadOnlyList<Suggestion> gold = fixture.Analyze("gold").Suggestions;
        Check(gold[0].Insert == "gold.reset" && gold[1].Insert == "player.gold" && gold[2].Insert == "player.gold.add", "일치 점수 순으로 정렬한다.");

        IReadOnlyList<Suggestion> initials = fixture.Analyze("pga").Suggestions;
        Check(initials.Count == 1 && initials[0].Insert == "player.gold.add" && initials[0].InsertSpans.Length == 3, "머리글자로 명령을 찾는다.");

        IReadOnlyList<Suggestion> byName = fixture.Analyze("무적").Suggestions;
        Check(byName.Count == 1 && byName[0].Insert == "player.god" && byName[0].DetailSpans.Length == 1 && byName[0].InsertSpans.Length == 0, "한글 이름으로 찾으면 이름 쪽 구간을 돌려준다.");
        Check(IndexOf(fixture.Analyze("he").Suggestions, "help") == 0, "내장 명령도 후보에 나온다.");

        // 최근에 쓴 치트는 같은 일치 방식 안에서 위로 온다. 더 나은 일치를 넘지는 않는다.
        fixture.History.RecordCheatUse("player.teleport");
        IReadOnlyList<Suggestion> recent = fixture.Analyze("pla").Suggestions;
        Check(recent[0].Insert == "player.teleport" && recent[1].Insert == "player.god", "최근에 쓴 치트가 먼저 나온다.");
        fixture.History.RecordCheatUse("player.gold.add");
        Check(fixture.Analyze("gold").Suggestions[0].Insert == "gold.reset", "최근 사용이 더 나은 일치를 넘지 않는다.");

        // 커서가 줄 중간이면 커서 앞까지가 검색어다.
        AssistResult middle = fixture.Assist.Analyze("playerXX 5", 3);
        Check(middle.TokenIndex == 0 && middle.Prefix == "pla" && middle.Suggestions.Count == 6 && middle.ReplaceEnd == 8, "줄 중간에서도 명령 후보를 낸다.");
    }

    private static void CheckArgumentSuggestions()
    {
        Fixture fixture = new Fixture();

        // 열거형: 기본값이 먼저, 나머지는 선언 순서.
        IReadOnlyList<Suggestion> element = fixture.Analyze("world.element ").Suggestions;
        Check(element.Count == 3 && element[0].Insert == "Ice" && element[0].Note == "기본값" && element[1].Insert == "Fire" && element[2].Insert == "Lightning", "열거형 후보는 기본값, 선언 순서로 나온다.");
        Check(element[0].Kind == ESuggestionKind.Argument, "인자 후보의 종류는 Argument다.");
        IReadOnlyList<Suggestion> filtered = fixture.Analyze("world.element li").Suggestions;
        Check(filtered.Count == 1 && filtered[0].Insert == "Lightning", "입력한 글자로 후보를 거른다.");

        // bool 값 치트: 지금 값이 먼저.
        IReadOnlyList<Suggestion> god = fixture.Analyze("player.god ").Suggestions;
        Check(god.Count == 2 && god[0].Insert == "false" && god[0].Note == "현재 값" && god[1].Insert == "true", "값 치트는 지금 값을 먼저 보인다.");
        fixture.Cheats.IsGodMode = true;
        Check(fixture.Analyze("player.god ").Suggestions[0].Insert == "true", "지금 값은 분석할 때마다 다시 읽는다.");
        IReadOnlyList<Suggestion> goldValue = fixture.Analyze("player.gold ").Suggestions;
        Check(goldValue.Count == 1 && goldValue[0].Insert == "100", "숫자 값 치트도 지금 값을 후보로 낸다.");

        // 공급자: 대상 객체에서 후보를 읽고, 준 순서를 지킨다.
        IReadOnlyList<Suggestion> items = fixture.Analyze("item.give ").Suggestions;
        Check(items.Count == 3 && items[0].Insert == "iron_sword" && items[0].Detail == "철검" && items[1].Insert == "long bow" && items[2].Insert == "potion", "공급자의 후보가 준 순서대로 나온다.");
        IReadOnlyList<Suggestion> byDescription = fixture.Analyze("item.give 물").Suggestions;
        Check(byDescription.Count == 1 && byDescription[0].Insert == "potion" && byDescription[0].DetailSpans.Length == 1, "후보의 설명으로도 찾는다.");
        fixture.Cheats.Items.Add(("pearl", "진주"));
        Check(fixture.Analyze("item.give p").Suggestions.Count == 2, "공급자는 분석할 때마다 다시 묻는다.");

        // 둘째 인자: 기본값만 후보다.
        IReadOnlyList<Suggestion> count = fixture.Analyze("item.give potion ").Suggestions;
        Check(count.Count == 1 && count[0].Insert == "1" && count[0].Note == "기본값", "후보가 없는 타입은 기본값만 낸다.");
        Check(fixture.Analyze("item.give potion 1 ").Suggestions.Count == 0, "인자 수를 넘은 자리에는 후보가 없다.");
        Check(fixture.Analyze("player.gold.add ").Suggestions.Count == 0, "기본값도 후보도 없는 인자에는 후보가 없다.");

        // 공급자가 예외를 던져도 분석은 끝나고, 그 인자의 후보만 빈다.
        AssistResult broken = fixture.Analyze("world.broken ");
        Check(broken.Suggestions.Count == 0 && broken.ProviderErrorOrNull != null && broken.ProviderErrorOrNull.Contains("공급자 고장") && broken.HintOrNull != null, "공급자의 예외는 안내로 바뀐다.");

        // help의 인자는 등록된 명령이다.
        IReadOnlyList<Suggestion> help = fixture.Analyze("help pga").Suggestions;
        Check(help.Count == 1 && help[0].Insert == "player.gold.add" && help[0].Detail == "골드 추가", "help는 명령 이름을 후보로 낸다.");
        Check(fixture.Analyze("nope ").Suggestions.Count == 0 && fixture.Analyze("nope ").UnknownCommandOrNull != null, "모르는 명령의 인자에는 후보 대신 안내가 있다.");
    }

    private static void CheckApplySuggestion()
    {
        Fixture fixture = new Fixture();

        AssistResult command = fixture.Analyze("pga");
        string applied = fixture.Assist.ApplySuggestion(command, command.Suggestions[0], out int caret);
        Check(applied == "player.gold.add " && caret == 16, "인자가 있는 명령은 공백을 붙이고 그 뒤로 커서를 옮긴다.");

        AssistResult readOnly = fixture.Analyze("player.h");
        applied = fixture.Assist.ApplySuggestion(readOnly, readOnly.Suggestions[0], out caret);
        Check(applied == "player.hp" && caret == 9, "읽기 전용 값은 공백을 붙이지 않는다.");
        AssistResult noArgs = fixture.Analyze("gold.r");
        Check(fixture.Assist.ApplySuggestion(noArgs, noArgs.Suggestions[0], out caret) == "gold.reset" && caret == 10, "인자가 없는 명령도 공백을 붙이지 않는다.");

        // 공백이 든 값은 따옴표로 감싼다. 다음 인자가 있으므로 공백을 붙인다.
        AssistResult spaced = fixture.Analyze("item.give lo");
        applied = fixture.Assist.ApplySuggestion(spaced, spaced.Suggestions[0], out caret);
        Check(applied == "item.give \"long bow\" " && caret == applied.Length, "공백이 든 후보는 따옴표로 감싼다.");
        Check(CommandLine.Parse(applied).Tokens[1].Text == "long bow", "감싼 값은 다시 읽으면 원래 값이다.");

        // 이미 따옴표를 연 채로 입력하던 토큰도 통째로 바꾼다.
        AssistResult open = fixture.Analyze("item.give \"long b");
        Check(open.Suggestions.Count == 1 && fixture.Assist.ApplySuggestion(open, open.Suggestions[0], out caret) == "item.give \"long bow\" ", "따옴표를 연 토큰을 통째로 바꾼다.");

        // 줄 중간: 커서가 있는 토큰만 바꾸고, 뒤에 이미 공백이 있으면 새로 넣지 않고 그 뒤로 커서를 옮긴다.
        AssistResult middle = fixture.Assist.Analyze("item.give po 3", 12);
        applied = fixture.Assist.ApplySuggestion(middle, middle.Suggestions[0], out caret);
        Check(applied == "item.give potion 3" && caret == 17, "줄 중간의 토큰만 바꾼다.");

        // 마지막 인자는 공백을 붙이지 않는다.
        AssistResult last = fixture.Analyze("world.element li");
        Check(fixture.Assist.ApplySuggestion(last, last.Suggestions[0], out caret) == "world.element Lightning" && caret == 23, "마지막 인자 뒤에는 공백을 붙이지 않는다.");

        // 새 토큰 자리(공백 뒤)에 넣는다.
        AssistResult fresh = fixture.Analyze("world.element ");
        Check(fixture.Assist.ApplySuggestion(fresh, fresh.Suggestions[1], out caret) == "world.element Fire", "빈 인자 자리에 후보를 넣는다.");
    }

    private static void CheckHint()
    {
        Fixture fixture = new Fixture();

        CommandHint name = fixture.Analyze("player.gold.add").HintOrNull!;
        Check(name.Definition.Id == "player.gold.add" && name.ActiveIndex == -1 && name.Parameters.Count == 2 && !name.HasError, "명령 이름만 쳤을 때는 활성 인자가 없다.");
        Check(fixture.Analyze("player.gold.ad").HintOrNull == null, "등록된 이름과 정확히 같아야 힌트가 있다.");

        CommandHint first = fixture.Analyze("player.gold.add ").HintOrNull!;
        Check(first.ActiveIndex == 0 && first.Parameters[0].IsActive && !first.Parameters[1].IsActive && !first.HasError, "공백 뒤에서는 다음 인자가 활성이다.");
        CommandHint second = fixture.Analyze("player.gold.add 5 wh").HintOrNull!;
        Check(second.ActiveIndex == 1 && second.Parameters[1].IsActive && !second.HasError, "입력 중인 인자가 활성이다.");

        // 입력하는 동안 타입과 범위를 확인한다.
        CommandHint wrongType = fixture.Analyze("player.gold.add abc").HintOrNull!;
        Check(wrongType.HasError && wrongType.Parameters[0].ErrorOrNull != null && wrongType.Parameters[0].ErrorOrNull!.Contains("정수"), "타입이 틀리면 그 인자에 오류가 붙는다.");
        CommandHint outOfRange = fixture.Analyze("player.gold.add 0 why").HintOrNull!;
        Check(outOfRange.Parameters[0].ErrorOrNull != null && outOfRange.Parameters[0].ErrorOrNull!.Contains("1~99999") && outOfRange.Parameters[1].ErrorOrNull == null, "범위를 벗어나면 범위를 알려 준다.");
        Check(fixture.Analyze("world.element 2").HintOrNull!.Parameters[0].ErrorOrNull!.Contains("Fire"), "열거형은 숫자를 받지 않고 고를 수 있는 이름을 알려 준다.");

        CommandHint tooMany = fixture.Analyze("player.gold.add 1 a b").HintOrNull!;
        Check(tooMany.ActiveIndex == -1 && tooMany.ErrorOrNull != null && tooMany.HasError, "인자가 넘치면 줄 전체의 오류다.");
        Check(fixture.Analyze("gold.reset now").HintOrNull!.ErrorOrNull!.Contains("받지 않습니다"), "인자가 없는 명령에 인자를 주면 오류다.");
        Check(fixture.Analyze("player.hp 3").HintOrNull!.ErrorOrNull!.Contains("읽기 전용") && fixture.Analyze("player.hp").HintOrNull!.ErrorOrNull == null, "읽기 전용 값에 값을 주면 오류다.");

        AssistResult unknown = fixture.Analyze("nope 1");
        Check(unknown.HintOrNull == null && unknown.UnknownCommandOrNull!.Contains("nope"), "모르는 명령은 힌트 대신 안내를 낸다.");
        Check(fixture.Analyze("nop").UnknownCommandOrNull == null, "명령 이름을 치는 중에는 모른다고 하지 않는다.");
    }

    private static void CheckPlaceholderAndGhost()
    {
        Fixture fixture = new Fixture();
        CheckEqual(" <amount:int> [reason:string=cheat]", fixture.Analyze("player.gold.add").Placeholder, "명령 이름 뒤에는 한 칸 띄우고 모든 인자 자리를 보인다.");
        CheckEqual("<amount:int> [reason:string=cheat]", fixture.Analyze("player.gold.add ").Placeholder, "공백 뒤에는 바로 이어서 보인다.");
        CheckEqual(" [reason:string=cheat]", fixture.Analyze("player.gold.add 5").Placeholder, "입력한 인자는 빼고 보인다.");
        CheckEqual("", fixture.Analyze("player.gold.add 5 why").Placeholder, "남은 인자가 없으면 비어 있다.");
        CheckEqual("", fixture.Assist.Analyze("player.gold.add 5", 3).Placeholder, "커서가 줄 끝이 아니면 보이지 않는다.");
        CheckEqual(" [value:int]", fixture.Analyze("player.gold").Placeholder, "값 치트의 value는 생략할 수 있는 자리다.");
        CheckEqual(" [id:string]", fixture.Analyze("help").Placeholder, "빈 글자 기본값은 = 없이 보인다.");
        CheckEqual("player.gold.add <amount:int 1~99999> [reason:string=cheat]", CheatFormatter.FormatSignature(fixture.Registry.FindOrNull("player.gold.add")!), "시그니처에는 범위가 붙는다.");

        // 회색 완성: 고른 후보에서 지금 입력 뒤에 이어질 글자.
        AssistResult typing = fixture.Analyze("player.go");
        CheckEqual("d", typing.GetGhostText(0), "첫 후보의 나머지를 보인다.");
        CheckEqual("ld", typing.GetGhostText(1), "고른 후보에 따라 달라진다.");
        CheckEqual("", typing.GetGhostText(-1), "고른 후보가 없으면 비어 있다.");
        CheckEqual("", fixture.Analyze("pga").GetGhostText(0), "지금 입력으로 시작하지 않는 후보는 이어 쓸 수 없다.");
        CheckEqual("", fixture.Analyze("player.god").GetGhostText(0), "이미 다 친 후보에는 이어질 글자가 없다.");
        CheckEqual("", fixture.Assist.Analyze("player.go 1", 9).GetGhostText(0), "커서가 줄 끝이 아니면 보이지 않는다.");
        CheckEqual("", fixture.Analyze("item.give lo").GetGhostText(0), "따옴표가 필요한 후보는 이어 쓰는 것으로 완성되지 않는다.");
        CheckEqual("ghtning", fixture.Analyze("world.element Li").GetGhostText(0), "인자 후보도 이어 보인다.");
        CheckEqual("", fixture.Analyze("world.element ").GetGhostText(0), "아무것도 치지 않은 자리에는 보이지 않는다.");
    }

    private static void CheckMissingRequired()
    {
        Fixture fixture = new Fixture();
        Check(fixture.Assist.FindMissingRequiredOrNull("player.gold.add")?.Name == "amount", "필수 인자가 빠졌으면 그 인자를 돌려준다.");
        Check(fixture.Assist.FindMissingRequiredOrNull("player.gold.add ")?.Name == "amount", "공백만 더 쳐도 여전히 빠진 것이다.");
        Check(fixture.Assist.FindMissingRequiredOrNull("player.gold.add 5") == null, "기본값이 있는 인자는 빠져도 된다.");
        Check(fixture.Assist.FindMissingRequiredOrNull("player.god") == null && fixture.Assist.FindMissingRequiredOrNull("gold.reset") == null, "값 치트와 인자 없는 명령은 바로 실행한다.");
        Check(fixture.Assist.FindMissingRequiredOrNull("nope") == null && fixture.Assist.FindMissingRequiredOrNull("") == null, "모르는 명령과 빈 줄은 빠진 인자가 없다.");
    }
}
