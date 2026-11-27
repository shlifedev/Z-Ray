using System;
using static Lumera.ZRay.Checks.Checker;

namespace Lumera.ZRay.Checks;

internal static class Program
{
    public static int Main()
    {
        Run(ZRayChecks.RunAll);

        Console.WriteLine($"ZRay checks: {CheckCount - FailureCount}/{CheckCount} passed.");
        return FailureCount == 0 ? 0 : 1;
    }
}
