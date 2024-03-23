// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

/// <summary>
/// An operation can implement this interface to provide its own code generator.
/// </summary>
public interface IHaveGenerator
{
    public RexlOperationGenerator GetGenerator();
}

/// <summary>
/// A registry of operation code generators.
/// </summary>
public abstract class GeneratorRegistry : RegistryBase<GeneratorRegistry, Type, RexlOperationGenerator>
{
    // REVIEW: Consider using lazy instantiation of the generators.
    protected GeneratorRegistry()
        : base()
    {
    }

    /// <summary>
    /// Ctor when there is at most one parent registry.
    /// </summary>
    protected GeneratorRegistry(GeneratorRegistry parent)
        : base(parent)
    {
    }

    /// <summary>
    /// Ctor for any number of parent registries.
    /// </summary>
    protected GeneratorRegistry(params GeneratorRegistry[] parents)
        : base(parents)
    {
    }

    /// <summary>
    /// Ctor for any number of parent registries.
    /// </summary>
    protected GeneratorRegistry(IEnumerable<GeneratorRegistry> parents)
        : base(parents)
    {
    }

    protected void Add(Type type, RexlOperationGenerator gen)
    {
        Validation.BugCheck(type.IsSealed, nameof(type));
        Validation.BugCheck(typeof(RexlOper).IsAssignableFrom(type), nameof(type));
        // REVIEW: Should we allow null?
        Validation.BugCheckValueOrNull(gen);

        AddItem(type, gen);
    }

    protected void Add<TOper>(RexlOperationGenerator<TOper> gen)
        where TOper : RexlOper
    {
        Validation.BugCheck(typeof(TOper).IsSealed, nameof(TOper));
        // REVIEW: Should we allow null?
        Validation.BugCheckValueOrNull(gen);

        AddItem(typeof(TOper), gen);
    }

    protected void Add<TParent, TChild>(RexlOperationGenerator<TParent, TChild> gen)
        where TParent : RexlOper
        where TChild : RexlOper
    {
        Validation.BugCheck(typeof(TParent).IsSealed, nameof(TParent));
        // REVIEW: Should we allow null to "hide" previous?
        Validation.BugCheckValueOrNull(gen);

        AddItem(typeof(TParent), gen);
    }

    /// <summary>
    /// Look for a code generator that handles the given operation.
    /// </summary>
    public bool TryGet(RexlOper oper, out RexlOperationGenerator gen)
    {
        Validation.AssertValue(oper);

        // REVIEW: Should this cache on an instance basis?

        // Give the operation first opportunity.
        if (oper is IHaveGenerator have &&
            (gen = have.GetGenerator()) is not null &&
            gen.Handles(oper))
        {
            return true;
        }

        // Look up the operation type.
        if (TryGetItem(oper.GetType(), out gen) &&
            gen.VerifyValue().Handles(oper))
        {
            Validation.Assert(gen is not null);
            return true;
        }

        // Look for the parent type.
        var par = oper.Parent;
        if (par is null)
            return false;
        if (par.IsFunc != oper.IsFunc)
            return false;

        // Give the parent instance first opportunity.
        if (par is IHaveGenerator parHave &&
            (gen = parHave.GetGenerator()) is not null &&
            gen.Handles(oper))
        {
            return true;
        }

        if (TryGetItem(par.GetType(), out gen) &&
            gen.VerifyValue().Handles(oper))
        {
            return true;
        }

        gen = null;
        return false;
    }
}

/// <summary>
/// An empty generator registry. Note that operation instances can still provide their own
/// generator.
/// </summary>
public sealed class EmptyGeneratorRegistry : GeneratorRegistry
{
    public static readonly EmptyGeneratorRegistry Instance = new EmptyGeneratorRegistry();

    private EmptyGeneratorRegistry()
        : base()
    {
    }
}

/// <summary>
/// A generator registry that simply aggregates its parents (latest wins).
/// </summary>
public sealed class AggregateGeneratorRegistry : GeneratorRegistry
{
    public AggregateGeneratorRegistry(params GeneratorRegistry[] parents)
        : base(parents)
    {
    }

    public AggregateGeneratorRegistry(IEnumerable<GeneratorRegistry> parents)
        : base(parents)
    {
    }
}
