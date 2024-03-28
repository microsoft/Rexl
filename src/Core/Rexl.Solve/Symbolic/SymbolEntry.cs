// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Symbolic;

using SvarTuple = Immutable.Array<BndFreeVarNode>;

/// <summary>
/// Represents a range of variable indices.
/// </summary>
public struct IdRng
{
    public readonly int Min;
    public readonly int Lim;

    public int Count => Lim - Min;

    public IdRng(int min, int lim)
    {
        Validation.BugCheck(0 <= min & min <= lim);
        Min = min;
        Lim = lim;
    }
}

// This partial defines the "entries" in a symbol map.
partial class SymbolMap
{
    /// <summary>
    /// Base class for symbol entries. These are "owned" by a <see cref="SymbolMap"/>. They contain mapping
    /// and reduction information for their associated symbols relative to the owning <see cref="SymbolMap"/>.
    /// For example, free variable symbols have associated <see cref="BndFreeVarNode"/> instances.
    /// The "id" of an <see cref="BndFreeVarNode"/> is an index that is unique within the <see cref="SymbolMap"/>.
    /// </summary>
    public abstract class SymbolEntry
    {
        /// <summary>
        /// The bound symbol information.
        /// </summary>
        public abstract ModSym Symbol { get; }

        /// <summary>
        /// The type of this entry. Note that it is possible for this to differ from
        /// the symbol type, in that the symbol type may be opt while this is req.
        /// </summary>
        public abstract DType Type { get; }

        private protected SymbolEntry()
        {
        }
    }

    /// <summary>
    /// For a computed/derived variable.
    /// </summary>
    public sealed class ComputedVarEntry : SymbolEntry
    {
        public override ModComputedVar Symbol { get; }
        public override DType Type => Node.Type;

        /// <summary>
        /// The value as a <see cref="BoundNode"/>.
        /// </summary>
        public BoundNode Node { get; }

        /// <summary>
        /// The associated <see cref="BndFreeVarNode"/> when needed.
        /// </summary>
        public BndFreeVarNode SvarOpt { get; private set; }

        private ComputedVarEntry(ModComputedVar sym, BoundNode node)
        {
            Validation.AssertValue(sym);
            Validation.AssertValue(node);
            Validation.Assert(node.Type == sym.Type);
            Symbol = sym;
            Node = node;
        }

        /// <summary>
        /// Should be used only by <see cref="SymbolMap"/>.
        /// </summary>
        internal static ComputedVarEntry Create(SymbolMap map, ModComputedVar sym, BoundNode node)
        {
            Validation.AssertValue(map);
            Validation.AssertValue(sym);
            Validation.AssertValue(node);
            Validation.Assert(node.Type == sym.Type);
            Validation.Assert(!map._entries.ContainsKey(sym.Name));

            var res = new ComputedVarEntry(sym, node);
            map._entries.Add(sym.Name, res);
            return res;
        }

        /// <summary>
        /// Make sure that we've created a <see cref="BndFreeVarNode"/> for this variable and return it.
        /// Should be used only by <see cref="SymbolMap"/> and <see cref="SymbolReducer"/>.
        /// </summary>
        internal BndFreeVarNode EnsureVariable(SymbolMap map)
        {
            Validation.AssertValue(map);
            Validation.Assert(map._entries.TryGetValue(Symbol.Name, out var entry) & entry == this);

            if (SvarOpt != null)
                return SvarOpt;

            int id = map._idToSvar.Count;
            var svar = BndFreeVarNode.Create(Type, id);
            map._idToSvar.Add(svar);
            return SvarOpt = svar;
        }
    }

    /// <summary>
    /// Base class for a free variable. A free variable maps to one or more <see cref="BndFreeVarNode"/>
    /// instances, which represent the "underlying" variables. For example, an "in" variable
    /// has a bit-valued <see cref="BndFreeVarNode"/> for each item in the domain sequence.
    /// </summary>
    public abstract class FreeVarEntry : SymbolEntry
    {
        /// <summary>
        /// The variable id range for this free variable.
        /// </summary>
        public abstract IdRng Rng { get; }

        /// <summary>
        /// The type of the underlying variables.
        /// </summary>
        public abstract DType TypeVar { get; }

        private protected FreeVarEntry()
        {
        }

        private protected static (IdRng rng, SvarTuple svars) BuildSvars(SymbolMap map, int count, DType type)
        {
            Validation.AssertValue(map);
            Validation.Assert(count >= 0);

            int idMin = map._idToSvar.Count;
            int idLim = idMin + count;

            // Callers should ensure that this doesn't overflow.
            Validation.Assert(idLim >= idMin);

            SvarTuple svars;
            if (count <= 0)
                svars = SvarTuple.Empty;
            else
            {
                var bldr = SvarTuple.CreateBuilder(count, init: true);
                for (int id = idMin; id < idLim; id++)
                {
                    var svar = BndFreeVarNode.Create(type, id);
                    Validation.Assert(map._idToSvar.Count == id);
                    map._idToSvar.Add(svar);
                    bldr[id - idMin] = svar;
                }
                Validation.Assert(map._idToSvar.Count == idLim);
                svars = bldr.ToImmutable();
            }

            return (new IdRng(idMin, idLim), svars);
        }
    }

