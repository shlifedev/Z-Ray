using System;
using static Lumera.DebuggerKit.Checks.Checker;

namespace Lumera.DebuggerKit.Checks;

// 디버거 킷 Core 검사를 모두 돌린다. 하나라도 실패하면 종료 코드 1을 돌려준다.
internal static class Program
{
    public static int Main()
    {
        Run(DebuggerKitChecks.RunAll);

        Console.WriteLine($"DebuggerKit checks: {CheckCount - FailureCount}/{CheckCount} passed.");
        return FailureCount == 0 ? 0 : 1;
    }
}
