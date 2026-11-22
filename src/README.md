# Lumera DebuggerKit

개발자 콘솔과 치트 메뉴다. 메서드에 `[Cheat]`를 붙이면 콘솔 명령, 자동완성, 인자 힌트, 도움말, 메뉴의 행이 모두 그 선언 하나에서 만들어진다. 게임 코드를 모르는 독립 패키지라서 다른 프로젝트에도 넣을 수 있다.

에디터와 Development Build에서만 컴파일된다(asmdef의 `defineConstraints`). 릴리스 빌드에 넣으려면 스크립팅 심볼 `LUMERA_DEBUGGERKIT`을 정의한다.

## 구성

| 폴더 | 어셈블리 | 내용 |
| --- | --- | --- |
| `Runtime/Core` | `Lumera.DebuggerKit` | 선언 특성, 치트 등록, 명령 줄 파서, 자동완성과 힌트, 실행, 입력 기록. UnityEngine을 쓰지 않는 C# 9 코드라 .NET에서도 컴파일하고 검사한다 |
| `Runtime/Unity` | `Lumera.DebuggerKit.Unity` | 호스트 컴포넌트와 화면(uGUI, TextMeshPro, Input System) |
| `Editor` | `Lumera.DebuggerKit.Editor` | 에디터 창, UI 프리팹 빌더, IL2CPP 빌드용 link.xml 생성 |

## 씬에 넣기

