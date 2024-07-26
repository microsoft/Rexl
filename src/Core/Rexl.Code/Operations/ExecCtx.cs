// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Code;

using BndTuple = Immutable.Array<BoundNode>;

/// <summary>
/// The base execution context class.
/// </summary>
public abstract class ExecCtx
{
    protected Exception _cancel;

    protected ExecCtx()
    {
    }

    /// <summary>
    /// Creates a bare execution context that ignores logging messages and only checks for cancellation on pings.
    /// </summary>
    public static ExecCtx CreateBare()
    {
        return new BareImpl();
    }

    private sealed class BareImpl : ExecCtx
    {
        public BareImpl()
            : base()
        {
        }

        public override void Log(int id, string msg)
        {
        }

        public override void Log(int id, string fmt, params object[] args)
        {
        }
    }

    /// <summary>
    /// This can be called by outside code to cancel execution.
    /// </summary>
    public void Cancel(Exception ex)
    {
        _cancel = ex ?? new OperationCanceledException();
    }

    /// <summary>
    /// Get the current date, time and time zone offset.
    /// </summary>
    public virtual DateTimeOffset GetDateTimeOffset(int id)
    {
        return DateTimeOffset.Now;
    }

    /// <summary>
    /// Make a new <see cref="Guid"/>. This is virtual so sub-classes can log
    /// and/or generate a known guid, eg, for tests.
    /// </summary>
    public virtual Guid MakeGuid(int id)
    {
        return Guid.NewGuid();
    }

    /// <summary>
    /// Get a single random value between 0 and 1.
    /// </summary>
    public virtual double GetRandomValue(int id)
    {
        return Random.Shared.NextDouble();
    }

    /// <summary>
    /// Get an instance of <see cref="Random"/>.
    /// </summary>
    public virtual Random GetRandom(int id)
    {
        return new Random();
    }

    /// <summary>
    /// Get a single random value between 0 and 1 using the given seed.
    /// </summary>
    public virtual double GetRandomValue(int id, long seed)
    {
        // REVIEW: Is there a cheaper way to get a single random value from a seed?
        // Random object initialization is fairly expensive.
        // LUT/LRU cache for seed -> value? In-house PRNG implementation?
        return new Random(HashSeed(id, seed)).NextDouble();
    }

    /// <summary>
    /// Get an instance of <see cref="Random"/> using the given seed.
    /// </summary>
    public virtual Random GetRandom(int id, long seed)
    {
        return new Random(HashSeed(id, seed));
    }

    /// <summary>
    /// REXL integer literals are typed as i8s, but <see cref="Random"/> is seeded with an int.
    /// This function hashes the user-given seed of type long into a compatible seed of type int.
    /// It also logs the input seed.
    /// </summary>
    protected virtual int HashSeed(int id, long seed)
    {
        // See if users prefer certain seeds, which could be optimized with a LUT/cache...
        Log(id, "RNG seed: {0}", seed);

        // Use xor followed by Fibonacci hashing so adjacent seeds are more likely to be
        // mapped to very different hashes. Note: just xor would map -1 and 0 onto the same hash.
        // Including an xor op before Fibonacci hashing produces better results in the upper hash bits.

        // This technique was taken from the following:
        // Skarupke, M. (2018, June 16). Fibonacci hashing: The optimization that the world forgot (or: A better alternative to integer modulo).
        // https://probablydance.com/2018/06/16/fibonacci-hashing-the-optimization-that-the-world-forgot-or-a-better-alternative-to-integer-modulo/

        // Fibonacci hashing constant for 64 bits: 2^64 / phi, rounded to the nearest odd integer.
        const ulong C = 11400714819323198485UL;

        ulong xored = (ulong)seed ^ (ulong)seed >> 32;
        return (int)((xored * C) >> 32);
    }

    /// <summary>
    /// Ping an iterative operation not associated with a bound node.
    /// </summary>
    public void Ping()
    {
        // REVIEW: Pending ExecCtx API refinement, we could separate
        // the contracts for these no-ID overloads so we don't need to check
        // for -1 on every call.
        Ping(-1);
    }

    /// <summary>
    /// Ping an iterative operation, eg <see cref="CountFunc"/>.
    /// The caller should pass its <paramref name="id"/> as a valid ID from
    /// its call to <see cref="ICodeGen.EnsureIdRange"/>, or -1 to mean no ID.
    /// </summary>
    public virtual void Ping(int id)
    {
        // REVIEW: Profiling shows the cost of this virtual call is fairly
        // high, even when the call is a no-op. Perhaps there's a better way to ping
        // that avoids this cost. For more details, see REXL design meeting notes in
        // OneNote for 2022-06-29.
        if (_cancel != null)
            throw new OperationCanceledException("Cancelled", _cancel);
    }