    /// <summary>
    /// For a simple free variable having a single underlying variable. Note that tensor valued
    /// variables expand to multiple svars, so are not handled by this class.
    /// </summary>
    public sealed class BasicVarEntry : FreeVarEntry
    {
        public override ModSimpleVar Symbol { get; }
        public override DType Type => Svar.Type;
        public override IdRng Rng => new IdRng(Id, Id + 1);
        public override DType TypeVar => Svar.Type;

        /// <summary>
        /// The "id" of the associated <see cref="BndFreeVarNode"/>.
        /// </summary>
        public int Id => Svar.Id;

        /// <summary>
        /// The associated svar.
        /// </summary>
        public BndFreeVarNode Svar { get; }

        private BasicVarEntry(ModSimpleVar sym, BndFreeVarNode svar)
        {
            Validation.AssertValue(sym);
            Validation.AssertValue(svar);
            Validation.Assert(sym.Type == svar.Type);
            Validation.Assert(!svar.Type.IsSequence);
            Validation.Assert(!svar.Type.IsTensorXxx);
            Validation.Assert(svar.Id >= 0);
            Symbol = sym;
            Svar = svar;
        }

        /// <summary>
        /// Should be used only by <see cref="SymbolMap"/>.
        /// </summary>
        internal static BasicVarEntry Create(SymbolMap map, ModSimpleVar sym)
        {
            // REVIEW: Need to consider opt handling.
            Validation.AssertValue(map);
            Validation.AssertValue(sym);
            Validation.Assert(!map._entries.ContainsKey(sym.Name));
            Validation.Assert(!sym.Type.IsSequence);
            Validation.Assert(!sym.Type.IsTensorXxx);

            int id = map._idToSvar.Count;
            var svar = BndFreeVarNode.Create(sym.Type, id);
            map._idToSvar.Add(svar);
            var res = new BasicVarEntry(sym, svar);
            map._entries.Add(sym.Name, res);
            return res;
        }
    }

    /// <summary>
    /// For a free variable that is tensor-valued, that is, indexed.
    /// </summary>
    public sealed class TensorVarEntry : FreeVarEntry
    {
        public override ModSimpleVar Symbol { get; }
        public override DType Type { get; }
        public override IdRng Rng { get; }
        public override DType TypeVar { get; }

        /// <summary>
        /// The shape of this tensor.
        /// </summary>
        public Shape Shape { get; }

        /// <summary>
        /// The individual scalar variables, flattened.
        /// </summary>
        public SvarTuple Svars { get; }

        private TensorVarEntry(ModSimpleVar sym, Shape shape, IdRng rng, SvarTuple svars)
        {
            // REVIEW: Should we support rank zero here? Or zero dimensions? Should be no need.
            Validation.Assert(sym.Type.IsTensorXxx);
            Validation.Assert(sym.Type.TensorRank == shape.Rank);
            Validation.Assert(shape.Rank >= 1);
            Validation.Assert(svars.Length >= 0);
            Validation.Assert(svars.Length == rng.Count);
#if DEBUG
            long size = svars.Length;
            for (int i = 0; i < shape.Rank; i++)
            {
                if (shape[i] == 0)
                    Validation.Assert(size == 0);
                else
                {
                    Validation.Assert(shape[i] > 0);
                    Validation.Assert(size % shape[i] == 0);
                    size /= shape[i];
                }
            }
            Validation.Assert(size == 1 || size == 0);
#endif
            Symbol = sym;
            Type = sym.Type;
            TypeVar = sym.Type.GetTensorItemType();
            Rng = rng;
            Shape = shape;
            Svars = svars;
        }

        /// <summary>
        /// Should be used only by <see cref="SymbolMap"/>.
        /// </summary>
        internal static TensorVarEntry Create(SymbolMap map, ModSimpleVar sym, Shape shape, int size)
        {
            Validation.AssertValue(map);
            Validation.AssertValue(sym);
            Validation.Assert(!map._entries.ContainsKey(sym.Name));
            Validation.Assert(sym.Type.IsTensorXxx);

            var (rng, svars) = BuildSvars(map, size, sym.Type.GetTensorItemType());
            var res = new TensorVarEntry(sym, shape, rng, svars);
            map._entries.Add(sym.Name, res);
            return res;
        }
    }

