using System.Collections.Generic;
using System.Text;
using static Lumera.ZRay.Checks.Checker;

namespace Lumera.ZRay.Checks;

// 런타임 화면과 에디터 창이 함께 쓰는 모델: 콘솔 입력 상태, 힌트 문장, 출력 기록, 치트 메뉴 목록.
internal static partial class ZRayChecks
{
    private static ConsoleSession CreateSession(Fixture fixture)
    {
        return new ConsoleSession(fixture.Assist, fixture.Executor, fixture.History);
    }

    private static void Type(ConsoleSession session, string text)
    {
        session.OnTextEdited(text, text.Length);
    }

    private static void CheckSessionCompletion()
    {
        Fixture fixture = new Fixture();
        ConsoleSession session = CreateSession(fixture);
        Type(session, "pga");
        Check(session.IsSuggestionListVisible && session.Suggestions[0].Insert == "player.gold.add", "입력하면 후보가 뜬다.");
        int inputVersion = session.InputVersion;
        Check(session.HandleKey(EConsoleKey.Complete) && session.Text == "player.gold.add " && session.Caret == 16, "Tab은 첫 후보를 넣고 다음 인자로 간다.");
        Check(session.InputVersion == inputVersion + 1, "세션이 글자를 바꾸면 InputVersion이 올라 화면이 입력칸을 다시 쓴다.");
        CheckEqual("<amount:int> [reason:string=cheat]", session.TrailingHint, "후보가 없으면 남은 인자 자리를 흐리게 보인다.");

        Type(session, "player.go");
        CheckEqual("d", session.GhostText, "회색 완성은 첫 후보의 나머지다.");
        Check(session.HandleKey(EConsoleKey.AcceptGhost) && session.Text == "player.god ", "→는 회색 완성을 받는다.");
        Type(session, "player.gold.add 1");
        session.OnCaretMoved(3);
        Check(!session.HandleKey(EConsoleKey.AcceptGhost), "커서가 줄 끝이 아니면 →는 입력칸이 처리한다.");
    }

    private static void CheckSessionSelection()
    {
        Fixture fixture = new Fixture();
        ConsoleSession session = CreateSession(fixture);
        Type(session, "pla");
        session.HandleKey(EConsoleKey.Down);
        session.HandleKey(EConsoleKey.Down);
        Check(session.Selected == 1, "↓는 후보 목록을 내려간다.");
        CheckEqual("yer.gold", session.GhostText, "회색 완성은 고른 후보를 따른다.");
        session.HandleKey(EConsoleKey.Up);
        session.HandleKey(EConsoleKey.Up);
        Check(session.Selected == -1 && session.Text == "pla", "첫 후보에서 ↑는 입력 줄로 돌아온다.");

        // 후보가 떠 있으면 ↑/↓는 입력 줄까지 포함해 한 바퀴 돈다. 입력 줄에서 ↑는 기록이 아니라 맨 아래 후보다.
        fixture.History.Add("help");
        int last = session.Suggestions.Count - 1;
        session.HandleKey(EConsoleKey.Up);
        Check(session.Selected == last && session.Text == "pla" && !session.History.IsNavigating, "후보가 떠 있을 때 입력 줄에서 ↑는 맨 아래 후보를 고른다.");
        session.HandleKey(EConsoleKey.Down);
        Check(session.Selected == -1, "맨 아래 후보에서 ↓는 입력 줄로 돌아온다.");
        session.HandleKey(EConsoleKey.Dismiss);
        session.HandleKey(EConsoleKey.Up);
        Check(session.Text == "help" && session.History.IsNavigating, "Esc로 후보를 닫은 뒤의 ↑는 기록이다.");
        Type(session, "pla");
        session.HandleKey(EConsoleKey.Down);
        session.HandleKey(EConsoleKey.Submit);
        Check(session.Text == "player.god " && !fixture.Cheats.IsGodMode, "후보를 고른 채 Enter는 실행하지 않고 후보를 넣는다.");

        Type(session, "pla");
        Check(session.HandleKey(EConsoleKey.Dismiss) && !session.IsSuggestionListVisible, "Esc는 후보 목록만 닫는다.");
        Check(!session.HandleKey(EConsoleKey.Dismiss), "닫을 후보가 없으면 Esc를 처리하지 않는다(콘솔이 닫힌다).");
        Type(session, "play");
        Check(session.IsSuggestionListVisible, "입력이 바뀌면 후보 목록이 다시 뜬다.");

        session.Accept(99);
        Check(session.Text == "play", "없는 후보 번호는 무시한다.");
    }