1. 메뉴 `Tools/Lumera/DebuggerKit/Build Prefabs`로 UI 프리팹을 만든다(`Runtime/Unity/Prefabs`).
2. 씬의 루트 오브젝트에 `DebuggerKitHost`를 붙이고 `Canvas Prefab`에 `DebuggerKitCanvas` 프리팹을 넣는다.
3. 여닫는 키는 호스트의 `Toggle Console`(기본 `` ` ``), `Toggle Menu`(기본 F1), `Close`(기본 Esc) 입력 액션에서 바꾼다. 화면의 안내와 버튼에는 지금 설정된 키 이름이 보인다.
4. 글꼴은 따로 넣지 않는다. 킷이 자기 글꼴(갈무리11)을 갖고 있다.

TMP Essential Resources가 프로젝트에 있어야 한다(비트맵 글꼴 셰이더 `TextMeshPro/Mobile/Bitmap`을 쓴다).

## 치트 선언

```csharp
using Lumera.DebuggerKit;

public sealed class TownCheats
{
    // 콘솔: player.gold.add 250      메뉴: "골드 추가" 행
    [Cheat("player.gold.add", Category = "플레이어", Name = "골드 추가", Description = "골드를 더한다.")]
    public string AddGold([Arg("양", Min = 1, Max = 100000, Step = 100)] int amount = 100)
    {
        _player.AddGold(amount);
        return $"골드 +{amount}";
    }

    // 콘솔: player.hp(읽기)           메뉴: 값이 보이는 행. setter가 있으면 위젯으로 바꿀 수 있다
    [CheatProperty("player.hp", Category = "플레이어", Name = "체력")]
    public int Hp => _player.Hp;

    // 인자 자리에 후보를 띄운다
    [Cheat("item.give", Category = "아이템", Name = "아이템 지급")]
    public CheatResult GiveItem([Arg("아이템"), Suggest(typeof(ItemSuggestions))] string id, int count = 1) { ... }
}
```

- **id**는 콘솔에 치는 이름이다. 점으로 조각을 나누면(`player.gold.add`) 조각 이름(`gold`)과 머리글자(`pga`)로도 찾는다.
- **Category**는 메뉴의 분류다. `/`로 하위 분류를 만든다(`"플레이어/재화"`).
- **기본값**은 C# 기본 매개변수 값을 그대로 쓴다. 기본값이 있는 인자는 생략할 수 있다.
- **반환형**은 `void`, `string`(성공 메시지), `CheatResult`(`Ok`/`Fail`)다.
- 깊은 곳이나 setter에서 실패를 알리려면 `CheatException`을 던진다. 메시지만 실패로 보이고 예외 로그는 남지 않는다. 다른 예외는 버그로 보고 로그를 남긴다.
- 인자 타입은 `string`, `bool`, `int`, `long`, `float`, `double`, 열거형을 지원한다. 다른 타입은 `IValueType`을 구현해 `Registry.ValueTypes.Register`로 더한다.
- 지원하지 않는 선언(타입, 반환형, 중복 id)은 등록하지 않고 경고 로그로 이유를 알린다.

### 등록

- **정적 메서드**는 호스트가 깨어날 때 저절로 등록된다. 디버거 킷을 참조하는 어셈블리만 훑는다.
- **인스턴스 메서드**는 대상 객체를 넘겨 등록한다. 판이나 씬이 바뀌면 이전 핸들을 버리고 다시 등록한다.

```csharp
IDisposable registration = DebuggerKitHost.CurrentOrNull.Register(new TownCheats(play));
// 판이 끝날 때
registration.Dispose();
```

### 자동완성 후보

```csharp
internal sealed class ItemSuggestions : ISuggestionProvider
{
    public void AddCandidates(SuggestionContext context, List<CandidateValue> output)
    {
        // TargetOrNull은 등록한 대상 객체다. 정적 상태 없이 지금 세션의 데이터를 찾는다.
        TownCheats cheats = (TownCheats)context.TargetOrNull;
        foreach (ItemRow row in cheats.Items)
        {
            output.Add(new CandidateValue(row.Id, row.Name));  // 값과, 옆에 보일 설명(검색에도 걸린다)
        }
    }
}
```

`context.PreviousArgs`로 앞 인자의 값을 볼 수 있다. 공급자가 없는 인자는 타입의 후보(열거형 이름, `true`/`false`)와 기본값을 띄운다.

## 게임과 잇기

호스트는 게임을 모른다. 게임 쪽에서 두 가지를 한다.

```csharp
DebuggerKitHost kit = DebuggerKitHost.CurrentOrNull;
kit.BlockingChanged += bBlocking => gameInput.IsSuspended = bBlocking;  // 콘솔·메뉴가 열려 있는 동안 게임 입력을 끈다
kit.Executed += result => hud.Refresh();                               // 치트를 실행한 뒤 화면을 다시 그린다
```

구독은 `OnDisable`에서 해제한다.

## 콘솔 쓰는 법

| 키 | 동작 |
| --- | --- |
| `` ` `` | 열기 / 닫기 |
| Tab | 후보를 넣고 다음 인자로 |
| → (줄 끝에서) | 회색으로 보이는 나머지 글자를 받는다 |
| ↓ / ↑ (후보가 떠 있을 때) | 후보를 고른다. 입력 줄까지 포함해 한 바퀴 돈다: 입력 줄에서 ↓는 첫 후보, ↑는 맨 아래 후보 |
| ↑ (후보가 없을 때) | 입력 기록. 빈 줄이거나 Esc로 후보를 닫은 뒤다. 기록을 보는 동안에는 ↑/↓로 넘긴다 |
| Enter | 고른 후보가 있으면 넣는다. 필수 인자가 남았으면 다음 인자를 입력할 자리로 간다. 아니면 실행한다 |
| Ctrl+Enter | 바로 실행 |
| PgUp / PgDn | 기록 스크롤 |
| Esc | 후보 목록을 닫는다. 한 번 더 누르면 콘솔을 닫는다 |
| Ctrl/⌘ + 휠, Ctrl/⌘ + `+` / `-` | 화면 배율을 0.25씩 바꾼다(콘솔이나 메뉴가 열려 있을 때) |
| Ctrl/⌘ + 0 | 화면 배율을 자동으로 되돌린다 |

- 공백이 든 값은 따옴표로 감싼다(`say "hello world"`). 따옴표 안의 따옴표는 `\"`다. 후보를 고르면 저절로 감싼다.
- 값 치트는 이름만 치면 읽고(`player.gold`), 값을 주면 쓴다(`player.gold 500`).
- `help`는 명령 목록, `help <명령>`은 사용법, `props`는 값 치트 목록과 지금 값, `clear`는 기록 비우기, `history`는 입력 기록이다.
- 콘솔을 열면 기록 맨 위에 짧은 안내가 적혀 있다.
- 기록에서 실행한 명령 줄(`> ...`)을 누르면 입력 줄로 다시 가져온다.
- 입력 기록과 최근에 쓴 치트, 즐겨찾기는 PlayerPrefs에 남는다.

## 치트 메뉴 쓰는 법

F1로 연다. 왼쪽에서 분류(전체, 즐겨찾기, 최근 사용, 분류 트리)를 고르고, 위의 검색칸에 치면 콘솔 자동완성과 같은 순위로 걸러진다.

- 행을 누르면 주 동작을 한다. 인자 없는 치트는 실행하고, 인자 있는 치트는 행 아래에 입력칸을 펴거나 접고, 스위치(bool 값)는 뒤집는다. 오른쪽의 `실행`·`입력` 버튼을 눌러도 같다. 목록을 끌어 스크롤할 때는 눌린 것으로 치지 않는다.
- 입력칸은 마지막으로 입력한 값으로 시작한다. 메뉴, 에디터 창, 콘솔 어디서 실행했든 같은 값이 남는다(`player.gold.add 250`을 콘솔에서 실행하면 메뉴의 입력칸도 250이다). 플레이가 끝나면 사라진다.
- 값 치트는 행 오른쪽의 위젯으로 바로 바꾼다. 화면의 값은 0.25초마다 다시 읽는다.
- 행의 ☆를 누르면 즐겨찾기에 넣는다.
- 인자 입력칸에 후보가 있으면, 입력을 시작할 때 칸 아래에 목록이 뜬다(포커스만 준 상태에서는 뜨지 않는다). 입력하지 않고 후보를 보려면 ↓를 누른다. 눌러서 고르거나 ↑/↓로 고른다.
- 메뉴에서 실행한 것도 콘솔 기록에 `[메뉴] > 명령 줄`로 남는다. 메뉴와 콘솔은 같은 실행 경로를 쓴다.
- 검색칸에서 ↑/↓로 행을 고르고 Enter로 실행하거나 편다.
- 인자 입력칸에서는 Enter로 실행한다. Tab은 다음 입력칸, Shift+Tab은 앞 칸으로 간다. 마지막 칸에서 Tab을 누르면 `실행` 버튼에 포커스가 가고(색이 밝아진다) Space나 Enter로 실행한다. 거기서 Tab을 한 번 더 누르면 첫 칸으로 돌아간다.

## 에디터 창

메뉴 `Tools/Lumera/DebuggerKit/Window`(Shift+F1)로 연다. 게임 화면을 가리지 않고 치트 메뉴와 콘솔을 쓴다. 플레이 중인 씬의 `DebuggerKitHost`에 붙어서 런타임 화면과 같은 치트, 콘솔 기록, 입력 기록, 즐겨찾기를 쓴다. 플레이 중이 아니면 쓸 수 없다.

런타임 화면은 uGUI, 에디터 창은 UI Toolkit으로 그리지만, 무엇을 보일지와 키 동작은 같은 Core 모델(`CheatMenuModel`, `ConsoleSession`)이 정한다.

## 화면 배율과 글꼴

- 글꼴은 갈무리11(`Runtime/Unity/Fonts/Galmuri11.ttf`, SIL OFL 1.1, 라이선스는 같은 폴더의 `Galmuri-OFL.txt`)이다. 12픽셀에서 글자가 픽셀 격자에 정확히 맞는 픽셀 글꼴이다.
- 실행할 때 TTF에서 비트맵 TMP 글꼴을 만든다. 글꼴 에셋을 저장소에 두지 않는 것은, 동적 아틀라스가 글자를 더할 때마다 에셋이 바뀌어 변경이 계속 남기 때문이다.
- 배율의 기본은 자동이다. 화면 높이 540픽셀마다 1배씩 정수로 키운다(720p는 1배, 1080p는 2배). 정수 배율에서는 글자 픽셀이 화면 픽셀에 딱 맞아 가장 또렷하다.
- 배율은 0.25 단위로 바꿀 수 있다(0.5~4). 콘솔의 `kit.scale 1.5`, 메뉴의 `UI 배율` 행, Ctrl/⌘+휠 어느 것으로 바꿔도 같고, 바꾼 값은 PlayerPrefs에 남는다. `kit.scale 0`이나 Ctrl/⌘+0은 자동으로 되돌린다.
- 소수 배율에서는 글자 픽셀이 고르게 나뉘지 않는다. 그때는 아틀라스를 선형 필터로 읽어 획 굵기가 들쭉날쭉해지지 않게 한다(조금 부드럽게 보인다).

## 구조

Core는 화면을 모르는 계산만 한다. 화면은 그 결과를 그린다.

| 타입 | 하는 일 |
| --- | --- |
| `CheatRegistry`, `CheatScanner` | 선언을 읽어 `CheatDefinition`으로 등록한다. 정의 하나가 실행·검증·도움말·자동완성·메뉴 행의 원천이다 |
| `CommandLine` | 입력 줄을 토큰으로 나눈다(따옴표, 이스케이프, 커서가 있는 토큰) |
| `TextMatcher`, `CheatSearch` | 일치 규칙(처음부터, 조각 처음부터, 중간, 머리글자)과 순위. 콘솔과 메뉴 검색이 같이 쓴다 |
| `ConsoleAssist` | 입력 줄과 커서 위치로 후보·힌트·인자 자리 안내(`AssistResult`)를 만든다 |
| `CheatExecutor` | 명령 줄을 실행한다. 콘솔과 메뉴가 같은 경로를 쓴다 |
| `CommandHistory`, `FavoriteSet` | 입력 기록, 최근에 쓴 치트, 즐겨찾기 |
| `ConsoleSession`, `ConsoleHintFormatter` | 콘솔 입력 줄의 상태(글자, 커서, 고른 후보, 기록 탐색)와 키 동작, 힌트 두 줄의 문장 |
| `ConsoleTranscript` | 콘솔에 보이는 기록. 런타임 콘솔과 에디터 창이 같은 기록을 그린다 |
| `CheatMenuModel` | 메뉴에 보일 것: 분류 목록, 묶음, 검색 순위, 펼친 치트 |
| `CheatDraftStore` | 치트마다 마지막으로 입력한 인자 값. 호스트가 하나 갖고 메뉴, 에디터 창, 콘솔 실행이 함께 쓴다 |
| `DebuggerKitHost` | 위의 것들을 갖고, 단축키와 창 여닫기, 화면 배율을 맡는다 |
| `ConsoleView`, `CheatMenuView` | 런타임 화면(uGUI) |
| `DebuggerKitWindow`, `ConsolePane`, `CheatMenuPane` | 에디터 창(UI Toolkit) |

검색 규칙을 늘리려면 `TextMatcher`만, 화면의 색과 크기를 바꾸려면 `DebuggerKitTheme`와 `DebuggerKitPrefabBuilder`만 고친다. 프리팹은 빌더가 매번 처음부터 다시 만들므로 손으로 고치지 않는다.
