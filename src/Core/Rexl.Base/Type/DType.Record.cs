// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl;

partial struct DType
{
    /// <summary>
    /// Used for representing record and module types. This is a red black tree with key <see cref="DName"/>
    /// and value <see cref="DType"/>. Each type's <see cref="DType.HasReq"/> property will be <c>false</c>.
    /// Optness is represented separately in a <see cref="BitSet"/>.
    /// </summary>
    private abstract class NameTypeTree<TTree> : DNameRedBlackTree<TTree, DType>
        where TTree : NameTypeTree<TTree>
    {
        protected NameTypeTree(Node? root)
            : base(root)
        {
        }

        protected override bool KeyIsValid(DName key)
        {
            return key.IsValid;
        }

        protected override bool ValIsValid(DType val)
        {
            return val.IsValid && !val.HasReq;
        }

        protected override bool ValEquals(DType val0, DType val1)
        {
            return val0 == val1;
        }

        protected override int ValHash(DType val)
        {
            return val.GetHashCode();
        }

        protected override ushort GetFlags(DName key, DType val)
        {
            return (ushort)val.Flags;
        }

        public DTypeFlags Flags
        {
            get
            {
                if (_root == null)
                    return 0;
                return (DTypeFlags)_root.Flags;
            }
        }
    }

    /// <summary>
    /// The name/type tree for record types.
    /// </summary>
    private sealed class RecordTree : NameTypeTree<RecordTree>, IEquatable<RecordTree>
    {
        public static readonly RecordTree Empty = new RecordTree(null);

        private RecordTree(Node? root)
            : base(root)
        {
            Validation.Assert(!UsesTag);
        }

        public static RecordTree CreateFromRoot(object? root)
        {
            if (root is Node node)
                return new RecordTree(node);

            Validation.Assert(root is null);
            return Empty;
        }

        protected override RecordTree Wrap(Node? root)
        {
            return root == null ? Empty : root == _root ? this : new RecordTree(root);
        }
    }

    /// <summary>
    /// The name/type tree for module types.
    /// </summary>
    private sealed class ModuleTree : NameTypeTree<ModuleTree>, IEquatable<ModuleTree>
    {
        public static readonly ModuleTree Empty = new ModuleTree(null);

        private readonly RecordTree _rec;

        private ModuleTree(Node? root)
            : base(root)
        {
            // Note that we use the nodes from the module tree directly as the nodes of the
            // record tree. Of course, the tags are ignored on the record side.
            _rec = RecordTree.CreateFromRoot(_root);
        }

        // The tag is the symbol kind.
        protected override bool UsesTag => true;

        protected override ModuleTree Wrap(Node? root)
        {
            return root == null ? Empty : root == _root ? this : new ModuleTree(root);
        }

        /// <summary>
        /// Gets a <see cref="RecordTree"/> with fields the same as the symbols in this module.
        /// </summary>
        public RecordTree GetRecordTree() => _rec;
    }

    /// <summary>
    /// Base class for record info and module info classes.
    /// </summary>
    private abstract class NameTypeInfo<TTree>
        where TTree : NameTypeTree<TTree>
    {
        protected readonly TTree _tree;
        protected readonly BitSet _reqToOpt;

        protected NameTypeInfo(TTree tree, BitSet reqToOpt)
        {
#if DEBUG
            Validation.AssertValue(tree);
            Validation.Assert(!reqToOpt.TestAtOrAbove(tree.Count));
            int i = 0;
            foreach (var pair in tree.GetPairs())
            {
                Validation.Assert(!pair.val.HasReq);
                Validation.Assert(!pair.val.IsOpt || !reqToOpt.TestBit(i));
                i++;
            }
#endif

            _tree = tree;
            _reqToOpt = reqToOpt;
        }

        public DTypeFlags Flags
        {
            get
            {
                var flags = _tree.Flags;
                if (!_reqToOpt.IsEmpty)
                    flags |= DTypeFlags.HasOpt | DTypeFlags.HasRemovableOpt;
                return flags;
            }
        }

        public int Count => _tree.Count;

        public IEnumerable<(DName name, DType type)> GetPairs()
        {
            if (_reqToOpt.IsEmpty)
                return _tree.GetPairs();
            return GetPairsCore();
        }

        private IEnumerable<(DName name, DType type)> GetPairsCore()
        {
            int i = 0;
            foreach (var pair in _tree.GetPairs())
            {
                Validation.Assert(!pair.val.HasReq);
                if (_reqToOpt.TestBit(i))
                {
                    Validation.Assert(!pair.val.IsOpt);
                    yield return (pair.key, pair.val.ToOpt());
                }
                else
                    yield return pair;
                i++;
            }
        }

        public bool Contains(DName name) => _tree.ContainsKey(name);

        public bool TryGetValue(DName name, out DType type)
        {
            if (!_tree.TryGetValue(name, out type, out int index))
                return false;
            Validation.Assert(!type.HasReq);
            if (_reqToOpt.TestBit(index))
            {
                Validation.Assert(!type.IsOpt);
                type = type.ToOpt();
            }
            return true;
        }

        public bool TryGetValue(DName name, out DType type, out int index)
        {
            if (!_tree.TryGetValue(name, out type, out index))
                return false;
            Validation.Assert(!type.HasReq);
            if (_reqToOpt.TestBit(index))
            {
                Validation.Assert(!type.IsOpt);
                type = type.ToOpt();
            }
            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_tree, _reqToOpt);
        }

