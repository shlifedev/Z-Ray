using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lumera.ZRay.Checks;

internal static class Checker
{
    private static int _checkCount;
    private static int _failureCount;

    public static int CheckCount => _checkCount;
    public static int FailureCount => _failureCount;

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
