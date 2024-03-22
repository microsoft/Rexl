// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#undef INVARIANT_CHECKS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Microsoft.Rexl.Private;

/// <summary>
/// Checks, bug-checks and asserts.
/// </summary>
internal static class Validation
{
    /// <summary>
    /// This is used to highlight code coverage, nothing more. Typical use is: Coverage(condition ? 1 : 0).
    /// Code coverage will color the line differently if only one branch was taken.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Coverage(int n)
    {
    }

    /// <summary>
    /// This is used to highlight code coverage, nothing more. Typical use is: Coverage(condition ? "widget" : "thingy").
    /// Code coverage will color the line differently if only one branch was taken.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Coverage(string s)
    {
    }

    #region Assert variations

    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool f)
    {
#if DEBUG
        if (!f)
            DbgFail();
#endif
    }

    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool f, string msg)
    {
#if DEBUG
        if (!f)
            DbgFail(msg);
#endif
    }

#pragma warning disable 8777 // Parameter must have a non-null value when exiting.
    // Doesn't ensure param is non-null but the nullable analysis still honors [NotNull] in all builds
    // to make code easier to write.
    [Conditional("DEBUG")]
    public static void AssertValue<T>([NotNull] T? val)
        where T : class
    {
#if DEBUG
        if (val is null)
            DbgFailValue();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertValue<T>([NotNull] T? val, string msg)
        where T : class
    {
#if DEBUG
        if (val is null)
            DbgFail(msg);
#endif
    }
#pragma warning restore 8777 // Parameter must have a non-null value when exiting.

    [Conditional("INVARIANT_CHECKS")]
    public static void AssertValueOrNull<T>(T? val)
        where T : class
    {
    }

    [Conditional("DEBUG")]
    public static void AssertAllValues<T>(IList<T>? args)
        where T : class
    {
#if DEBUG
        int size = Size(args);
        for (int i = 0; i < size; i++)
        {
            if (args![i] is null)
                DbgFail();
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertAllValues<T>(IEnumerable<T>? args)
        where T : class
    {
#if DEBUG
        if (args != null)
        {
            foreach (T item in args)
            {
                if (item is null)
                    DbgFail();
            }
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertIndex(int index, int lim)
    {
#if DEBUG
        if (!IsValidIndex(index, lim))
            DbgFail();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertIndex(long index, long lim)
    {
#if DEBUG
        if (!IsValidIndex(index, lim))
            DbgFail();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertIndexInclusive(int index, int max)
    {
#if DEBUG
        if (!IsValidIndexInclusive(index, max))
            DbgFail();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertIndexInclusive(long index, long max)
    {
#if DEBUG
        if (!IsValidIndexInclusive(index, max))
            DbgFail();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertInRange(int index, int min, int lim)
    {
#if DEBUG
        if (!IsInRange(index, min, lim))
            DbgFail();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertInRange(long index, long min, long lim)
    {
#if DEBUG
        if (!IsInRange(index, min, lim))
            DbgFail();
#endif
    }

#pragma warning disable 8777 // Parameter must have a non-null value when exiting.
    // Doesn't ensure param is non-null but the nullable analysis still honors [NotNull] in all builds
    // to make code easier to write.
    [Conditional("DEBUG")]
    public static void AssertNonEmpty([NotNull] string? s)
    {
#if DEBUG
        if (string.IsNullOrEmpty(s))
            DbgFailEmpty();
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertNonEmpty<T>([NotNull] IReadOnlyCollection<T>? args)
    {
#if DEBUG
        if (args is null || args.Count == 0)
            DbgFailEmpty();
#endif
    }
#pragma warning restore 8777

    [Conditional("DEBUG")]
    public static void AssertNonEmptyOrNull(string? s)
    {
#if DEBUG
        if (s != null)
            AssertNonEmpty(s);
#endif
    }

    [Conditional("DEBUG")]
    public static void AssertNonEmptyOrNull<T>(IReadOnlyCollection<T>? args)
    {
#if DEBUG
        if (args != null)
            AssertNonEmpty(args);
#endif
    }

    #endregion Assert variations

    #region Verify variations

    // Verify is used to assert a value in debug, and act as a pass through in retail.

    public static bool Verify(this bool f)
    {
        Assert(f);
        return f;
    }

    public static T VerifyValue<T>(this T val)
        where T : class
    {
        AssertValue(val);
        return val;
    }

    #endregion Verify variations

    #region Check variations

    public static void Check(bool f)
    {
        if (!f)
            throw Except();
    }

    public static void Check(bool f, string msg)
    {
        if (!f)
            throw Except(msg);
    }

    public static void CheckParam(bool f, string paramName)
    {
        if (!f)
            throw ExceptParam(paramName);
    }

    public static void CheckParam(bool f, string paramName, string msg)
    {
        if (!f)
            throw ExceptParam(paramName, msg);
    }

    public static void CheckRange(bool f, string paramName)
    {
        if (!f)
            throw ExceptRange(paramName);
    }

    public static void CheckRange(bool f, string paramName, string msg)
    {
        if (!f)
            throw ExceptRange(paramName, msg);
    }

    public static void CheckValue<T>([NotNull] T? val, string paramName)
        where T : class
    {
        if (val is null)
            throw ExceptValue(paramName);
    }

    public static void CheckValue<T>([NotNull] T? val, string paramName, string msg)
        where T : class
    {
        if (val is null)
            throw ExceptValue(paramName, msg);
    }

    [Conditional("INVARIANT_CHECKS")]
    public static void CheckValueOrNull<T>(T? val)
        where T : class
    {
    }

    public static void CheckAllValues<T>(IList<T>? args, string paramName)
        where T : class
    {
        int size = Size(args);
        for (int i = 0; i < size; i++)
        {
            if (args![i] is null)
                throw ExceptParam(paramName);
        }
    }

    public static void CheckNonEmpty([NotNull] string? s, string paramName)
    {
        if (string.IsNullOrEmpty(s))
            throw ExceptEmpty(paramName);
    }

    public static void CheckNonEmpty([NotNull] string? s, string paramName, string msg)
    {
        if (string.IsNullOrEmpty(s))
            throw ExceptEmpty(paramName, msg);
    }

    public static void CheckNonEmpty<T>([NotNull] IList<T>? args, string paramName)
    {
        if (args == null || args.Count == 0)
            throw ExceptEmpty(paramName);
    }

    public static void CheckNonEmptyOrNull(string? s, string paramName)
    {
        if (s != null && s.Length == 0)
            throw ExceptEmpty(paramName);
    }

    public static void CheckAllNonEmpty(IList<string?>? args, string paramName)
    {
        int size = Size(args);
        for (int i = 0; i < size; i++)
        {
            if (string.IsNullOrEmpty(args![i]))
                throw ExceptEmpty(paramName);
        }
    }

    public static void CheckIndex(int index, int lim, string paramName)
    {
        if (!IsValidIndex(index, lim))
            throw ExceptRange(paramName);
    }

    public static void CheckIndex(int index, int lim, string paramName, string msg)
    {
        if (!IsValidIndex(index, lim))
            throw ExceptRange(paramName, msg);
    }

    public static void CheckIndexInclusive(int index, int lim, string paramName)
    {
        if (!IsValidIndexInclusive(index, lim))
            throw ExceptRange(paramName);
    }

    public static void CheckIndexInclusive(int index, int max, string paramName, string msg)
    {
        if (!IsValidIndexInclusive(index, max))
            throw ExceptRange(paramName, msg);
    }

    #endregion Check variations

    #region BugCheck variations

    public static void BugCheck([DoesNotReturnIf(false)] bool f)
    {
        if (!f)
            throw BugExcept();
    }

    public static void BugCheck([DoesNotReturnIf(false)] bool f, string msg)
    {
        if (!f)
            throw BugExcept(msg);
    }

    public static void BugCheckParam([DoesNotReturnIf(false)] bool f, string paramName)
    {
        if (!f)
            throw BugExceptParam(paramName);
    }

    public static void BugCheckParam([DoesNotReturnIf(false)] bool f, string paramName, string msg)
    {
        if (!f)
            throw BugExceptParam(paramName, msg);
    }

    public static void BugCheckRange([DoesNotReturnIf(false)] bool f, string paramName)
    {
        if (!f)
            throw BugExceptRange(paramName);
    }

    public static void BugCheckRange([DoesNotReturnIf(false)] bool f, string paramName, string msg)
    {
        if (!f)
            throw BugExceptRange(paramName, msg);
    }

    public static void BugCheckValue<T>([NotNull] T? val, string paramName)
        where T : class
    {
        if (val is null)
            throw BugExceptValue(paramName);
    }

    public static void BugCheckValue<T>([NotNull] T? val, string paramName, string msg)
        where T : class
    {
        if (val is null)
            throw BugExceptValue(paramName, msg);
    }

    [Conditional("INVARIANT_CHECKS")]
    public static void BugCheckValueOrNull<T>(T? val)
        where T : class
    {
    }

    public static void BugCheckAllValues<T>(IList<T>? args, string paramName)
        where T : class
    {
        int size = Size(args);
        for (int i = 0; i < size; i++)
        {
            if (args![i] is null)
                throw BugExceptParam(paramName);
        }
    }

    public static void BugCheckAllValues<T>(IEnumerable<T>? args, string paramName)
        where T : class
    {
        if (args != null)
        {
            foreach (T item in args)
            {
                if (item is null)
                    throw BugExceptParam(paramName);
            }
        }
    }

    public static void BugCheckNonEmpty([NotNull] string? s, string paramName)
    {
        if (string.IsNullOrEmpty(s))
            throw BugExceptEmpty(paramName);
    }

    public static void BugCheckNonEmpty([NotNull] string? s, string paramName, string msg)
    {
        if (string.IsNullOrEmpty(s))
            throw BugExceptEmpty(paramName, msg);
    }

    public static void BugCheckNonEmpty<T>([NotNull] IList<T>? args, string paramName)
    {
        if (args == null || args.Count == 0)
            throw BugExceptEmpty(paramName);
    }

    public static void BugCheckNonEmptyOrNull(string? s, string paramName)
    {
        if (s != null && s.Length == 0)
            throw BugExceptEmpty(paramName);
    }

    public static void BugCheckAllNonEmpty(IList<string?>? args, string paramName)
    {
        int size = Size(args);
        for (int i = 0; i < size; i++)
        {
            if (string.IsNullOrEmpty(args![i]))
                throw BugExceptEmpty(paramName);
        }
    }

    public static void BugCheckIndex(int index, int lim, string paramName)
    {
        if (!IsValidIndex(index, lim))
            throw BugExceptRange(paramName);
    }

    public static void BugCheckIndex(long index, long lim, string paramName)
    {
        if (!IsValidIndex(index, lim))
            throw BugExceptRange(paramName);
    }

    public static void BugCheckIndex(int index, int lim, string paramName, string msg)
    {
        if (!IsValidIndex(index, lim))
            throw BugExceptRange(paramName, msg);
    }

    public static void BugCheckIndexInclusive(int index, int max, string paramName)
    {
        if (!IsValidIndexInclusive(index, max))
            throw BugExceptRange(paramName);
    }

    public static void BugCheckIndexInclusive(long index, long max, string paramName)
    {
        if (!IsValidIndexInclusive(index, max))
            throw BugExceptRange(paramName);
    }

    public static void BugCheckIndexInclusive(int index, int max, string paramName, string msg)
    {
        if (!IsValidIndexInclusive(index, max))
            throw BugExceptRange(paramName, msg);
    }

    #endregion BugCheck variations

    #region Exceptions

    public static Exception Except()
    {
        return Process(new InvalidOperationException());
    }

    public static Exception Except(string msg)
    {
        return Process(new InvalidOperationException(msg));
    }

    public static Exception Except<T>(string msg, T arg)
    {
        return Process(new InvalidOperationException(FormatMessage(msg, arg)));
    }

    public static Exception Except(string msg, params object?[] args)
    {
        return Process(new InvalidOperationException(FormatMessage(msg, args)));
    }

    public static Exception ExceptEmpty(string paramName)
    {
        return Process(new ArgumentException(paramName));
    }

    public static Exception ExceptEmpty(string paramName, string msg)
    {
        return Process(new ArgumentException(msg, paramName));
    }

    public static Exception ExceptParam(string paramName)
    {
        return Process(new ArgumentException(paramName));
    }

    public static Exception ExceptParam(string paramName, string msg)
    {
        return Process(new ArgumentException(msg, paramName));
    }

    public static Exception ExceptParam<T>(string paramName, string msg, T arg)
    {
        return Process(new ArgumentException(FormatMessage(msg, arg), paramName));
    }

    public static Exception ExceptRange(string paramName)
    {
        return Process(new ArgumentOutOfRangeException(paramName));
    }

    public static Exception ExceptRange(string paramName, string msg)
    {
        return Process(new ArgumentOutOfRangeException(paramName, msg));
    }

    public static Exception ExceptValue(string paramName)
    {
        return Process(new ArgumentNullException(paramName));
    }

    public static Exception ExceptValue(string paramName, string msg)
    {
        return Process(new ArgumentNullException(paramName, msg));
    }

    private static Exception Process(Exception ex)
    {
        // REVIEW: Implement logging hooks.
        // This is also a convenient place to set a break point.
        return ex;
    }

    #endregion Exceptions

    #region BugExceptions

    public static Exception BugExcept()
    {
        return BugProcess(new InvalidOperationException());
    }

    public static Exception BugExcept(string msg)
    {
        return BugProcess(new InvalidOperationException(msg));
    }

    public static Exception BugExcept<T>(string msg, T arg)
    {
        return BugProcess(new InvalidOperationException(FormatMessage(msg, arg)));
    }

    public static Exception BugExcept(string msg, params object?[] args)
    {
        return BugProcess(new InvalidOperationException(FormatMessage(msg, args)));
    }

    public static Exception BugExceptDisposed(string? name, string? msg = null)
    {
        return BugProcess(new ObjectDisposedException(name, msg));
    }

    public static Exception BugExceptEmpty(string paramName)
    {
        return BugProcess(new ArgumentException(paramName));
    }

    public static Exception BugExceptEmpty(string paramName, string msg)
    {
        return BugProcess(new ArgumentException(msg, paramName));
    }

    public static Exception BugExceptParam(string paramName)
    {
        return BugProcess(new ArgumentException(paramName));
    }

    public static Exception BugExceptParam(string paramName, string msg)
    {
        return BugProcess(new ArgumentException(msg, paramName));
    }

    public static Exception BugExceptParam<T>(string paramName, string msg, T arg)
    {
        return BugProcess(new ArgumentException(FormatMessage(msg, arg), paramName));
    }

    public static Exception BugExceptRange(string paramName)
    {
        return BugProcess(new ArgumentOutOfRangeException(paramName));
    }

    public static Exception BugExceptRange(string paramName, string msg)
    {
        return BugProcess(new ArgumentOutOfRangeException(paramName, msg));
    }

    public static Exception BugExceptValue(string paramName)
    {
        return BugProcess(new ArgumentNullException(paramName));
    }

    public static Exception BugExceptValue(string paramName, string msg)
    {
        return BugProcess(new ArgumentNullException(paramName, msg));
    }

    private static Exception BugProcess(Exception ex)
    {
        // REVIEW: Implement logging hooks.
        // This is also a convenient place to set a break point.
#if DEBUG
        DbgFailCore(string.Format("Bug check failed: '{0}'", ex.Message), inBugCheck: true);
#endif
        ex.Data["IsBug"] = true;
        return ex;
    }

    #endregion BugExceptions

    #region Helpers

    /// <summary>
    /// Tests whether <paramref name="index"/> is non-negative and less than <paramref name="lim"/>.
    /// </summary>
    internal static bool IsValidIndex(int index, int lim)
    {
        return 0 <= index && index < lim;
    }

    /// <summary>
    /// Tests whether <paramref name="index"/> is non-negative and less than <paramref name="lim"/>.
    /// </summary>
    internal static bool IsValidIndex(long index, long lim)
    {
        return 0 <= index && index < lim;
    }

    /// <summary>
    /// Tests whether <paramref name="index"/> is non-negative and less than or equal to <paramref name="max"/>.
    /// </summary>
    internal static bool IsValidIndexInclusive(int index, int max)
    {
        return 0 <= index && index <= max;
    }

    /// <summary>
    /// Tests whether <paramref name="index"/> is non-negative and less than or equal to <paramref name="max"/>.
    /// </summary>
    internal static bool IsValidIndexInclusive(long index, long max)
    {
        return 0 <= index && index <= max;
    }

    /// <summary>
    /// Tests whether <paramref name="index"/> falls within the range of
    /// [<paramref name="min"/>, <paramref name="lim"/>).
    /// </summary>
    internal static bool IsInRange(int index, int min, int lim)
    {
        return min <= index && index < lim;
    }

    /// <summary>
    /// Tests whether <paramref name="index"/> falls within the range of
    /// [<paramref name="min"/>, <paramref name="lim"/>).
    /// </summary>
    internal static bool IsInRange(long index, long min, long lim)
    {
        return min <= index && index < lim;
    }

    private static string FormatMessage(string msg, params object?[] args)
    {
        AssertValue(msg);
        AssertValue(args);
        return string.Format(CultureInfo.CurrentCulture, msg, args);
    }

    /// <summary>
    /// Return zero if list is null, otherwise, return list.Count.
    /// </summary>
    private static int Size<T>(IList<T>? list)
    {
        return list == null ? 0 : list.Count;
    }

    #endregion Helpers

#if DEBUG
    // If we're running in Unit Test environment, throw an exception that can be caught by the test harness.
    // The AssertFailedException constructor.
    private static ConstructorInfo? _assertFailExCtor;
    // Whether we've already looked for the AssertFailedException constructor.
    private static bool _lookedForAssertFailExCtor;

    private static void DbgFailCore(string msg, bool inBugCheck = false)
    {
        // In VS, tests are often aborted when an assert fires. To work around this,
        // we try to explicitly throw an AssertFailedException.

        // Look for the exception ctor if we haven't already.
        if (!_lookedForAssertFailExCtor)
        {
            // Look for the needed test assembly. If we're running in tests, this assembly should
            // already be loaded.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies
                .Where(a => a.FullName?.StartsWith("Microsoft.VisualStudio.TestPlatform.TestFramework,") ?? false)
                .FirstOrDefault();

            if (assembly != null)
            {
                // Look for the exception type.
                Type? type = assembly
                    .ExportedTypes
                    .FirstOrDefault(t =>
                        t.Name == "AssertFailedException" &&
                        t.Namespace == "Microsoft.VisualStudio.TestTools.UnitTesting");

                if (type != null)
                {
                    // Look for the ctor that takes a string.
                    _assertFailExCtor = type.GetConstructor(new Type[] { typeof(string) });
                }
            }

            _lookedForAssertFailExCtor = true;
        }

        if (_assertFailExCtor != null)
        {
            if (inBugCheck)
            {
                // When we're in a BugCheck, or BugExcept, we will throw, so no need to throw here.
                return;
            }
            Exception ex = (Exception)_assertFailExCtor.Invoke(new object?[] { msg });
            throw ex;
        }

        // REVIEW: In some environments, asserts are silent. We need a good solution for this.
        Debug.Assert(false, msg);
        Debugger.Break();
    }

    private static void DbgFail()
    {
        DbgFailCore("Assertion Failed");
    }

    private static void DbgFail(string msg)
    {
        DbgFailCore(msg);
    }

    private static void DbgFailValue()
    {
        DbgFailCore("Non-null assertion failure");
    }

    private static void DbgFailEmpty()
    {
        DbgFailCore("Non-empty assertion failure");
    }
#endif
}
