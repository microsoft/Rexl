// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl.Harness;

/// <summary>
/// Contains configuration settings for <see cref="Harness"/>.
/// </summary>
public interface IHarnessConfig
{
    /// <summary>
    /// Whether script execution should continue when an error is encountered.
    /// </summary>
    bool ShouldContinue { get; }

    /// <summary>
    /// Whether output should be verbose.
    /// </summary>
    bool IsVerbose { get; }

    /// <summary>
    /// Whether code optimizations should be performed.
    /// </summary>
    bool Optimize { get; }

    /// <summary>
    /// Whether to display IL from code gen.
    /// </summary>
    bool ShowIL { get; }

    /// <summary>
    /// Whether to display execution time.
    /// </summary>
    bool ShowTime { get; }

    /// <summary>
    /// Whether to display a typed parse tree.
    /// </summary>
    bool ShowTypedParseTree { get; }

    /// <summary>
    /// Whether to display a bound tree (non-verbose).
    /// </summary>
    bool ShowBoundTree { get; }

    /// <summary>
    /// Whether to display a verbose bound tree.
    /// </summary>
    bool ShowVerboseBoundTree { get; }
}

/// <summary>
/// A convenient implementation of <see cref="IHarnessConfig"/>.
/// </summary>
public class HarnessConfig : IHarnessConfig
{
    public bool ShouldContinue { get; private set; }
    public bool IsVerbose { get; private set; }
    public bool Optimize { get; private set; }
    public bool ShowIL { get; private set; }
    public bool ShowTime { get; private set; }
    public bool ShowTypedParseTree { get; private set; }
    public bool ShowBoundTree { get; private set; }
    public bool ShowVerboseBoundTree { get; private set; }

    // REVIEW: Should the default for verbose be false?
    public HarnessConfig(bool recover = false, bool verbose = true, bool optimize = true,
        bool showIL = false, bool showTime = false,
        bool showParse = false, bool showBnd = false, bool verboseBnd = false)
    {
        ShouldContinue = recover;
        IsVerbose = verbose;
        Optimize = optimize;
        ShowIL = showIL;
        ShowTime = showTime;
        ShowTypedParseTree = showParse;
        ShowBoundTree = showBnd;
        ShowVerboseBoundTree = verboseBnd;
    }

    public virtual bool SetShouldContinue(bool value)
    {
        bool prev = ShouldContinue;
        ShouldContinue = value;
        return prev;
    }

    public virtual bool SetVerbose(bool value)
    {
        bool prev = IsVerbose;
        IsVerbose = value;
        return prev;
    }

    public virtual bool SetOptimize(bool value)
    {
        bool prev = Optimize;
        Optimize = value;
        return prev;
    }

    public virtual bool SetShowIL(bool value)
    {
        bool prev = ShowIL;
        ShowIL = value;
        return prev;
    }

    public virtual bool SetShowTime(bool value)
    {
        bool prev = ShowTime;
        ShowTime = value;
        return prev;
    }

    public virtual bool SetShowTypedParseTree(bool value)
    {
        bool prev = ShowTypedParseTree;
        ShowTypedParseTree = value;
        return prev;
    }

    public virtual bool SetShowBoundTree(bool value)
    {
        bool prev = ShowBoundTree;
        ShowBoundTree = value;
        return prev;
    }

    public virtual bool SetShowVerboseBoundTree(bool value)
    {
        bool prev = ShowVerboseBoundTree;
        ShowVerboseBoundTree = value;
        return prev;
    }
}
