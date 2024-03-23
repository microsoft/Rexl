// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl;

/// <summary>
/// Interface for getting signatures.
/// </summary>
public interface IHaveSignatures
{
    Immutable.Array<Signature> GetSignatures();
}

/// <summary>
/// Provides metadata of a function and each of its arguments.
/// </summary>
public sealed class Signature
{
    /// <summary>
    /// Defines a group of parameters in a signature. A group consists of a contiguous
    /// range of parameter indices. Different types of groups have different characteristics.
    /// For example, a <see cref="RepetitiveGroup"/> allows the parameters to be repeated.
    /// </summary>
    public abstract class Group
    {
        /// <summary>
        /// The minimum <see cref="Argument"/> index for this group.
        /// </summary>
        public int Min { get; }

        /// <summary>
        /// The limit <see cref="Argument"/> index for this group.
        /// </summary>
        public int Lim { get; }

        /// <summary>
        /// The number of <see cref="Argument"/> items in this group.
        /// </summary>
        public int Size => Lim - Min;

        protected Group(int min, int lim)
        {
            Validation.BugCheckParam(lim > 0, nameof(lim));
            Validation.BugCheckIndex(min, lim, nameof(min));
            Min = min;
            Lim = lim;
        }

        /// <summary>
        /// Return a group with the given <paramref name="min"/>, effectively "shifting" the group
        /// to reference a different range of arguments. This is used by <see cref="Expand"/>.
        /// Depending on the group type, this may do additional transformation. For example, a
        /// <see cref="RepetitiveGroup"/> also sets the <see cref="RepetitiveGroup.MinCount"/>
        /// to zero.
        /// </summary>
        internal abstract Group MoveForExpand(int min);
    }

    /// <summary>
    /// A group of optional parameters. When an argument is "omitted", all succeeding
    /// parameters in the group are also considered omitted (assuming named arguments
    /// aren't being used).
    /// </summary>
    public sealed class OptionalGroup : Group
    {
        private OptionalGroup(int min, int lim)
            : base(min, lim)
        {
        }

        public static OptionalGroup Create(int min, int lim)
        {
            return new OptionalGroup(min, lim);
        }

        internal override Group MoveForExpand(int min)
        {
            if (min == Min)
                return this;
            return new OptionalGroup(min, min + Lim - Min);
        }
    }

    /// <summary>
    /// A group of repetitive parameters.
    /// </summary>
    public sealed class RepetitiveGroup : Group
    {
        /// <summary>
        /// The minimum number of times the parameters must be specified, typically
        /// either zero or one. There is no maximum.
        /// </summary>
        public int MinCount { get; }

        private RepetitiveGroup(int minCount, int min, int lim)
            : base(min, lim)
        {
            Validation.Assert(minCount >= 0);
            MinCount = minCount;
        }

        public static RepetitiveGroup Create(int minCount, int index)
        {
            Validation.BugCheck(minCount >= 0);
            return new RepetitiveGroup(minCount, index, index + 1);
        }

        public static RepetitiveGroup Create(int minCount, int min, int lim)
        {
            Validation.BugCheck(minCount >= 0);
            return new RepetitiveGroup(minCount, min, lim);
        }

        /// <summary>
        /// This moves the group and clears the MinCount.
        /// </summary>
        internal override Group MoveForExpand(int min)
        {
            if (min == Min && MinCount == 0)
                return this;

            // Also clear the MinCount.
            return new RepetitiveGroup(0, min, min + Lim - Min);
        }
    }

    /// <summary>
    /// Description of the function.
    /// </summary>
    public StringId Description { get; }

    /// <summary>
    /// Whether this signature is for a volatile invocation.
    /// </summary>
    public bool IsVolatile { get; }

    /// <summary>
    /// The return type, if known, otherwise the default value.
    /// </summary>
    public DType ReturnType { get; }

    /// <summary>
    /// Descriptions for each argument. For variadic functions,
    /// the last description should be assumed to be description
    /// when a specific description is not available.
    /// </summary>
    public Immutable.Array<Argument> Arguments { get; }

    /// <summary>
    /// Descriptions for groups of arguments.
    /// </summary>
    public Immutable.Array<Group> Groups { get; }

    /// <summary>
    /// An example formula using this function, if provided.
    /// </summary>
    public string? Example { get; }

