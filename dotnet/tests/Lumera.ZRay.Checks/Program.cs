using System;
using static Lumera.ZRay.Checks.Checker;

namespace Lumera.ZRay.Checks;

// ZRay Core 검사를 모두 돌린다. 하나라도 실패하면 종료 코드 1을 돌려준다.
internal static class Program
{
    public static int Main()
    {
        Run(ZRayChecks.RunAll);

        Console.WriteLine($"ZRay checks: {CheckCount - FailureCount}/{CheckCount} passed.");
        return FailureCount == 0 ? 0 : 1;
    }
}