    /// <summary>
    /// Log a message.
    /// </summary>
    public void Log(string msg) => Log(-1, msg);

    /// <summary>
    /// Log a message.
    /// The caller should pass its <paramref name="id"/> as a valid ID from
    /// its call to <see cref="ICodeGen.EnsureIdRange"/>, or -1 to mean no ID.
    /// </summary>
    public abstract void Log(int id, string msg);

    /// <summary>
    /// Log a formatted message.
    /// </summary>
    public void Log(string fmt, params object[] args) => Log(-1, fmt, args);

    /// <summary>
    /// Log a formatted message.
    /// The caller should pass its <paramref name="id"/> as a valid ID from
    /// its call to <see cref="ICodeGen.EnsureIdRange"/>, or -1 to mean no ID.
    /// </summary>
    public abstract void Log(int id, string fmt, params object[] args);

    /// <summary>
    /// Produces an <see cref="EvalSink"/> if one is available to the context.
    /// </summary>
    public virtual bool TryGetSink(out EvalSink sink)
    {
        sink = null;
        return false;
    }

    /// <summary>
    /// Produces a <see cref="CodeGeneratorBase"/> if one is available to the context.
    /// </summary>
    public virtual bool TryGetCodeGen(out CodeGeneratorBase codeGen)
    {
        codeGen = null;
        return false;
    }

    /// <summary>
    /// Load a stream from the given link. Return null or throw on failure. The caller should
    /// catch and handle exceptions.
    /// </summary>
    public virtual Stream LoadStream(Link link, int id)
    {
        if (link is null)
            return null;

        Log(id, "LoadStream not supported");
        return null;
    }
}

/// <summary>
/// An execution context that tracks a total ping count.
/// </summary>
public abstract class TotalPingsExecCtx : ExecCtx
{
    protected long _pingCount;

    /// <summary>
    /// Returns the number of times this context has been pinged.
    /// </summary>
    public long PingCount => Interlocked.Read(ref _pingCount);

    protected TotalPingsExecCtx()
        : base()
    {
    }

    /// <summary>
    /// Creates a bare execution context that ignores logging messages and tracks a total ping count.
    /// </summary>
    public static new TotalPingsExecCtx CreateBare()
    {
        return new BareImpl();
    }

    private sealed class BareImpl : TotalPingsExecCtx
    {
        public BareImpl()
            : base()
        {
        }

        public override void Log(int id, string msg)
        {
        }

        public override void Log(int id, string fmt, params object[] args)
        {
        }
    }

    public override void Ping(int id)
    {
        Interlocked.Increment(ref _pingCount);
        base.Ping(id);
    }
}

/// <summary>
/// An execution context that tracks separate ping counts for a contiguous range of IDs starting at 0
/// up to some count. In the typical case, this count should be given by <see cref="IdBndMap.Count"/>
/// to match the ID count of the compiled bound tree for which this execution context will be used.
/// Pings with an ID that fall outside the valid range are all considered to have no ID
/// and are all tracked together with a single count, <see cref="PingCountNoId"/>.
/// </summary>
public abstract class PingsPerIdExecCtx : ExecCtx
{
    protected readonly long[] _idToPings;
    protected long _pingCountNoId;

    /// <summary>
    /// Returns the total number of IDs for which this context tracks individual ping counts.
    /// </summary>
    public int IdCount => _idToPings.Length;

    /// <summary>
    /// Returns the number of times this context has been pinged without an ID.
    /// </summary>
    public long PingCountNoId => Interlocked.Read(ref _pingCountNoId);

    protected PingsPerIdExecCtx(int idCount)
        : base()
    {
        Validation.Assert(idCount >= 0);
        _idToPings = new long[idCount];
    }

    /// <summary>
    /// Returns the total number of pings seen by this context.
    /// </summary>
    public long GetTotalPingCount(bool includeNoId = true)
    {
        long pingSum = includeNoId ? PingCountNoId : 0;
        for (int id = 0; id < IdCount; id++)
            pingSum += Interlocked.Read(ref _idToPings[id]);
        return pingSum;
    }