    /// <summary>
    /// This is for free variables that are based on a "key" sequence. Each ends up with
    /// N primitive variables, where N is the length of the key sequence.
    /// There are three types:
    /// (1) "item": the "value" is a single value from the key sequence (or possibly null if opt).
    ///     The underlying variables are bit-valued.
    /// (2) "subset": the "value" is a sub-sequence of the key sequence. The underlying variables
    ///     are bit-valued.
    /// (3) "zip": the "value" is a sequence of records, with two fields, "variable" and "key".
    /// </summary>
    public abstract class KeyedVarEntry : FreeVarEntry
    {
        public sealed override IdRng Rng { get; }

        /// <summary>
        /// The system of an item in the key sequence.
        /// </summary>
        public DType TypeKey { get; }

        /// <summary>
        /// The key sequence items, in order. The number of items will be the same as the Count of the Rng.
        /// REVIEW: Is it practical to use Immutable.Array for this? We need to be able to use this
        /// as a sequence-valued arg to a rexl generated function.
        /// </summary>
        public Array Keys => KeysConst.Items;

        /// <summary>
        /// The key sequence items, wrapped as a <see cref="BndArrConstNode"/>.
        /// </summary>
        public BndArrConstNode KeysConst { get; }

        /// <summary>
        /// The individual scalar variables.
        /// </summary>
        public SvarTuple Svars { get; }

        private protected KeyedVarEntry(BndArrConstNode keys, IdRng rng, SvarTuple svars)
        {
            Validation.Assert(keys != null && keys.Length > 0);
            Validation.Assert(rng.Count == keys.Length);
            Validation.Assert(svars.Length == keys.Length);
            Validation.Assert(keys.Type.IsSequence);
            KeysConst = keys;
            TypeKey = keys.Type.ItemTypeOrThis;
            Rng = rng;
            Svars = svars;
        }
    }

    /// <summary>
    /// Base class for variables that map to a set of indicator (bit-valued) variables. This handles
    /// the "item" and "subset" cases mentioned above.
    /// </summary>
    public abstract class IndicatorVarEntry : KeyedVarEntry
    {
        public sealed override DType TypeVar => DType.BitReq;

        private protected IndicatorVarEntry(BndArrConstNode keys, IdRng rng, SvarTuple svars)
            : base(keys, rng, svars)
        {
        }
    }

    /// <summary>
    /// For a subset variable, where the value of the variable is a sub-sequence of its domain/key sequence.
    /// </summary>
    public sealed class SubsetVarEntry : IndicatorVarEntry
    {
        public override ModSimpleVar Symbol { get; }
        public override DType Type { get; }

        private SubsetVarEntry(ModSimpleVar sym, BndArrConstNode keys, IdRng rng, SvarTuple svars)
            : base(keys, rng, svars)
        {
            Validation.AssertValue(sym);
            Type = KeysConst.Type;
            Symbol = sym;
        }

        /// <summary>
        /// Should be used only by <see cref="SymbolMap"/>.
        /// </summary>
        internal static SubsetVarEntry Create(SymbolMap map, ModSimpleVar sym, BndArrConstNode keys)
        {
            Validation.AssertValue(map);
            Validation.AssertValue(sym);
            Validation.Assert(!map._entries.ContainsKey(sym.Name));
            Validation.Assert(keys != null && keys.Length > 0);

            var (rng, svars) = BuildSvars(map, keys.Length, DType.BitReq);
            var res = new SubsetVarEntry(sym, keys, rng, svars);
            map._entries.Add(sym.Name, res);
            return res;
        }
    }

    /// <summary>
    /// For an item variable, where the value is a single item from its domain/key sequence, or possibly null,
    /// when the variable is marked "opt".
    /// </summary>
    public sealed class ItemVarEntry : IndicatorVarEntry
    {
        public override ModItemVar Symbol { get; }
        public override DType Type => Symbol.Type;

        private ItemVarEntry(ModItemVar sym, BndArrConstNode keys, IdRng rng, SvarTuple svars)
            : base(keys, rng, svars)
        {
            Validation.AssertValue(sym);
            Symbol = sym;
        }

        /// <summary>
        /// Should be used only by <see cref="SymbolMap"/>.
        /// </summary>
        internal static ItemVarEntry Create(SymbolMap map, ModItemVar sym, BndArrConstNode keys)
        {
            Validation.AssertValue(map);
            Validation.AssertValue(sym);
            Validation.Assert(!map._entries.ContainsKey(sym.Name));
            Validation.Assert(keys != null && keys.Length > 0);

            var (rng, svars) = BuildSvars(map, keys.Length, DType.BitReq);
            var res = new ItemVarEntry(sym, keys, rng, svars);
            map._entries.Add(sym.Name, res);
            return res;
        }
    }
}
