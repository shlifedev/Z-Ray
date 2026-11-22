using System;
using System.Collections.Generic;
using static Lumera.DebuggerKit.Checks.Checker;

namespace Lumera.DebuggerKit.Checks;

// 명령 실행, 내장 명령, 메뉴 경로, 기록과 저장.
internal static partial class DebuggerKitChecks
{
    private static void CheckExecuteAction()
    {
        Fixture fixture = new Fixture();
        int executedCount = 0;
        fixture.Executor.Executed += _ => executedCount++;

        ExecutionResult added = fixture.Executor.Execute("  player.gold.add 50 ");
        Check(added.IsSuccess && fixture.Cheats.Gold == 150 && added.DefinitionOrNull?.Id == "player.gold.add", "인자를 읽어 치트를 실행한다.");
        Check(added.Lines.Count == 2 && added.Lines[0].Kind == EConsoleLineKind.Echo && added.Lines[0].Text == "> player.gold.add 50   (기본값: reason=cheat)", $"실행한 줄에 채운 기본값을 덧붙인다: {added.Lines[0].Text}");
        Check(added.Lines[1].Kind == EConsoleLineKind.Success && added.Lines[1].Text == "골드 +50 (cheat)" && added.Message == "골드 +50 (cheat)", "string 반환값은 성공 메시지다.");

        ExecutionResult quoted = fixture.Executor.Execute("PLAYER.GOLD.ADD 1 \"big win\"");
        Check(quoted.IsSuccess && quoted.Message == "골드 +1 (big win)" && quoted.Lines[0].Text == "> PLAYER.GOLD.ADD 1 \"big win\"", "따옴표 인자를 한 값으로 넘기고, 기본값을 쓰지 않았으면 덧붙이지 않는다.");

        ExecutionResult enumArgument = fixture.Executor.Execute("world.element lightning");
        Check(enumArgument.IsSuccess && fixture.Cheats.Element == ETestElement.Lightning && enumArgument.Message == "속성 완료", "열거형은 이름으로 읽고, 메시지 없는 치트는 완료를 알린다.");
        fixture.Executor.Execute("world.element");
        Check(fixture.Cheats.Element == ETestElement.Ice, "생략한 열거형 인자는 기본값으로 채운다.");

        ExecutionResult given = fixture.Executor.Execute("item.give \"long bow\" 3");
        Check(given.IsSuccess && fixture.Cheats.Given.Count == 1 && fixture.Cheats.Given[0] == "long bowx3", "CheatResult 반환값의 성공을 그대로 쓴다.");
        ExecutionResult refused = fixture.Executor.Execute("item.give nothing");
        Check(!refused.IsSuccess && refused.DefinitionOrNull != null && refused.Lines[1].Kind == EConsoleLineKind.Error && refused.Message.Contains("nothing"), "CheatResult.Fail은 오류 줄이 된다.");

        Check(fixture.Executor.Execute("").Lines.Count == 0 && fixture.Executor.Execute("   ").Lines.Count == 0 && executedCount == 6, "빈 줄은 실행하지 않고 알리지도 않는다.");

        // 메뉴에 보이는 치트만 최근 사용에 남는다. 방금 쓴 것이 앞이다.
        IReadOnlyList<string> recent = fixture.History.RecentCheatIds;
        Check(recent.Count == 3 && recent[0] == "item.give" && recent[1] == "world.element" && recent[2] == "player.gold.add", $"최근에 쓴 치트를 기록한다: {string.Join(",", recent)}");
        fixture.Executor.Execute("help");
        Check(fixture.History.RecentCheatIds.Count == 3, "콘솔 전용 명령은 최근 사용에 넣지 않는다.");
    }

