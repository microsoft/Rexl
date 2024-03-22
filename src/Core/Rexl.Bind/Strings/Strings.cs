// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

// REVIEW: This is a temporary place holder, until we get UI strings in resources.
public partial struct StringId
{
    // REVIEW: This is a temporary place for the Culture to use. Eventually, it may
    // be somewhere else.
    public static CultureInfo Culture { get { return CultureInfo.CurrentCulture; } }

    public bool IsValid
    {
        get { return !string.IsNullOrEmpty(_msg); }
    }

    public readonly string Tag;

    // REVIEW: The GetString() call will require access to a ResourceManager
    // instance to get culture-appropriate strings.
    // Probably need to implement a constructor that takes a
    // Func<string, string, string> that takes the tag and culture as parameter
    // to return string resource from the appropriate culture resource file.

    /// <summary>
    /// Creates a StringId without a Tag.
    /// This is meant to be used when only a constant string should be returned.
    /// This string should not be stored in resource files or read from resource file.
    /// </summary>
    public static StringId Create(string msg)
    {
        Validation.AssertNonEmpty(msg);
        return new StringId(msg);
    }

    private StringId(string msg)
    {
        Validation.AssertNonEmpty(msg);
        Tag = null;
        _msg = msg;
    }

#if !STRINGS_IN_RESOURCES
    private readonly string _msg;

    public StringId(string tag, string msg)
    {
        Validation.AssertNonEmpty(tag);
        Validation.AssertNonEmpty(msg);
        Tag = tag;
        _msg = msg;
    }

    public string GetString()
    {
        Validation.AssertNonEmpty(_msg);
        return _msg;
    }
#elif COMPILE_RESOURCE_STRINGS
    private readonly string _msg;

    // REVIEW: Implement the compile-time tool to put the strings in resources.
    // This is just a sketch....
    public StringId(string tag, string msg)
    {
        Validation.AssertNonEmpty(tag);
        Validation.AssertNonEmpty(msg);
        Tag = tag;
        _msg = msg;

        // When compiling the resources, the constructor does all the work.
        EmitStringResource(Tag, _msg);
    }

    public string GetString()
    {
        Validation.AssertNonEmpty(_msg);
        return _msg;
    }
#else // STRINGS_IN_RESOURCES && !COMPILE_RESOURCE_STRINGS
    // REVIEW: In this case, we really don't want the msg strings to be in the assembly
    // as constants. Is there a good way to do this in C# without invoking the C++ preprocessor?
    public StringId(string tag, string msg)
    {
        Contracts.AssertNonEmpty(tag);
        Contracts.AssertNonEmpty(msg);
        Tag = tag;
    }

    public string GetString()
    {
        Contracts.AssertNonEmpty(Tag);
        return global::Microsoft.Tangram.Resources.ResourceManager.GetString(Tag);
    }
#endif // STRINGS_IN_RESOURCES && !COMPILE_RESOURCE_STRINGS
}

public partial struct StringId
{
}

// REVIEW: This should be renamed to indicate these are for intellisense/signatures.
// REVIEW: Remove unused strings, particularly ones from Tangram.

// REVIEW: We should ensure there is a consistent naming convention for the IDs
// and style for the messages. Perhaps a style guide is worthwhile?
internal static partial class RexlStrings
{
}

public static partial class ErrorStrings
{
}