        public override bool Equals(object? obj)
        {
            // REVIEW: Not sure if this should delegate to the static Equals methods.
            // Perhaps this should assert false?
            return base.Equals(obj);
        }
    }

    /// <summary>
    /// This is the _detail field of a Record type. Contains a red-black tree mapping from DName to DType.
    /// Both DName and DType are required to be valid.
    /// </summary>
    private sealed class RecordInfo : NameTypeInfo<RecordTree>, IEquatable<RecordInfo>
    {
        public static readonly RecordInfo Empty = new RecordInfo(RecordTree.Empty, 0);

        private readonly RecordInfo _reqForm;

        private RecordInfo(RecordTree tree, BitSet reqToOpt)
            : base(tree, reqToOpt)
        {
            _reqForm = _reqToOpt.IsEmpty ? this : new RecordInfo(tree, 0);
            Validation.Assert(_reqForm._reqToOpt.IsEmpty);
        }

        public static RecordInfo CreateFromTree(RecordTree tree, BitSet reqToOpt)
        {
            Validation.AssertValue(tree);
            Validation.Assert(!reqToOpt.TestAtOrAbove(tree.Count));

            if (tree.Count > 0)
                return new RecordInfo(tree, reqToOpt);
            return Empty;
        }

        public RecordInfo Create(ReadOnly.Array<DName> names, ReadOnly.Array<DType> types)
        {
            Validation.Assert(names.Length == types.Length);

            BitSet itemsOpt = default;
            DType[]? typesReq = null;
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (!type.HasReq)
                    continue;

                if (typesReq is null)
                {
                    typesReq = new DType[types.Length];
                    types.Copy(0, types.Length, typesReq, 0);
                }
                typesReq[i] = type.ToReq();
                itemsOpt = itemsOpt.SetBit(i);
            }
            Validation.Assert((typesReq is null) == itemsOpt.IsEmpty);

            if (itemsOpt.IsEmpty)
                return CreateFromTree(_tree.Create(names, types), default);

            BitSet reqToOpt = default;
            var tree = _tree.Create(names, typesReq, out int[] indices);
            Validation.Assert(tree.Count <= indices.Length);
            for (int ii = 0; ii < tree.Count; ii++)
            {
                int i = indices[ii];
                if (!itemsOpt.TestBit(i))
                    continue;
                Validation.Assert(types[i].HasReq);
#if DEBUG
                Validation.Assert(tree.TryGetValue(names[i], out var type, out int index));
                Validation.Assert(!type.HasReq);
                Validation.Assert(index == ii);
                Validation.Assert(type == types[i].ToReq());
#endif
                // When there are duplicates, the last one should be kept so we need to explicitly
                // clear the bit to get the right thing.
                reqToOpt = reqToOpt.SetBit(ii);
            }

            return CreateFromTree(tree, reqToOpt);
        }

        public RecordInfo Create(IEnumerable<TypedName> tns)
        {
            Validation.AssertValue(tns);

            BitSet itemsOpt = default;
            var pairs = new List<(DName name, DType type)>();
            int i = 0;
            foreach (var tn in tns)
            {
                if (!tn.Type.HasReq)
                    pairs.Add((tn.Name, tn.Type));
                else
                {
                    pairs.Add((tn.Name, tn.Type.ToReq()));
                    itemsOpt = itemsOpt.SetBit(i);
                }
                i++;
            }

            if (itemsOpt.IsEmpty)
                return CreateFromTree(_tree.Create(pairs), default);

            BitSet reqToOpt = default;
            var tree = _tree.Create(pairs, out int[] indices);
            Validation.Assert(tree.Count <= indices.Length);
            for (int ii = 0; ii < tree.Count; ii++)
            {
                i = indices[ii];
                if (!itemsOpt.TestBit(i))
                    continue;
#if DEBUG
                Validation.Assert(tree.TryGetValue(pairs[i].name, out var type, out int index));
                Validation.Assert(!type.HasReq);
                Validation.Assert(index == ii);
                Validation.Assert(type == pairs[i].type);
#endif
                // When there are duplicates, the last one should be kept so we need to explicitly
                // clear the bit to get the right thing.
                reqToOpt = reqToOpt.SetBit(ii);
            }

            return CreateFromTree(tree, reqToOpt);
        }

        /// <summary>
        /// Returns a <see cref="RecordInfo"/> for the same tree, but with no opt bits set.
        /// </summary>
        public RecordInfo GetReqFieldForm()
        {
            Validation.Assert(_reqForm._tree == _tree);
            Validation.Assert(_reqForm._reqToOpt.IsEmpty);
            return _reqForm;
        }

        /// <summary>
        /// Returns a <see cref="RecordInfo"/> for the same tree, but with given opt bits.
        /// This cleanses the opt bits.
        /// REVIEW: Should this instead throw if any opt bits are set incorrectly?
        /// </summary>
        public RecordInfo SetFieldOpts(BitSet opts)
        {
            // REVIEW: Optimize this.
            int idx = 0;
            foreach (var (name, type) in _tree.GetPairs())
            {
                Validation.Assert(!type.HasReq);
                if (opts.TestBit(idx) && type.IsOpt)
                    opts = opts.ClearBit(idx);
            }

            if (opts == _reqToOpt)
                return this;

            if (opts.IsEmpty)
                return _reqForm;

            return new RecordInfo(_tree, opts);
        }

        public BitSet GetFieldOpts() => _reqToOpt;

        public static bool Equals(RecordInfo? a, RecordInfo? b)
        {
            Validation.AssertValueOrNull(a);
            Validation.AssertValueOrNull(b);

            if (object.ReferenceEquals(a, b))
                return true;
            if (a is null || b is null)
                return false;
            if (a._reqToOpt != b._reqToOpt)
                return false;
            return RecordTree.Equals(a._tree, b._tree);
        }

        public bool Equals(RecordInfo? other)
        {
            Validation.AssertValueOrNull(other);
            return Equals(this, other);
        }

        /// <summary>
        /// Whether the two record infos have the same required field types. That is, whether
        /// the record infos differ only in opt-ness of some of the field types, if at all.
        /// </summary>
        public static bool SameReqs(RecordInfo? a, RecordInfo? b)
        {
            Validation.AssertValueOrNull(a);
            Validation.AssertValueOrNull(b);

            if (object.ReferenceEquals(a, b))
                return true;
            if (a is null || b is null)
                return false;
            return RecordTree.Equals(a._tree, b._tree);
        }

        public RecordInfo SetItem(DName name, DType type)
        {
            bool hasReq = type.HasReq;
            if (hasReq)
                type = type.ToReq();
            var tree = _tree.SetItem(name, type, out int index, out bool isNew);
            Validation.AssertIndex(index, tree.Count);
            BitSet reqToOpt;
            if (isNew)
            {
                Validation.Assert(tree.Count == _tree.Count + 1);
                reqToOpt = _reqToOpt.Insert(index, hasReq);
            }
            else
            {
                Validation.Assert(tree.Count == _tree.Count);
                reqToOpt = hasReq ? _reqToOpt.SetBit(index) : _reqToOpt.ClearBit(index);
                if (reqToOpt == _reqToOpt && RecordTree.Equals(tree, _tree))
                    return this;
            }
            return CreateFromTree(tree, reqToOpt);
        }

        public RecordInfo RemoveItem(DName name) => RemoveItemCore(name, known: false);

        public RecordInfo RemoveKnownItem(DName name) => RemoveItemCore(name, known: true);

        private RecordInfo RemoveItemCore(DName name, bool known)
        {
            var tree = _tree.RemoveItem(name, out int index);
            if (index < 0)
            {
                Validation.Assert(!known);
                Validation.Assert(tree == _tree);
                return this;
            }

            Validation.Assert(tree.Count == _tree.Count - 1);
            Validation.AssertIndex(index, _tree.Count);
            var reqToOpt = _reqToOpt.Delete(index);
            return CreateFromTree(tree, reqToOpt);
        }
    }

    /// <summary>
    /// This is the _detail field of a Module type. Contains a red-black tree mapping from DName to DType
    /// with tag containing the symbol kind. Both DName and DType are required to be valid. Note that the
    /// red-black tree nodes for the module are used directly as the tree for the associated record type.
    /// For the latter, the tag values are ignored.
    /// </summary>
    private sealed class ModuleInfo : NameTypeInfo<ModuleTree>, IEquatable<ModuleInfo>
    {
        public static readonly ModuleInfo Empty = new ModuleInfo(ModuleTree.Empty, 0);

        private readonly RecordInfo _rec;

        private ModuleInfo(ModuleTree tree, BitSet reqToOpt)
            : base(tree, reqToOpt)
        {
            // Note that we use the nodes from the module tree directly as the nodes of the
            // record tree. Of course, the tags are ignored on the record side.
            _rec = RecordInfo.CreateFromTree(_tree.GetRecordTree(), _reqToOpt);
        }

        private static ModuleInfo CreateFromTree(ModuleTree tree, BitSet reqToOpt)
        {
            Validation.AssertValue(tree);
            Validation.Assert(!reqToOpt.TestAtOrAbove(tree.Count));

            if (tree.Count > 0)
                return new ModuleInfo(tree, reqToOpt);
            return Empty;
        }

        public static bool Equals(ModuleInfo? a, ModuleInfo? b)
        {
            Validation.AssertValueOrNull(a);
            Validation.AssertValueOrNull(b);

            if (object.ReferenceEquals(a, b))
                return true;
            if (a is null || b is null)
                return false;
            if (a._reqToOpt != b._reqToOpt)
                return false;
            return ModuleTree.Equals(a._tree, b._tree);
        }

        public bool Equals(ModuleInfo? other)
        {
            Validation.AssertValueOrNull(other);
            return Equals(this, other);
        }

        /// <summary>
        /// Gets a <see cref="RecordInfo"/> with fields the same as the symbols in this module.
        /// </summary>
        public RecordInfo GetRecordInfo() => _rec;

        public IEnumerable<(DName name, DType type, ModSymKind sk)> GetInfos()
        {
            int i = 0;
            foreach (var (name, typeReq, tag) in _tree.GetInfos())
            {
                Validation.Assert(!typeReq.HasReq);
                DType type;
                if (_reqToOpt.TestBit(i))
                {
                    Validation.Assert(!typeReq.IsOpt);
                    type = typeReq.ToOpt();
                }
                else
                    type = typeReq;
                var sk = (ModSymKind)tag;
                Validation.Assert(sk.IsValid());
                yield return (name, type, sk);
                i++;
            }
        }

        public bool TryGetValue(DName name, out DType type, out ModSymKind sk)
        {
            if (!_tree.TryGetValue(name, out type, out int index, out var tag))
            {
                sk = default;
                return false;
            }

            if (_reqToOpt.TestBit(index))
            {
                Validation.Assert(!type.IsOpt);
                type = type.ToOpt();
            }
            else
                Validation.Assert(!type.HasReq);

            sk = (ModSymKind)tag;
            Validation.Assert(sk.IsValid());

            return true;
        }

        public ModuleInfo SetItem(DName name, DType type, ModSymKind sk)
        {
            Validation.Assert(name.IsValid);
            Validation.Assert(type.IsValid);
            Validation.Assert(sk.IsValid());

            bool hasReq = type.HasReq;
            if (hasReq)
                type = type.ToReq();
            var tree = _tree.SetItem(name, type, out int index, out bool isNew, (byte)sk);
            Validation.AssertIndex(index, tree.Count);
            BitSet reqToOpt;
            if (isNew)
            {
                Validation.Assert(tree.Count == _tree.Count + 1);
                reqToOpt = _reqToOpt.Insert(index, hasReq);
            }
            else
            {
                Validation.Assert(tree.Count == _tree.Count);
                reqToOpt = hasReq ? _reqToOpt.SetBit(index) : _reqToOpt.ClearBit(index);
                if (reqToOpt == _reqToOpt && RecordTree.Equals(tree, _tree))
                    return this;
            }
            return CreateFromTree(tree, reqToOpt);
        }
    }

    private static RecordInfo _GetRecordInfo(object? detail)
    {
        Validation.Assert(detail is RecordInfo);
        return (RecordInfo)detail;
    }

    private RecordInfo _GetRecordInfo()
    {
        Validation.Assert(_kind == DKind.Record);
        Validation.Assert(_detail is RecordInfo);
        return (RecordInfo)_detail;
    }

    private static ModuleInfo _GetModuleInfo(object? detail)
    {
        Validation.Assert(detail is ModuleInfo);
        return (ModuleInfo)detail;
    }

    private ModuleInfo _GetModuleInfo()
    {
        Validation.Assert(_kind == DKind.Module);
        Validation.Assert(_detail is ModuleInfo);
        return (ModuleInfo)_detail;
    }

    public static DType CreateRecord(bool opt, params TypedName[] typedNames)
    {
        return _CreateRecordCore(opt, typedNames);
    }

    public static DType CreateRecord(bool opt, IEnumerable<TypedName> typedNames)
    {
        return _CreateRecordCore(opt, typedNames);
    }

    /// <summary>
    /// Creates a record type from parallel arrays for <paramref name="names"/> and <paramref name="types"/>.
    /// </summary>
    public static DType CreateRecord(bool opt, ReadOnly.Array<DName> names, ReadOnly.Array<DType> types)
    {
        return _CreateRecordCore(opt, names, types);
    }

    public static DType CreateTable(bool opt, params TypedName[] typedNames)
    {
        return _CreateRecordCore(opt, typedNames, 1);
    }

    public static DType CreateTable(bool opt, IEnumerable<TypedName> typedNames)
    {
        return _CreateRecordCore(opt, typedNames, 1);
    }

    private static DType _CreateRecordCore(bool opt, IEnumerable<TypedName> typedNames, int seqCount = 0)
    {
        Validation.BugCheckValue(typedNames, nameof(typedNames));
        foreach (var tn in typedNames)
        {
            Validation.BugCheckParam(tn.Name.IsValid, nameof(typedNames));
            Validation.BugCheckParam(tn.Type.IsValid, nameof(typedNames));
        }
        Validation.BugCheckParam(seqCount >= 0, nameof(seqCount));

        return new DType(RecordInfo.Empty.Create(typedNames), opt, seqCount);
    }

    private static DType _CreateRecordCore(bool opt, ReadOnly.Array<DName> names, ReadOnly.Array<DType> types,
        int seqCount = 0)
    {
        Validation.BugCheck(names.Length == types.Length);
        for (int i = 0; i < names.Length; i++)
        {
            Validation.BugCheckParam(names[i].IsValid, nameof(names));
            Validation.BugCheckParam(types[i].IsValid, nameof(types));
        }
        Validation.BugCheckParam(seqCount >= 0, nameof(seqCount));

        return new DType(RecordInfo.Empty.Create(names, types), opt, seqCount);
    }

    /// <summary>
    /// When RootType is a Record type, returns the number of fields of the record. Otherwise, returns -1.
    /// </summary>
    public int FieldCount
    {
        get
        {
            AssertValidOrDefault();
            return _kind == DKind.Record ? _GetRecordInfo().Count : -1;
        }
    }

    /// <summary>
    /// When RootType is a Module type, returns the number of symbols of the module. Otherwise, returns -1.
    /// </summary>
    public int SymbolCount
    {
        get
        {
            AssertValidOrDefault();
            return _kind == DKind.Module ? _GetModuleInfo().Count : -1;
        }
    }

    /// <summary>
    /// Returns whether the two types are record types with the same required field types.
    /// That is, returns true if they differ only in opt-ness of some of the field types
    /// and/or opt-ness at the top level (if at all).
    /// </summary>
    public bool SameFieldReqs(DType other)
    {
        if (!IsRecordXxx)
            return false;
        if (!other.IsRecordXxx)
            return false;
        return RecordInfo.SameReqs(_GetRecordInfo(), other._GetRecordInfo());
    }

    /// <summary>
    /// For this record type, get the record type that has optness stripped from field types
    /// and at the top level.
    /// </summary>
    public DType GetReqFieldType()
    {
        Validation.BugCheck(IsRecordXxx);

        var info = _GetRecordInfo();
        var infoReq = info.GetReqFieldForm();
        if (info == infoReq && !_opt)
            return this;
        return new DType(DKind.Record, opt: false, seqCount: 0, infoReq);
    }

    /// <summary>
    /// For this record type, get the record type that has the field optness set according
    /// to the given bit set.
    /// </summary>
    public DType SetFieldOpts(BitSet opts)
    {
        Validation.BugCheck(IsRecordXxx);

        var info = _GetRecordInfo();
        var infoRes = info.SetFieldOpts(opts);
        if (infoRes == info)
            return this;
        return new DType(DKind.Record, opt: _opt, seqCount: 0, infoRes);
    }

    /// <summary>
    /// For this record type, get the opt field bits, which has bits set for each field whose
    /// type is opt and has an associated required type.
    /// </summary>
    public BitSet GetFieldOpts()
    {
        Validation.BugCheck(IsRecordXxx);

        var info = _GetRecordInfo();
        return info.GetFieldOpts();
    }

    /// <summary>
    /// Return whether the RootType is a Record or Module and contains a field/symbol with the given name.
    /// </summary>
    public bool Contains(DName name)
    {
        AssertValidOrDefault();

        if (name.IsValid)
        {
            switch (_kind)
            {
            case DKind.Record:
                return _GetRecordInfo().Contains(name);
            case DKind.Module:
                return _GetModuleInfo().Contains(name);
            }
        }
        return false;
    }

    /// <summary>
    /// If the RootType is a Record or Module and contains a field/symbol with the given name, returns
    /// the type of the field.
    /// Otherwise returns default(DType).
    /// </summary>
    public DType GetNameTypeOrDefault(DName name)
    {
        AssertValidOrDefault();

        if (name.IsValid)
        {
            DType type;
            switch (_kind)
            {
            case DKind.Record:
                if (_GetRecordInfo().TryGetValue(name, out type))
                    return type;
                break;
            case DKind.Module:
                if (_GetModuleInfo().TryGetValue(name, out type))
                    return type;
                break;
            }
        }
        return default;
    }

    /// <summary>
    /// If name isn't valid, returns false. Otherwise, if RootType is not a Record or Module type, returns false.
    /// Otherwise, returns whether the root Record/Module type has a field/symbol with the given name, and sets
    /// <paramref name="type"/> accordingly.
    /// </summary>
    public bool TryGetNameType(DName name, out DType type)
    {
        AssertValidOrDefault();

        if (name.IsValid)
        {
            switch (_kind)
            {
            case DKind.Record:
                return _GetRecordInfo().TryGetValue(name, out type);
            case DKind.Module:
                return _GetModuleInfo().TryGetValue(name, out type);
            }
        }
        type = default;
        return false;
    }

    /// <summary>
    /// If name isn't valid, returns false. Otherwise, if RootType is not a Record or Module type, returns false.
    /// Otherwise, returns whether the root Record/Module type has a field/symbol with the given name, and sets
    /// <paramref name="type"/> and <paramref name="index"/> accordingly.
    /// </summary>
    public bool TryGetNameType(DName name, out DType type, out int index)
    {
        AssertValidOrDefault();

        if (name.IsValid)
        {
            switch (_kind)
            {
            case DKind.Record:
                return _GetRecordInfo().TryGetValue(name, out type, out index);
            case DKind.Module:
                return _GetModuleInfo().TryGetValue(name, out type, out index);
            }
        }
        type = default;
        index = -1;
        return false;
    }

    /// <summary>
    /// If name isn't valid, returns false. Otherwise, if RootType is not a Module type, returns false.
    /// Otherwise, returns whether the root Module type has a symbol with the given name, and sets
    /// <paramref name="type"/> and <paramref name="sk"/> accordingly.
    /// </summary>
    public bool TryGetSymbolNameType(DName name, out DType type, out ModSymKind sk)
    {
        AssertValidOrDefault();

        if (name.IsValid && _kind == DKind.Module && _GetModuleInfo().TryGetValue(name, out type, out sk))
        {
            Validation.Assert(sk.IsValid());
            return true;
        }

        type = default;
        sk = default;
        return false;
    }

    /// <summary>
    /// Asserts that the RootType of this type is a Record type, and returns the result of
    /// setting the type of the indicated field.
    /// </summary>
    public DType SetNameType(DName name, DType type)
    {
        BugCheckValid();
        Validation.BugCheck(_kind == DKind.Record);
        Validation.BugCheckParam(name.IsValid, nameof(name));
        type.BugCheckValid();

        var tree = _GetRecordInfo().SetItem(name, type);
        return new DType(tree, _opt, _seqCount);
    }

    /// <summary>
    /// Asserts that the RootType of this type is a Module type, and returns the result of
    /// setting the type and kind of the indicated symbol.
    /// </summary>
    public DType SetNameType(DName name, DType type, ModSymKind sk)
    {
        BugCheckValid();
        Validation.BugCheck(_kind == DKind.Module);
        Validation.BugCheckParam(name.IsValid, nameof(name));
        type.BugCheckValid();
        Validation.BugCheckParam(sk.IsValid(), nameof(sk));

        var tree = _GetModuleInfo().SetItem(name, type, sk);
        return new DType(tree, _opt, _seqCount);
    }

    /// <summary>
    /// Asserts that the RootType of this type is a Record type and that the record doesn't
    /// contain a field with the given name. Returns the result of adding the field with the
    /// given type.
    /// </summary>
    public DType AddNameType(DName name, DType type)
    {
        BugCheckValid();
        Validation.BugCheck(_kind == DKind.Record);
        Validation.BugCheckParam(name.IsValid, nameof(name));
        type.BugCheckValid();

        var info = _GetRecordInfo();
        Validation.Assert(!info.Contains(name));
        return new DType(info.SetItem(name, type), _opt, _seqCount);
    }

    /// <summary>
    /// Return a new type based on this, with an additional field.
    /// </summary>
    public DType Add(TypedName tn)
    {
        return AddNameType(tn.Name, tn.Type);
    }

    /// <summary>
    /// Asserts that the RootType of this type is a Module type and that the module doesn't
    /// contain a symbol with the given name. Returns the result of adding the symbol with the
    /// given type and symbol kind.
    /// </summary>
    public DType AddNameType(DName name, DType type, ModSymKind sk)
    {
        BugCheckValid();
        Validation.BugCheck(_kind == DKind.Module);
        Validation.BugCheckParam(name.IsValid, nameof(name));
        type.BugCheckValid();
        Validation.BugCheckParam(sk.IsValid(), nameof(sk));

        var info = _GetModuleInfo();
        Validation.Assert(!info.Contains(name));
        return new DType(info.SetItem(name, type, sk), _opt, _seqCount);
    }

    /// <summary>
    /// Return a new type based on this, with an additional symbol.
    /// </summary>
    public DType Add(TypedSymName tsn)
    {
        return AddNameType(tsn.Name, tsn.Type, tsn.SymKind);
    }

    /// <summary>
    /// Checks that the RootType of this type is a Record type. Returns the result of removing the
    /// given field, if it is there.
    /// </summary>
    public DType DropName(DName name)
    {
        BugCheckValid();
        Validation.BugCheck(_kind == DKind.Record);
        Validation.BugCheckParam(name.IsValid, nameof(name));

        var tree = _GetRecordInfo().RemoveItem(name);
        return new DType(tree, _opt, _seqCount);
    }

    /// <summary>
    /// If the root is not a record or module type, produces an empty sequence. Otherwise, produces
    /// the sequence of typed field/symbol names.
    /// </summary>
    public IEnumerable<TypedName> GetNames()
    {
        BugCheckValid();

        switch (_kind)
        {
        case DKind.Record:
            return _GetRecordInfo().GetPairs().Select(pair => new TypedName(pair.name, pair.type));
        case DKind.Module:
            return _GetModuleInfo().GetPairs().Select(pair => new TypedName(pair.name, pair.type));
        }
        return Array.Empty<TypedName>();
    }

    /// <summary>
    /// If the root is not a record or module type, produces an empty sequence. Otherwise, produces
    /// the sequence of (name, type, index) triples for the fields/symbols.
    /// </summary>
    public IEnumerable<(DName name, DType type, int index)> GetFields()
    {
        BugCheckValid();

        switch (_kind)
        {
        case DKind.Record:
            return _GetRecordInfo().GetPairs().Select((pair, idx) => (pair.name, pair.type, idx));
        case DKind.Module:
            return _GetModuleInfo().GetPairs().Select((pair, idx) => (pair.name, pair.type, idx));
        }
        return Array.Empty<(DName, DType, int)>();
    }

    /// <summary>
    /// If the root is not a module type, produces an empty sequence. Otherwise, produces
    /// the sequence of typed symbol names (and kinds).
    /// </summary>
    public IEnumerable<TypedSymName> GetSymNames()
    {
        BugCheckValid();

        switch (_kind)
        {
        case DKind.Module:
            return _GetModuleInfo().GetInfos().Select(
                info => new TypedSymName(info.name, info.type, info.sk));
        }
        return Array.Empty<TypedSymName>();
    }

    /// <summary>
    /// Get the record type corresponding to this module type.
    /// </summary>
    public DType ModuleToRecord()
    {
        BugCheckValid();
        Validation.BugCheck(_kind == DKind.Module);
        return new DType(_GetModuleInfo().GetRecordInfo(), _opt, _seqCount);
    }

    /// <summary>
    /// Return true if this and <paramref name="type"/> are both required record types
    /// and the fields of this are a subset of the fields of <paramref name="type"/>.
    /// </summary>
    public bool IsSubRecord(DType type)
    {
        if (!IsRecordReq)
            return false;
        if (!type.IsRecordReq)
            return false;

        var infoSml = _GetRecordInfo();
        var infoBig = type._GetRecordInfo();
        if (infoSml == infoBig)
            return true;

        if (infoSml.Count == 0)
            return true;
        if (infoSml.Count > infoBig.Count)
            return false;

        using var atorSml = infoSml.GetPairs().GetEnumerator();
        using var atorBig = infoBig.GetPairs().GetEnumerator();
        while (atorSml.MoveNext())
        {
            var (keySml, valSml) = atorSml.Current;
            for (; ; )
            {
                if (!atorBig.MoveNext())
                    return false;
                var (keyBig, valBig) = atorBig.Current;
                if (keyBig == keySml)
                {
                    if (valBig != valSml)
                        return false;
                    break;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Implements acceptance for record types. That is, return true if <paramref name="infoDst"/> is a super-type
    /// of <paramref name="infoSrc"/>. See the comment on <see cref="UseUnionDefault"/> for precise specification.
    /// </summary>
    private static bool _RecordAccepts(RecordInfo infoDst, RecordInfo infoSrc, bool union)
    {
        Validation.AssertValue(infoDst);
        Validation.AssertValue(infoSrc);

        // Test for the same instance.
        if (infoDst == infoSrc)
            return true;

        // The number of names in dst minus the number of names in src.
        int diff = infoDst.Count - infoSrc.Count;
        if (!union)
        {
            // Everything in dst needs to be in src.
            if (diff > 0)
                return false;
            foreach (var (name, type) in infoDst.GetPairs())
            {
                Validation.Assert(type.IsValid);
                if (!infoSrc.TryGetValue(name, out DType typeSrc))
                    return false;
                if (!type.Accepts(typeSrc, union))
                    return false;
            }
        }
        else
        {
            // Everything in src needs to be in dst. Any extra fields in dst need to be opt.
            if (diff < 0)
                return false;
            int cfldSrc = 0;
            foreach (var (name, type) in infoDst.GetPairs())
            {
                Validation.Assert(type.IsValid);
                if (infoSrc.TryGetValue(name, out DType typeSrc))
                {
                    // Both records have this field. Make sure their types work.
                    if (!type.Accepts(typeSrc, union))
                        return false;
                    cfldSrc++;
                }
                else
                {
                    // Make sure the dst field is opt.
                    if (!type.IsOpt)
                        return false;
                    // Not in src. Make sure we have enough "surplus" fields left.
                    if (--diff < 0)
                        return false;
                }
            }
            Validation.Assert(cfldSrc == infoSrc.Count);
            Validation.Assert(diff == 0);
        }

        return true;
    }

    private static RecordInfo _GetRecordSuperType(RecordInfo info1, RecordInfo info2, bool union, ref bool toGen)
    {
        Validation.AssertValue(info1);
        Validation.AssertValue(info2);

        // Make tree1 be the smaller when union is false, the larger when union is true.
        if (!union && info1.Count > info2.Count || union && info1.Count < info2.Count)
            Util.Swap(ref info1, ref info2);

        // Start with tree1. The code below assumes this.
        RecordInfo infoRes = info1;

        using var ator1 = info1.GetPairs().GetEnumerator();
        using var ator2 = info2.GetPairs().GetEnumerator();
        bool fAtor1 = ator1.MoveNext();
        bool fAtor2 = ator2.MoveNext();

        while (fAtor1 && fAtor2)
        {
            var kvp1 = ator1.Current;
            var kvp2 = ator2.Current;
            int cmp = DName.Compare(kvp1.name, kvp2.name);
            if (cmp == 0)
            {
                DType typeFld = GetSuperTypeCore(kvp1.type, kvp2.type, union, ref toGen);
                if (typeFld != kvp1.type)
                    infoRes = infoRes.SetItem(kvp1.name, typeFld);
                fAtor1 = ator1.MoveNext();
                fAtor2 = ator2.MoveNext();
            }
            else if (cmp < 0)
            {
                // ator1 has an item that ator2 does not.
                if (!union)
                    infoRes = infoRes.RemoveKnownItem(kvp1.name);
                else if (!kvp1.type.IsOpt)
                    infoRes = infoRes.SetItem(kvp1.name, kvp1.type.ToOpt());
                fAtor1 = ator1.MoveNext();
            }
            else
            {
                // ator2 has an item that ator1 does not.
                if (union)
                    infoRes = infoRes.SetItem(kvp2.name, kvp2.type.ToOpt());
                fAtor2 = ator2.MoveNext();
            }
        }

        Validation.Assert(!fAtor1 || !fAtor2);
        if (!union)
        {
            // If we still have fields in ator1, they need to be removed.
            while (fAtor1)
            {
                var kvp = ator1.Current;
                infoRes = infoRes.RemoveKnownItem(kvp.name);
                fAtor1 = ator1.MoveNext();
            }
        }
        else
        {
            // If we still have fields in ator1, they need to be forced opt.
            while (fAtor1)
            {
                var (name, type) = ator1.Current;
                if (!type.IsOpt)
                    infoRes = infoRes.SetItem(name, type.ToOpt());
                fAtor1 = ator1.MoveNext();
            }
            // If we still have fields in ator2, they need to be added and forced opt.
            while (fAtor2)
            {
                var kvp = ator2.Current;
                infoRes = infoRes.SetItem(kvp.name, kvp.type.ToOpt());
                fAtor2 = ator2.MoveNext();
            }
        }

        return infoRes;
    }

    private static void _AppendRecordType(TextSink sink, RecordInfo info, bool opt, bool compact)
    {
        Validation.AssertValue(sink);

        sink.Write('{');

        string strPre = "";
        string sep = compact ? "," : ", ";
        foreach (var (name, type) in info.GetPairs())
        {
            Validation.Assert(type.IsValid);
            sink.Write(strPre);
            sink.WriteEscapedName(name);
            sink.Write(':');
            type.AppendTo(sink, compact);
            strPre = sep;
        }

        sink.Write('}');
        if (opt)
            sink.Write('?');
    }
}

/// <summary>
/// A simple name together with a DType.
/// </summary>
public struct TypedName : IEquatable<TypedName>
{
    public readonly DName Name;
    public readonly DType Type;

    public TypedName(DName name, DType type)
    {
        Validation.BugCheckParam(name.IsValid, nameof(name));
        Validation.BugCheckParam(type.IsValid, nameof(type));

        Name = name;
        Type = type;
    }

    public TypedName(string name, DType type)
        : this(new DName(name), type)
    {
    }

    public (DName, DType) ToPair()
    {
        Validation.BugCheck(IsValid);
        return (Name, Type);
    }

    public override string ToString()
    {
        if (!IsValid)
            return "<invalid>";

        var sink = new SbTextSink();
        sink.WriteEscapedName(Name).Write(':');
        Type.AppendTo(sink);
        return sink.Builder.ToString();
    }

    public static bool operator ==(TypedName tn1, TypedName tn2)
    {
        return tn1.Name == tn2.Name && tn1.Type == tn2.Type;
    }

    public static bool operator !=(TypedName tn1, TypedName tn2)
    {
        return !(tn1 == tn2);
    }

    public bool Equals(TypedName other)
    {
        return this == other;
    }

    public override bool Equals(object? obj)
    {
        Validation.AssertValueOrNull(obj);
        if (obj is not TypedName tn)
            return false;
        return this == tn;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name);
    }

    public bool IsValid { get { return Name.IsValid && Type.IsValid; } }
}

/// <summary>
/// A <see cref="DName"/> together with a <see cref="DType"/> and <see cref="ModSymKind"/>.
/// </summary>
public struct TypedSymName : IEquatable<TypedSymName>
{
    public readonly DName Name;
    public readonly DType Type;
    public readonly ModSymKind SymKind;

    public TypedSymName(DName name, DType type, ModSymKind sk)
    {
        Validation.BugCheckParam(name.IsValid, nameof(name));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        Validation.BugCheckParam(sk.IsValid(), nameof(sk));

        Name = name;
        Type = type;
        SymKind = sk;
    }

    public override string ToString()
    {
        if (!IsValid)
            return "<invalid>";

        var sink = new SbTextSink();
        sink.TWrite(SymKind.ToStr()).TWrite(' ').WriteEscapedName(Name).Write(':');
        Type.AppendTo(sink);
        return sink.Builder.ToString();
    }

    public static bool operator ==(TypedSymName tn1, TypedSymName tn2)
    {
        return tn1.SymKind == tn2.SymKind && tn1.Name == tn2.Name && tn1.Type == tn2.Type;
    }

    public static bool operator !=(TypedSymName tn1, TypedSymName tn2)
    {
        return !(tn1 == tn2);
    }

    public bool Equals(TypedSymName other)
    {
        return this == other;
    }

    public override bool Equals(object? obj)
    {
        Validation.AssertValueOrNull(obj);
        if (obj is not TypedSymName tn)
            return false;
        return this == tn;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name, SymKind);
    }

    public bool IsValid { get { return Name.IsValid && Type.IsValid && SymKind.IsValid(); } }
}