    private static void CheckExecuteErrors()
    {
        Fixture fixture = new Fixture();

        ExecutionResult outOfRange = fixture.Executor.Execute("player.gold.add 0");
        Check(!outOfRange.IsSuccess && outOfRange.DefinitionOrNull == null && fixture.Cheats.Gold == 100 && outOfRange.Message.Contains("1~99999") && outOfRange.Message.Contains("양"), "범위를 벗어나면 실행하지 않는다.");
        ExecutionResult wrongType = fixture.Executor.Execute("player.gold.add many");
        Check(!wrongType.IsSuccess && wrongType.Message.Contains("정수") && fixture.Cheats.Gold == 100, "타입이 틀리면 실행하지 않는다.");
        ExecutionResult missing = fixture.Executor.Execute("player.gold.add");
        Check(!missing.IsSuccess && missing.Message.Contains("양") && missing.Message.Contains("<amount:int 1~99999>"), "필수 인자가 빠지면 사용법을 보인다.");
        ExecutionResult tooMany = fixture.Executor.Execute("player.gold.add 1 a b");
        Check(!tooMany.IsSuccess && tooMany.Message.Contains("너무 많습니다") && fixture.Cheats.Gold == 100, "인자가 넘치면 실행하지 않는다.");
        Check(fixture.Executor.Execute("world.element 2").Message.Contains("Fire, Ice, Lightning"), "열거형에 숫자를 주면 고를 수 있는 이름을 보인다.");
        Check(fixture.History.RecentCheatIds.Count == 0, "실행하지 못한 명령은 최근 사용에 넣지 않는다.");

        ExecutionResult unknown = fixture.Executor.Execute("player.gol 5");
        Check(!unknown.IsSuccess && unknown.Lines.Count == 3 && unknown.Lines[1].Kind == EConsoleLineKind.Error && unknown.Lines[2].Text == "비슷한 명령: player.gold, player.gold.add", $"모르는 명령에는 비슷한 명령을 알려 준다: {unknown.Message}");
        Check(fixture.Executor.Execute("zzzz").Lines.Count == 2, "비슷한 명령이 없으면 그 줄은 없다.");

        // 치트가 던진 예외는 오류 줄이 되고, 원래 예외를 호스트가 로그로 남길 수 있게 돌려준다.
        ExecutionResult thrown = fixture.Executor.Execute("world.explode");
        Check(!thrown.IsSuccess && thrown.ExceptionOrNull is InvalidOperationException && thrown.Message == "InvalidOperationException: 폭발했다" && thrown.DefinitionOrNull != null, "예외를 풀어 원인을 보인다.");
        Check(fixture.Executor.Execute("gold.reset").IsSuccess && fixture.Cheats.Gold == 0, "예외 뒤에도 다음 명령을 실행한다.");

        // CheatException은 치트가 일부러 알린 실패다. 메시지만 보이고 예외로 돌려주지 않는다.
        Check(fixture.Executor.Execute("player.level 3").IsSuccess && fixture.Cheats.Level == 3, "값 치트의 setter가 값을 받는다.");
        ExecutionResult refused = fixture.Executor.Execute("player.level 2");
        Check(!refused.IsSuccess && refused.ExceptionOrNull == null && refused.Message == "레벨은 내릴 수 없습니다." && fixture.Cheats.Level == 3, "CheatException은 메시지만 실패로 보인다.");
    }

    private static void CheckExecuteValue()
    {
        Fixture fixture = new Fixture();

        ExecutionResult read = fixture.Executor.Execute("player.gold");
        Check(read.IsSuccess && read.Lines.Count == 2 && read.Lines[1].Kind == EConsoleLineKind.Info && read.Lines[1].Text == "player.gold = 100", "인자 없이 치면 값을 읽는다.");
        ExecutionResult written = fixture.Executor.Execute("player.gold 300");
        Check(written.IsSuccess && fixture.Cheats.Gold == 300 && written.Lines[1].Kind == EConsoleLineKind.Success && written.Message == "player.gold = 300", "값을 주면 쓰고, 쓴 뒤의 값을 보인다.");
        ExecutionResult outOfRange = fixture.Executor.Execute("player.gold 5000");
        Check(!outOfRange.IsSuccess && fixture.Cheats.Gold == 300 && outOfRange.Message.Contains("0~1000"), "[CheatProperty]의 범위를 확인한다.");

        Check(fixture.Executor.Execute("player.god on").IsSuccess && fixture.Cheats.IsGodMode, "bool은 on/off도 읽는다.");
        Check(fixture.Executor.Execute("player.god false").Message == "player.god = false" && !fixture.Cheats.IsGodMode, "bool 값은 true/false로 보인다.");

        ExecutionResult readOnly = fixture.Executor.Execute("player.hp 3");
        Check(!readOnly.IsSuccess && readOnly.Message.Contains("읽기 전용"), "읽기 전용 값은 쓸 수 없다.");
        Check(fixture.Executor.Execute("player.hp").Message == "player.hp = 7", "읽기 전용 값도 읽을 수 있다.");
        CheckEqual("300", fixture.Registry.FindOrNull("player.gold")!.FormatValue(), "메뉴가 쓰는 FormatValue는 지금 값을 글자로 돌려준다.");
    }

