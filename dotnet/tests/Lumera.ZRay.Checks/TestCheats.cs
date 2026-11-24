using System;
using System.Collections.Generic;

namespace Lumera.ZRay.Checks;

internal enum ETestElement
{
    Fire,
    Ice,
    Lightning
}

[Flags]
internal enum ETestFlags
{
    None = 0,
    A = 1,
    B = 2
}

internal readonly struct TestPoint
{
    public int X { get; }
    public int Y { get; }

    public TestPoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}

internal sealed class TestPointValueType : IValueType
{
    public string Label => "point";

    public bool TryParse(string text, out object? value, out string? errorOrNull)
    {
        string[] parts = text.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
        {
            value = new TestPoint(x, y);
            errorOrNull = null;
            return true;
        }

        value = null;
        errorOrNull = "x,y 형식으로 입력하세요.";
        return false;
    }

    public string Format(object? value)
    {
        return value is TestPoint point ? $"{point.X},{point.Y}" : "";
    }

    public void AddCandidates(List<string> candidates)
    {
    }
}

internal sealed class TestCheats
{
    public int Gold = 100;
    public ETestElement Element = ETestElement.Fire;
    public TestPoint Position;
    public readonly List<string> Given = new List<string>();
    public readonly List<(string Id, string Name)> Items = new List<(string Id, string Name)>
    {
        ("iron_sword", "철검"),
        ("long bow", "긴 활"),
        ("potion", "물약")
    };

    [Cheat("player.gold.add", Category = "플레이어/재화", Name = "골드 추가", Description = "골드를 더한다.")]
    public string AddGold([Arg("양", Description = "더할 골드", Min = 1, Max = 99999)] int amount, string reason = "cheat")
    {
        Gold += amount;
        return $"골드 +{amount} ({reason})";
    }

    [CheatProperty("player.gold", Category = "플레이어/재화", Name = "골드", Min = 0, Max = 1000)]
    public int GoldValue
    {
        get => Gold;
        set => Gold = value;
    }

    [CheatProperty("player.level", Category = "플레이어", Name = "레벨")]
    public int Level
    {
        get => _level;
        set => _level = value >= _level ? value : throw new CheatException("레벨은 내릴 수 없습니다.");
    }

    private int _level = 1;

    [CheatProperty("player.god", Category = "플레이어", Name = "무적")]
    public bool IsGodMode { get; set; }

    [CheatProperty("player.hp", Category = "플레이어", Name = "체력")]
    public int Hp => 7;

    [Cheat("item.give", Category = "아이템", Name = "아이템 지급")]
    public CheatResult GiveItem([Arg("아이템"), Suggest(typeof(TestItemSuggestions))] string itemId, int count = 1)
    {
        foreach ((string id, string _) in Items)
        {
            if (id == itemId)
            {
                Given.Add($"{itemId}x{count}");
                return CheatResult.Ok($"{itemId} {count}개 지급");
            }
        }

        return CheatResult.Fail($"'{itemId}' 아이템이 없습니다.");
    }

    [Cheat("world.element", Category = "월드", Name = "속성")]
    public void SetElement(ETestElement element = ETestElement.Ice)
    {
        Element = element;
    }

    [Cheat("world.explode", Category = "월드", Name = "폭발")]
    public void Explode()
    {
        throw new InvalidOperationException("폭발했다");
    }

    [Cheat("world.broken", Category = "월드", Name = "고장난 후보")]
    public void Broken([Suggest(typeof(ThrowingSuggestions))] string value)
    {
    }

    [Cheat("gold.reset", Category = "플레이어/재화", Name = "골드 초기화", Order = -1)]
    private void ResetGold()
    {
        Gold = 0;
    }

    [Cheat("player.teleport", Category = "플레이어", Name = "순간이동")]
    public string Teleport(TestPoint point)
    {
        Position = point;
        return $"{point.X},{point.Y}로 이동";
    }
}

internal sealed class TestItemSuggestions : ISuggestionProvider
{
    public void AddCandidates(SuggestionContext context, List<CandidateValue> output)
    {
        TestCheats cheats = (TestCheats)context.TargetOrNull!;
        foreach ((string id, string name) in cheats.Items)
        {
            output.Add(new CandidateValue(id, name));
        }
    }
}

internal sealed class ThrowingSuggestions : ISuggestionProvider
{
    public void AddCandidates(SuggestionContext context, List<CandidateValue> output)
    {
        output.Add(new CandidateValue("never"));
        throw new InvalidOperationException("공급자 고장");
    }
}

internal static class StaticTestCheats
{
    [Cheat("static.ping", Category = "검사", Name = "핑")]
    private static string Ping()
    {
        return "pong";
    }
}

internal sealed class FailingSuggestions<T> : ISuggestionProvider
{
    public FailingSuggestions()
    {
        throw new InvalidOperationException("공급자 생성 실패");
    }

    public void AddCandidates(SuggestionContext context, List<CandidateValue> output)
    {
    }
}

internal static class BadStaticCheats
{
    [Cheat("bad.constructor")]
    public static void Broken([Suggest(typeof(FailingSuggestions<int>))] string value)
    {
    }
}

internal static class GenericTestCheats<T>
{
    [Cheat("generic.action")]
    public static string Action()
    {
        return typeof(T).Name;
    }

    [CheatProperty("generic.property")]
    public static string Property => typeof(T).Name;

    [CheatProperty("generic.field")]
    public static readonly string FIELD = typeof(T).Name;
}

internal sealed class BadCheats
{
    [Cheat("bad.type")]
    public void UnsupportedType(DateTime when)
    {
    }

    [Cheat("bad.return")]
    public int UnsupportedReturn()
    {
        return 0;
    }

    [Cheat("bad.flags")]
    public void FlagsEnum(ETestFlags flags)
    {
    }

    [Cheat("bad id")]
    public void SpaceInId()
    {
    }

    [Cheat("bad.provider")]
    public void BadProvider([Suggest(typeof(string))] string value)
    {
    }

    [Cheat("bad.generic")]
    public void OpenMethod<T>()
    {
    }

    [Cheat("bad.open-provider")]
    public void OpenProvider([Suggest(typeof(FailingSuggestions<>))] string value)
    {
    }

    [Cheat("bad.null-provider")]
    public void MissingProvider([Suggest(null!)] string value)
    {
    }

    [CheatProperty("bad.value")]
    public DateTime UnsupportedValue { get; set; }
}

internal sealed class DuplicateCheats
{
    [Cheat("PLAYER.GOLD.ADD")]
    public void AddGoldAgain()
    {
    }

    [Cheat("unique.one")]
    public void Unique()
    {
    }
}

internal sealed class MemoryStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

    public string? GetStringOrNull(string key)
    {
        return _values.TryGetValue(key, out string? value) ? value : null;
    }

    public void SetString(string key, string value)
    {
        _values[key] = value;
    }
}