    private static void CheckSessionSubmit()
    {
        Fixture fixture = new Fixture();
        ConsoleSession session = CreateSession(fixture);
        List<ExecutionResult> executed = new List<ExecutionResult>();
        fixture.Executor.Executed += executed.Add;

        Type(session, "player.gold.add");
        session.HandleKey(EConsoleKey.Submit);
        Check(executed.Count == 0 && session.Text == "player.gold.add " && session.Notice == "양 입력 필요", "필수 인자가 남았으면 실행하지 않고 다음 인자 자리로 간다.");
        Type(session, "player.gold.add 5");
        Check(session.Notice.Length == 0, "입력하면 안내를 지운다.");
        session.HandleKey(EConsoleKey.Submit);
        Check(executed.Count == 1 && fixture.Cheats.Gold == 105 && session.Text.Length == 0 && fixture.History.Lines[0] == "player.gold.add 5", "실행하면 기록에 남기고 입력 줄을 비운다.");

        Type(session, "player.gold.add");
        session.HandleKey(EConsoleKey.ForceSubmit);
        Check(executed.Count == 2 && !executed[1].IsSuccess, "Ctrl+Enter는 빠진 인자가 있어도 바로 실행한다(실행기가 사용법을 알린다).");
        Type(session, "   ");
        session.HandleKey(EConsoleKey.Submit);
        Check(executed.Count == 2, "빈 줄은 실행하지 않는다.");
    }

    private static void CheckSessionHistory()
    {
        Fixture fixture = new Fixture();
        ConsoleSession session = CreateSession(fixture);
        Type(session, "help");
        session.HandleKey(EConsoleKey.ForceSubmit);
        Type(session, "player.gold");
        session.HandleKey(EConsoleKey.ForceSubmit);

        Type(session, "");
        session.HandleKey(EConsoleKey.Up);
        Check(session.Text == "player.gold" && session.History.IsNavigating && !session.IsSuggestionListVisible, "빈 입력 줄에서 ↑는 기록이고, 기록을 보는 동안 후보 목록은 숨긴다.");
        session.HandleKey(EConsoleKey.Up);
        Check(session.Text == "help", "↑를 더 누르면 더 옛 줄이다.");
        session.HandleKey(EConsoleKey.Down);
        session.HandleKey(EConsoleKey.Down);
        Check(session.Text.Length == 0 && !session.History.IsNavigating, "↓로 끝까지 오면 작성 중이던 입력으로 돌아온다.");

        session.Recall("item.give potion 2");
        Check(session.Text == "item.give potion 2" && session.Caret == session.Text.Length, "기록의 명령 줄을 다시 가져온다.");
    }

    private static void CheckHintFormatter()
    {
        Fixture fixture = new Fixture();
        ConsoleSession session = CreateSession(fixture);
        List<StyledText> first = new List<StyledText>();
        List<StyledText> second = new List<StyledText>();

        ConsoleHintFormatter.Build(session, fixture.Registry, first, second);
        Check(first.Count == 0 && second.Count == 0, "빈 줄에는 하단 조작 안내를 보이지 않는다.");

        Type(session, "pga");
        ConsoleHintFormatter.Build(session, fixture.Registry, first, second);
        Check(Joined(first).StartsWith("player.gold.add <amount") && first[0].Style == ETextStyle.Muted && Joined(second) == "골드 추가 — 골드를 더한다.", "명령 이름을 치는 중에는 들어갈 후보를 흐리게 미리 보인다.");

        Type(session, "player.gold.add ");
        ConsoleHintFormatter.Build(session, fixture.Registry, first, second);
        Check(first[0].Style == ETextStyle.Normal && first[2].Style == ETextStyle.Accent && first[4].Style == ETextStyle.Muted, "입력 중인 인자를 강조한다.");
        Check(Joined(second) == "양 — 더할 골드" && second[0].Style == ETextStyle.Accent, "둘째 줄은 입력 중인 인자의 설명이다.");

        Type(session, "player.gold.add 0");
        ConsoleHintFormatter.Build(session, fixture.Registry, first, second);
        Check(first[2].Style == ETextStyle.Error && second[0].Style == ETextStyle.Error && Joined(second).Contains("1~99999"), "틀린 인자는 빨갛게 보이고 이유를 알린다.");

        Type(session, "nope 1");
        ConsoleHintFormatter.Build(session, fixture.Registry, first, second);
        Check(first[0].Style == ETextStyle.Error && Joined(first).Contains("nope"), "모르는 명령을 알린다.");

        Type(session, "player.gold.add");
        session.HandleKey(EConsoleKey.Submit);
        ConsoleHintFormatter.Build(session, fixture.Registry, first, second);
        Check(Joined(second) == "양 입력 필요", "Enter로 다음 인자로 옮겼으면 무엇을 입력할지 알린다.");

        // 리치 텍스트: 글자는 noparse로 감싸 태그로 읽히지 않는다.
        StringBuilder builder = new StringBuilder();
        RichText.AppendStyled(builder, new[] { new StyledText("<b>", ETextStyle.Error) }, static style => style == ETextStyle.Error ? "#F00" : "#FFF");
        CheckEqual("<color=#F00><noparse><b></noparse></color>", builder.ToString(), "조각마다 색을 입히고 글자는 noparse로 감싼다.");
    }

