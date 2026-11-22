using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lumera.DebuggerKit.Checks;

// 모든 검사가 함께 쓰는 확인 함수와 집계. 각 검사 파일은 `using static Lumera.DebuggerKit.Checks.Checker;`로 가져다 쓴다.
// 확인이 실패해도 멈추지 않고 기록만 한다. 예상하지 못한 예외는 Run이 잡아 실패로 기록하고 다음 검사로 넘어간다.
internal static class Checker
{
    private static int _checkCount;
    private static int _failureCount;

    public static int CheckCount => _checkCount;
    public static int FailureCount => _failureCount;

    // 검사 하나를 실행한다. 이름은 호출한 식(메서드 이름이나 람다 본문)을 그대로 쓴다.
    public static void Run(Action test, [CallerArgumentExpression(nameof(test))] string name = "")
    {
        try
        {
            test();
        }
        catch (Exception exception)
        {
            Check(false, $"{name} raised an unexpected exception: {exception}");
        }
    }

    public static void Check(bool bCondition, string message)
    {
        _checkCount++;
        if (!bCondition)
        {
            _failureCount++;
            Console.Error.WriteLine($"FAIL: {message}");
        }
    }

    public static void CheckEqual<T>(T expected, T actual, string message)
    {
        bool bEqual = EqualityComparer<T>.Default.Equals(expected, actual);
        Check(bEqual, $"{message} Expected {expected}, got {actual}.");
    }

    public static void CheckThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            Check(true, message);
            return;
        }
        catch (Exception exception)
        {
            Check(false, $"{message} Expected {typeof(TException).Name}, got {exception.GetType().Name}.");
            return;
        }

        Check(false, $"{message} No exception was thrown.");
    }
}