    /// <summary>
    /// The number of additional args in the expanded form. If this is zero,
    /// <see cref="_sigExpanded"/> is the same as this.
    /// </summary>
    private readonly int _extra;

    /// <summary>
    /// The expanded signature. Created lazily.
    /// </summary>
    private volatile Signature _sigExpanded;

    public Signature(StringId description)
    {
        Validation.BugCheck(description.IsValid);

        Description = description;
        IsVolatile = false;
        Arguments = Immutable.Array<Argument>.Empty;
        _sigExpanded = this;
    }

    public Signature(StringId description, Argument arg)
    {
        Validation.BugCheck(description.IsValid);
        Validation.BugCheckValue(arg, nameof(arg));

        Description = description;
        IsVolatile = false;
        Arguments = Immutable.Array.Create(arg);
        _sigExpanded = this;
    }

    public Signature(StringId description, Argument arg, Group group)
    {
        Validation.BugCheck(description.IsValid);
        Validation.BugCheckValue(arg, nameof(arg));
        Validation.BugCheckValue(group, nameof(group));
        Validation.BugCheckParam(group.Lim == 1, nameof(group));

        Description = description;
        IsVolatile = false;
        Arguments = Immutable.Array.Create(arg);
        Groups = Immutable.Array.Create(group);

        var extra = ExtraFromGroup(group);
        if (extra == 0)
            _sigExpanded = this;
        else
        {
            Validation.Assert(extra > 0);
            Validation.BugCheck(extra + 1 <= int.MaxValue, nameof(group));
            _extra = (int)extra;
        }
    }

    public Signature(StringId description, params Argument[] args)
    {
        Validation.BugCheck(description.IsValid);
        Validation.BugCheckValue(args, nameof(args));
        Validation.BugCheckAllValues(args, nameof(args));

        Description = description;
        IsVolatile = false;
        Arguments = Immutable.Array.Create(args);
        _sigExpanded = this;
    }

    public Signature(StringId description, Immutable.Array<Argument> args,
        DType typeRet = default, bool isVol = false, string? example = null)
    {
        Validation.BugCheck(description.IsValid);
        Validation.BugCheck(!args.IsDefault);
        Description = description;
        IsVolatile = isVol;
        Arguments = args;
        ReturnType = typeRet;
        Example = example;
        _sigExpanded = this;
    }

    private static long ExtraFromGroup(Group grp)
    {
        long extra = 0;
        if (grp is RepetitiveGroup rep && rep.MinCount != 0)
        {
            Validation.Assert(rep.MinCount > 0);
            Validation.Assert(rep.Lim > rep.Min);
            extra += (long)rep.MinCount * (rep.Lim - rep.Min);
        }
        return extra;
    }

    public Signature(StringId description, Immutable.Array<Argument> args, Immutable.Array<Group> groups,
        DType typeRet = default, bool isVol = false, string? example = null)
    {
        Validation.Assert(description.IsValid);
        Validation.BugCheck(!args.IsDefault);
        Validation.BugCheck(!groups.IsDefault);

        Description = description;
        IsVolatile = isVol;
        Arguments = args;
        Groups = groups;
        ReturnType = typeRet;
        Example = example;

        // Groups should be sorted and not overlap. Also, if there are no repetitive groups
        // with positive MinCount, then the expanded form is the same as this.
        long extra = 0;
        int lim = 0;
        foreach (var grp in groups)
        {
            Validation.BugCheckParam(lim <= grp.Min, nameof(groups));
            lim = grp.Lim;
            extra += ExtraFromGroup(grp);
            // Check for overflow.
            Validation.Assert(extra >= 0);
            Validation.BugCheck(extra + args.Length <= int.MaxValue, nameof(groups));
        }
        Validation.BugCheckParam(lim <= args.Length, nameof(groups));
        if (extra == 0)
            _sigExpanded = this;
        else
        {
            Validation.Assert(0 < extra && extra <= int.MaxValue - args.Length);
            _extra = (int)extra;
        }
    }

    public Signature(StringId description, Immutable.Array<Argument> args, Group group,
            DType typeRet = default, bool isVol = false, string? example = null)
        : this(description, args, Immutable.Array.Create(group), typeRet, isVol, example)
    {
    }