    private static void CheckTranscript()
    {
        Fixture fixture = new Fixture();
        ConsoleTranscript transcript = new ConsoleTranscript();
        transcript.Write("첫 줄\n둘째 줄", EConsoleLineKind.Muted);
        Check(transcript.Lines.Count == 2 && transcript.Lines[1].Text == "둘째 줄" && transcript.Lines[0].Kind == EConsoleLineKind.Muted && transcript.Lines[0].Recall.Length == 0, "Write는 줄마다 나눠 쓴다.");

        int version = transcript.Version;
        transcript.Append(fixture.Executor.Execute("player.gold.add 5"));
        Check(transcript.Version == version + 1 && transcript.Lines.Count == 4, "실행 결과를 더하면 Version이 오른다.");
        Check(transcript.Lines[2].Kind == EConsoleLineKind.Echo && transcript.Lines[2].Recall == "player.gold.add 5" && transcript.Lines[3].Recall.Length == 0, "실행한 명령 줄만 다시 가져올 명령을 갖는다.");

        version = transcript.Version;
        transcript.Append(fixture.Executor.Execute(""));
        Check(transcript.Version == version, "빈 결과는 기록을 바꾸지 않는다.");
        transcript.Append(fixture.Executor.Execute("clear"));
        Check(transcript.Lines.Count == 0, "clear는 기록을 비운다.");

        for (int index = 0; index < ConsoleTranscript.MAX_LINES + 10; index++)
        {
            transcript.Write($"줄 {index}");
        }

        Check(transcript.Lines.Count == ConsoleTranscript.MAX_LINES && transcript.Lines[0].Text == "줄 10", "상한을 넘으면 가장 옛 줄부터 버린다.");
    }