    /// <summary>
    /// If <paramref name="id"/> falls in the range [0, <see cref="IdCount"/>),
    /// returns the ping count for the given ID. Otherwise returns <see cref="PingCountNoId"/>.
    /// </summary>
    public long GetIdPingCount(int id)
    {
        if ((uint)id >= (uint)IdCount)
            return PingCountNoId;
        return Interlocked.Read(ref _idToPings[id]);
    }

    /// <summary>
    /// Returns an array indexed by ID of ping counts. If <paramref name="includeNoId"/> is set, a final
    /// entry will be appended to correspond to the count for no ID; this entry will have an index of
    /// <see cref="IdCount"/>.
    /// </summary>
    public Immutable.Array<long> GetPingInfo(bool includeNoId = true)
    {
        int count = IdCount + includeNoId.ToNum();
        var bldrPings = Immutable.Array<long>.CreateBuilder(count, init: true);
        for (int i = 0; i < IdCount; i++)
            bldrPings[i] = Interlocked.Read(ref _idToPings[i]);
        if (includeNoId)
            bldrPings[IdCount] = PingCountNoId;
        return bldrPings.ToImmutable();
    }

    public override void Ping(int id)
    {
        if ((uint)id >= (uint)IdCount)
            Interlocked.Increment(ref _pingCountNoId);
        else
            Interlocked.Increment(ref _idToPings[id]);
        base.Ping(id);
    }

    /// <summary>
    /// Hides the inherited <see cref="ExecCtx.CreateBare()"/>.
    /// Expected to cause a compile time error.
    /// </summary>
    public static new void CreateBare()
    {
    }

    /// <summary>
    /// Creates a bare execution context that ignores logging messages and tracks a total ping count.
    /// </summary>
    public static PingsPerIdExecCtx CreateBare(int idCount)
    {
        return new BareImpl(idCount);
    }

    private sealed class BareImpl : PingsPerIdExecCtx
    {
        public BareImpl(int idCount)
            : base(idCount)
        {
        }

        public override void Log(int id, string msg)
        {
        }

        public override void Log(int id, string fmt, params object[] args)
        {
        }
    }
}

/// <summary>
/// Map between IDs and bound nodes. Each bound node is associated with a
/// contiguous range of IDs. Each ID is used to identify a usage by its
/// corresponding bound node in the compiled bound tree.
/// </summary>
public sealed class IdBndMap
{
    /// <summary>
    /// Mapping from IDs to bound nodes.
    /// </summary>
    public BndTuple IdToBnd { get; }

    /// <summary>
    /// Mapping from bound nodes to IDs.
    /// </summary>
    public ReadOnly.Dictionary<BoundNode, SlotRange> BndToIdRng { get; }

    /// <summary>
    /// Number of entries.
    /// </summary>
    public int Count => IdToBnd.Length;

    internal IdBndMap(BndTuple idToBnd, ReadOnly.Dictionary<BoundNode, SlotRange> bndToIds)
    {
        Validation.Assert(!bndToIds.IsDefault, nameof(bndToIds));
#if DEBUG
        int count = 0;
        foreach (var (bnd, rng) in bndToIds)
        {
            Validation.Assert(rng.Count >= 1);
            count += rng.Count;
            for (int id = rng.Min; id < rng.Lim; id++)
                Validation.Assert(idToBnd[id] == bnd);
        }
        Validation.Assert(idToBnd.Length == count);
#endif
        IdToBnd = idToBnd.Length > 0 ? idToBnd : BndTuple.Empty;
        BndToIdRng = bndToIds;
    }
}

/// <summary>
/// For an exec context that wraps an <see cref="IdBndMap"/> and collects pings for each id.
/// </summary>
public abstract class IdBndMapExecCtx : PingsPerIdExecCtx
{
    public IdBndMap Map { get; }

    protected IdBndMapExecCtx(IdBndMap map)
        : base(map.VerifyValue().Count)
    {
        Map = map;
    }

    /// <summary>
    /// Hides the inherited <see cref="PingsPerIdExecCtx.CreateBare(int)"/>.
    /// Expected to cause a compile time error.
    /// </summary>
    public static new void CreateBare(int idCount)
    {
    }

    /// <summary>
    /// Creates a bare execution context that ignores logging messages and tracks a total ping count.
    /// </summary>
    public static IdBndMapExecCtx CreateBare(IdBndMap map)
    {
        return new BareImpl(map);
    }

    private sealed class BareImpl : IdBndMapExecCtx
    {
        public BareImpl(IdBndMap map)
            : base(map)
        {
        }

        public override void Log(int id, string msg)
        {
        }

        public override void Log(int id, string fmt, params object[] args)
        {
        }
    }
}