    /// <summary>
    /// Returns a signature where any repetitive groups that have positive MinCount are expanded.
    /// </summary>
    public Signature Expand()
    {
        if (_sigExpanded != null)
            return _sigExpanded;

        Validation.Assert(_extra > 0);
        Validation.Assert(Groups.Length > 0);
        var args = Immutable.Array<Argument>.CreateBuilder(Arguments.Length + _extra, init: true);
        var grps = Groups.ToBuilder();

        int iargSrc = 0;
        int iargDst = 0;
        for (int igrp = 0; igrp < grps.Count; igrp++)
        {
            var grp = Groups[igrp];
            Validation.AssertIndexInclusive(iargDst, iargSrc + _extra);
            Validation.Assert(iargSrc <= grp.Min);
            while (iargSrc < grp.Min)
                args[iargDst++] = Arguments[iargSrc++];
            Validation.Assert(iargSrc == grp.Min);

            uint suff = 0;
            if (grp is RepetitiveGroup rep && rep.MinCount > 0)
            {
                // Start with one, not zero.
                ++suff;
                for (int count = rep.MinCount; --count >= 0;)
                {
                    for (int i = rep.Min; i < rep.Lim; i++)
                        args[iargDst++] = Arguments[i].SetSuffix(suff);
                    ++suff;
                }
            }

            Validation.AssertIndexInclusive(iargDst, iargSrc + _extra);
            Validation.Assert(iargSrc == grp.Min);
            grps[igrp] = grp.MoveForExpand(iargDst);
            while (iargSrc < grp.Lim)
                args[iargDst++] = Arguments[iargSrc++].SetSuffix(suff);
            Validation.Assert(iargSrc == grp.Lim);
        }

        Validation.AssertIndexInclusive(iargDst, iargSrc + _extra);
        Validation.Assert(iargSrc <= Arguments.Length);
        while (iargSrc < Arguments.Length)
            args[iargDst++] = Arguments[iargSrc++];
        Validation.Assert(iargSrc == Arguments.Length);
        Validation.Assert(iargDst == args.Count);

        var sig = new Signature(Description, args.ToImmutable(), grps.ToImmutable(),
            ReturnType, IsVolatile, Example);
        Validation.Assert(sig._sigExpanded == sig);

        Interlocked.CompareExchange(ref _sigExpanded, sig, null);
        return _sigExpanded;
    }
}

/// <summary>
/// Provides metadata of an argument for a function.
/// </summary>
public sealed class Argument
{
    /// <summary>
    /// Name of the argument.
    /// </summary>
    public StringId Name { get; }

    /// <summary>
    /// An integer suffix for the name. Zero means, don't use.
    /// </summary>
    public uint Suffix { get; }

    /// <summary>
    /// The name as a string with the suffix appended (when appropriate).
    /// </summary>
    public string NameStr
    {
        get
        {
            var str = Name.GetString();
            if (Suffix > 0)
                str += Suffix.ToString();
            return str;
        }
    }

    /// <summary>
    /// Description of the argument.
    /// </summary>
    public StringId Description { get; }

    /// <summary>
    /// DType of the argument. Could be default if not properly set.
    /// </summary>
    public DType Type { get; }

    /// <summary>
    /// Enumeration of possible values of the argument.
    /// </summary>
    public Immutable.Array<string> EnumValues;

    private Argument(StringId name, uint suff, StringId description, DType type, Immutable.Array<string> enumValues)
    {
        Validation.Assert(name.IsValid);
        Validation.Assert(description.IsValid);

        Name = name;
        Suffix = suff;
        Description = description;
        Type = type;
        EnumValues = enumValues;
    }

    public static Argument Create(StringId name, StringId description)
    {
        Validation.BugCheck(name.IsValid);
        Validation.BugCheck(description.IsValid);
        return new Argument(name, 0, description, default, default);
    }

    public static Argument Create(StringId name, StringId description, DType type, Immutable.Array<string> enumValues = default)
    {
        Validation.BugCheck(name.IsValid);
        Validation.BugCheck(description.IsValid);
        return new Argument(name, 0, description, type, enumValues);
    }

    public Argument SetSuffix(uint suff)
    {
        if (suff == Suffix)
            return this;
        return new Argument(Name, suff, Description, Type, EnumValues);
    }
}