    private static void CheckMenuModel()
    {
        Fixture fixture = new Fixture();
        FavoriteSet favorites = new FavoriteSet();
        CheatMenuModel model = new CheatMenuModel(fixture.Registry, favorites, fixture.History);
        Check(model.Refresh() && !model.Refresh(), "처음에는 목록을 만들고, 바뀐 것이 없으면 다시 만들지 않는다.");

        // 분류: 고정 항목 넷 + 경로(상위 경로 포함). 콘솔 명령은 세지 않는다.
        IReadOnlyList<MenuCategory> categories = model.Categories;
        Check(categories[0].Key == CheatMenuModel.ALL_KEY && categories[0].Count == 11 && categories[1].Key == CheatMenuModel.FAVORITES_KEY && categories[2].Key == CheatMenuModel.RECENT_KEY && categories[3].Key == CheatMenuModel.PROPERTIES_KEY, "맨 위는 전체, 즐겨찾기, 최근 사용, 프로퍼티다.");
        Check(categories[3].Count == 4, "프로퍼티 분류는 값 치트 수를 센다.");
        MenuCategory parent = FindCategory(model, "플레이어");
        MenuCategory child = FindCategory(model, "플레이어/재화");
        Check(parent.Count == 7 && parent.Depth == 0 && child.Count == 3 && child.Depth == 1 && child.Title == "재화", "상위 분류는 하위 분류의 치트까지 세고, 하위 분류는 이름만 보인다.");

        // 전체: 분류마다 묶음. 즐겨찾기가 없으면 즐겨찾기 묶음은 없다.
        Check(model.Entries[0].IsGroup && model.Entries[0].GroupKey == "아이템" && model.Entries[1].DefinitionOrNull!.Id == "item.give", "분류마다 묶음 제목과 치트가 이어진다.");
        Check(FindGroup(model, "플레이어/재화") >= 0 && FindGroup(model, CheatMenuModel.FAVORITES_KEY) < 0, "즐겨찾기가 없으면 그 묶음을 만들지 않는다.");

        favorites.Toggle("world.element");
        Check(model.Refresh() && model.Entries[0].GroupKey == CheatMenuModel.FAVORITES_KEY && model.Entries[1].DefinitionOrNull!.Id == "world.element", "즐겨찾기가 바뀌면 다시 만들고, 전체의 맨 위에 즐겨찾기 묶음을 둔다.");

        model.ToggleCollapsed("월드");
        model.Refresh();
        int worldGroup = FindGroup(model, "월드");
        Check(model.Entries[worldGroup].IsGroupCollapsed && (worldGroup + 1 == model.Entries.Count || model.Entries[worldGroup + 1].IsGroup), "접은 묶음은 제목만 남는다.");

        model.Category = "플레이어";
        model.Refresh();
        Check(FindGroup(model, "플레이어") >= 0 && FindGroup(model, "플레이어/재화") >= 0 && FindGroup(model, "아이템") < 0, "분류를 고르면 그 분류와 하위 분류만 보인다.");

        model.Category = CheatMenuModel.PROPERTIES_KEY;
        model.Refresh();
        Check(model.Entries.Count > 0 && model.Entries[0].IsGroup, "프로퍼티 분류는 값 치트를 분류별로 묶어 보인다.");
        foreach (MenuEntry entry in model.Entries)
        {
            if (!entry.IsGroup)
            {
                Check(entry.DefinitionOrNull!.Kind == ECheatKind.Value, "프로퍼티 분류에는 값 치트만 보인다.");
            }
        }

        // 검색: 묶음 없이 순위대로.
        model.Category = CheatMenuModel.ALL_KEY;
        model.Query = "  gold ";
        model.Refresh();
        Check(model.IsSearching && !model.Entries[0].IsGroup && model.Entries[0].DefinitionOrNull!.Id == "gold.reset" && model.Entries[1].DefinitionOrNull!.Id == "player.gold", "검색하면 묶음을 풀고 콘솔과 같은 순위로 보인다.");
        model.Query = "없는말";
        model.Refresh();
        Check(model.Entries.Count == 0, "일치하는 것이 없으면 비어 있다.");
        model.Query = "";

        // 최근 사용: 실행기가 남긴 순서대로.
        fixture.Executor.Execute("player.god on");
        fixture.Executor.Execute("gold.reset");
        model.Category = CheatMenuModel.RECENT_KEY;
        Check(model.Refresh() && model.Entries.Count == 2 && model.Entries[0].DefinitionOrNull!.Id == "gold.reset" && FindCategory(model, CheatMenuModel.RECENT_KEY).Count == 2, "최근 사용은 방금 쓴 것부터 보인다.");

        // 판이 바뀌어 고른 분류의 치트가 모두 빠지면 전체로 돌아간다.
        model.Category = "플레이어/재화";
        model.Refresh();
        fixture.Registration.Dispose();
        Check(model.Refresh() && model.Category == CheatMenuModel.ALL_KEY && model.Categories[0].Count == 0, "고른 분류가 사라지면 전체로 돌아간다.");
    }

    private static void CheckMenuDrafts()
    {
        Fixture fixture = new Fixture();
        CheatMenuModel model = new CheatMenuModel(fixture.Registry, new FavoriteSet(), fixture.History);
        CheatDefinition addGold = fixture.Registry.FindOrNull("player.gold.add")!;
        string[] drafts = model.GetDrafts(addGold);
        Check(drafts.Length == 2 && drafts[0] == "" && drafts[1] == "cheat" && model.GetDrafts(addGold) == drafts, "초안은 기본값으로 시작하고 같은 배열을 계속 쓴다.");

        drafts[0] = "5";
        CheckEqual("player.gold.add 5 cheat", model.BuildLine(addGold), "초안으로 명령 줄을 만든다.");
        drafts[1] = "";
        CheckEqual("player.gold.add 5", model.BuildLine(addGold), "비워 둔 뒤쪽 선택 인자는 생략한다.");
        drafts[0] = "";
        CheckEqual("player.gold.add \"\"", model.BuildLine(addGold), "비워 둔 필수 인자는 자리를 지켜 실행기가 이유를 알리게 한다.");
        CheckEqual("player.gold 30", CheatMenuModel.BuildValueLine(fixture.Registry.FindOrNull("player.gold")!, "30"), "값 치트에 쓰는 명령 줄.");

        // ± 스테퍼: Step과 범위를 지키고, 숫자가 아니면 하한에서 시작한다.
        CheatParameter amount = addGold.Parameters[0];
        CheckEqual("2", amount.StepText("1", 1), "Step이 없으면 1씩 움직인다.");
        CheckEqual("1", amount.StepText("1", -1), "하한 밑으로 내려가지 않는다.");
        CheckEqual("99999", amount.StepText("99999", 1), "상한 위로 올라가지 않는다.");
        CheckEqual("2", amount.StepText("abc", 1), "숫자가 아니면 하한(1)에서 시작한다.");
        CheatParameter gold = fixture.Registry.FindOrNull("player.gold")!.Parameters[0];
        CheckEqual("1000", gold.StepText("999", 1), "값 치트도 범위를 지킨다.");
        CheckEqual("cheat", addGold.Parameters[1].StepText("cheat", 1), "숫자가 아닌 인자는 그대로 둔다.");

        Check(!model.IsExpanded(addGold) && model.ToggleExpanded(addGold) && model.IsExpanded(addGold) && !model.ToggleExpanded(addGold), "치트 입력칸을 펴고 접는다.");
        Check(model.Refresh(), "펴고 접으면 목록을 다시 만든다.");
    }