    private static void CheckBuiltIns()
    {
        Fixture fixture = new Fixture();

        ExecutionResult list = fixture.Executor.Execute("help");
        Check(list.IsSuccess && list.Message.Contains("[플레이어/재화]") && list.Message.Contains("player.gold.add <amount:int 1~99999> [reason:string=cheat]  — 골드 추가") && list.Message.Contains("help [id:string]"), "help는 분류별로 모든 명령의 시그니처를 보인다.");
        Check(list.Lines.Count > 10, "여러 줄 메시지는 줄마다 나눈다.");

        ExecutionResult detail = fixture.Executor.Execute("help player.gold.add");
        Check(detail.IsSuccess && detail.Message.Contains("골드를 더한다.") && detail.Message.Contains("amount (양): 더할 골드"), "help <명령>은 설명과 인자를 보인다.");
        Check(fixture.Executor.Execute("help player.hp").Message.Contains("읽기 전용"), "값 치트는 읽기·쓰기 여부를 보인다.");
        Check(!fixture.Executor.Execute("help nope").IsSuccess, "모르는 명령의 도움말은 실패다.");

        ExecutionResult cleared = fixture.Executor.Execute("clear");
        Check(cleared.IsSuccess && cleared.IsCleared && cleared.Lines.Count == 1 && cleared.Lines[0].Kind == EConsoleLineKind.Echo, "clear는 기록을 비우라고 알린다.");

        Check(fixture.Executor.Execute("history").Message.Contains("없습니다"), "입력 기록이 없으면 그렇게 알린다.");
        fixture.History.Add("player.gold 1");
        fixture.History.Add("help");
        Check(fixture.Executor.Execute("history").Message == "  player.gold 1\n  help", "history는 입력한 줄을 오래된 것부터 보인다.");
        Check(fixture.Registry.FindOrNull("help")!.IsShownInMenu == false && fixture.Registry.FindOrNull("player.gold")!.IsShownInMenu, "내장 명령은 메뉴에 보이지 않는다.");

        ExecutionResult propsAll = fixture.Executor.Execute("props");
        Check(propsAll.IsSuccess && propsAll.Message.Contains("프로퍼티 4개") && propsAll.Message.Contains("player.gold = 100") && propsAll.Message.Contains("player.hp = 7") && propsAll.Message.Contains("[읽기 전용]"), "props는 치트 프로퍼티와 지금 값을 보인다.");
        ExecutionResult propsFiltered = fixture.Executor.Execute("props gold");
        Check(propsFiltered.IsSuccess && propsFiltered.Message.Contains("프로퍼티 1개") && propsFiltered.Message.Contains("player.gold = 100"), "props <검색어>는 일치하는 프로퍼티만 걸러 보인다.");
        Check(fixture.Registry.FindOrNull("props")!.IsShownInMenu == false, "props 내장 명령은 메뉴에 보이지 않는다.");
    }

    // 메뉴는 위젯의 값을 명령 줄로 만들어 같은 Execute로 실행한다. 결과가 콘솔에서 친 것과 같아야 한다.
    private static void CheckMenuPath()
    {
        Fixture fixture = new Fixture();
        CheatDefinition addGold = fixture.Registry.FindOrNull("player.gold.add")!;
        string line = CheatExecutor.BuildLine(addGold, new[] { "5", "big win" });
        CheckEqual("player.gold.add 5 \"big win\"", line, "인자 글자로 명령 줄을 만든다.");
        CheckEqual("gold.reset", CheatExecutor.BuildLine(fixture.Registry.FindOrNull("gold.reset")!, Array.Empty<string>()), "인자가 없으면 이름뿐이다.");
        CheckEqual("item.give \"\"", CheatExecutor.BuildLine(fixture.Registry.FindOrNull("item.give")!, new[] { "" }), "빈 값은 빈 따옴표로 자리를 지킨다.");

        ExecutionResult fromMenu = fixture.Executor.Execute(line, "[메뉴] ");
        int goldAfterMenu = fixture.Cheats.Gold;
        ExecutionResult fromConsole = fixture.Executor.Execute(line);
        Check(fromMenu.Lines[0].Text == "[메뉴] > player.gold.add 5 \"big win\"" && fromConsole.Lines[0].Text == "> player.gold.add 5 \"big win\"", "출처는 실행한 줄 앞에만 붙는다.");
        Check(fromMenu.Message == fromConsole.Message && goldAfterMenu == 105 && fixture.Cheats.Gold == 110, "메뉴와 콘솔의 실행 결과가 같다.");

        // 메뉴가 범위 밖의 값을 넘겨도 콘솔과 같은 검증에 걸린다.
        ExecutionResult invalid = fixture.Executor.Execute(CheatExecutor.BuildLine(addGold, new[] { "0" }), "[메뉴] ");
        Check(!invalid.IsSuccess && invalid.Message.Contains("1~99999") && fixture.Cheats.Gold == 110, "메뉴 실행도 같은 검증을 거친다.");
    }