    // 입력값 보관소: 메뉴 두 개(런타임, 에디터 창)와 콘솔 실행이 같은 값을 본다.
    private static void CheckDraftStore()
    {
        Fixture fixture = new Fixture();
        CheatDraftStore store = new CheatDraftStore();
        CheatExecutor executor = new CheatExecutor(fixture.Registry, fixture.History, store);
        CheatMenuModel runtime = new CheatMenuModel(fixture.Registry, new FavoriteSet(), fixture.History, store);
        CheatMenuModel editor = new CheatMenuModel(fixture.Registry, new FavoriteSet(), fixture.History, store);
        CheatDefinition addGold = fixture.Registry.FindOrNull("player.gold.add")!;

        runtime.GetDrafts(addGold)[0] = "7";
        Check(editor.GetDrafts(addGold)[0] == "7" && editor.GetDrafts(addGold) == runtime.GetDrafts(addGold), "보관소를 함께 쓰는 메뉴는 같은 입력값을 본다.");

        executor.Execute("player.gold.add 250");
        Check(runtime.GetDrafts(addGold)[0] == "250" && runtime.GetDrafts(addGold)[1] == "cheat", "콘솔에서 실행한 인자를 기억하고, 생략한 인자는 그대로 둔다.");
        executor.Execute("player.gold.add 30 \"big win\"");
        CheckEqual("player.gold.add 30 \"big win\"", runtime.BuildLine(addGold), "기억한 값으로 같은 명령 줄을 다시 만든다.");

        executor.Execute("player.gold.add 0 oops");
        executor.Execute("player.gold.add many");
        Check(runtime.GetDrafts(addGold)[0] == "30", "인자가 틀려 실행하지 못한 줄은 기억하지 않는다.");
        executor.Execute("player.gold 500");
        Check(store.Get(fixture.Registry.FindOrNull("player.gold")!)[0] == "", "값 치트는 기억하지 않는다(지금 값이 입력칸의 값이다).");

        // 판이 바뀌어 치트가 다시 등록돼도 id가 같으면 값이 남는다.
        fixture.Registration.Dispose();
        fixture.Registry.Register(new TestCheats());
        CheatDefinition again = fixture.Registry.FindOrNull("player.gold.add")!;
        Check(again != addGold && store.Get(again)[0] == "30", "다시 등록된 치트도 같은 입력값을 쓴다.");
        Check(new CheatMenuModel(fixture.Registry, new FavoriteSet(), fixture.History).GetDrafts(again)[0] == "", "보관소를 주지 않은 모델은 자기 것만 쓴다.");
    }

    private static MenuCategory FindCategory(CheatMenuModel model, string key)
    {
        foreach (MenuCategory category in model.Categories)
        {
            if (category.Key == key)
            {
                return category;
            }
        }

        return new MenuCategory("?", "?", -1, -1);
    }

    private static int FindGroup(CheatMenuModel model, string key)
    {
        for (int index = 0; index < model.Entries.Count; index++)
        {
            if (model.Entries[index].IsGroup && model.Entries[index].GroupKey == key)
            {
                return index;
            }
        }

        return -1;
    }

    private static string Joined(List<StyledText> parts)
    {
        StringBuilder builder = new StringBuilder();
        foreach (StyledText part in parts)
        {
            builder.Append(part.Text);
        }

        return builder.ToString();
    }
}