    private static void CheckHistory()
    {
        CommandHistory history = new CommandHistory();
        Check(!history.TryNavigateOlder("draft", out _) && !history.IsNavigating, "기록이 없으면 탐색하지 않는다.");

        history.Add("  first  ");
        history.Add("second");
        history.Add("second");
        history.Add("   ");
        history.Add("third");
        Check(history.Lines.Count == 3 && history.Lines[0] == "first" && history.Lines[2] == "third", "앞뒤 공백을 떼고, 빈 줄과 바로 앞과 같은 줄은 넣지 않는다.");

        Check(history.TryNavigateOlder("draft", out string line) && line == "third" && history.IsNavigating && history.NavigationNumber == 1, "↑는 가장 새 줄부터 보인다.");
        Check(history.TryNavigateOlder("ignored", out line) && line == "second" && history.NavigationNumber == 2, "↑를 더 누르면 더 옛 줄이다.");
        Check(history.TryNavigateOlder("", out line) && line == "first" && !history.TryNavigateOlder("", out _) && history.NavigationNumber == 3, "가장 옛 줄에서 멈춘다.");
        Check(history.TryNavigateNewer(out line) && line == "second" && history.TryNavigateNewer(out line) && line == "third", "↓는 새 줄로 돌아온다.");
        Check(history.TryNavigateNewer(out line) && line == "draft" && !history.IsNavigating, "가장 새 줄을 지나면 작성 중이던 입력을 돌려주고 탐색을 끝낸다.");
        Check(!history.TryNavigateNewer(out _), "탐색 중이 아니면 ↓는 아무것도 하지 않는다.");

        history.TryNavigateOlder("x", out _);
        history.Add("fourth");
        Check(!history.IsNavigating && history.TryNavigateOlder("y", out line) && line == "fourth", "줄을 더하면 탐색이 끝난다.");

        CommandHistory capped = new CommandHistory();
        for (int index = 0; index < CommandHistory.MAX_LINES + 5; index++)
        {
            capped.Add($"line {index}");
        }

        Check(capped.Lines.Count == CommandHistory.MAX_LINES && capped.Lines[0] == "line 5", "상한을 넘으면 가장 옛 줄부터 버린다.");

        for (int index = 0; index < CommandHistory.MAX_RECENT_CHEATS + 3; index++)
        {
            capped.RecordCheatUse($"cheat.{index}");
        }

        int versionBefore = capped.RecentVersion;
        capped.RecordCheatUse("CHEAT.5");
        Check(capped.RecentCheatIds.Count == CommandHistory.MAX_RECENT_CHEATS && capped.RecentCheatIds[0] == "CHEAT.5" && capped.RecentVersion == versionBefore + 1, "다시 쓴 치트는 앞으로 오고 중복되지 않는다.");
    }

    private static void CheckStorage()
    {
        MemoryStore store = new MemoryStore();
        CommandHistory history = new CommandHistory();
        history.Add("player.gold.add 5 \"big win\"");
        history.Add("help");
        history.RecordCheatUse("player.gold.add");
        history.RecordCheatUse("item.give");
        history.Save(store);

        CommandHistory loaded = new CommandHistory();
        loaded.Add("old line");
        loaded.Load(store);
        Check(loaded.Lines.Count == 2 && loaded.Lines[0] == "player.gold.add 5 \"big win\"" && loaded.Lines[1] == "help", "입력 기록을 저장하고 다시 읽는다.");
        Check(loaded.RecentCheatIds.Count == 2 && loaded.RecentCheatIds[0] == "item.give", "최근에 쓴 치트도 순서대로 다시 읽는다.");
        new CommandHistory().Load(new MemoryStore());

        FavoriteSet favorites = new FavoriteSet();
        Check(favorites.Toggle("player.gold") && favorites.Toggle("item.give") && favorites.Contains("PLAYER.GOLD"), "즐겨찾기를 더한다. id는 대소문자를 무시한다.");
        int versionBefore = favorites.Version;
        Check(!favorites.Toggle("Player.Gold") && !favorites.Contains("player.gold") && favorites.Version == versionBefore + 1, "다시 누르면 뺀다.");
        favorites.Toggle("player.god");
        favorites.Save(store);

        FavoriteSet loadedFavorites = new FavoriteSet();
        loadedFavorites.Load(store);
        Check(loadedFavorites.Ids.Count == 2 && loadedFavorites.Ids[0] == "item.give" && loadedFavorites.Ids[1] == "player.god", "즐겨찾기를 더한 순서대로 저장하고 다시 읽는다.");
    }
}
