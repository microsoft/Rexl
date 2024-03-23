// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using Integer = System.Numerics.BigInteger;

// This partial is for representation of values compatible with this type manager as
// BoundCompoundConstNode values.
partial class TypeManager
{
    /// <summary>
    /// Tries to wrap the given value as a <see cref="BndConstNode"/> of the given <paramref name="type"/>.
    /// Note that if <paramref name="type"/> has a required form, the result may have the required type,
    /// rather then the opt type.
    /// </summary>
    public bool TryWrapConst(DType type, object val, out BndConstNode bcn)
    {
        Validation.BugCheckParam(type.IsValid, nameof(type));

        if (val == null)
        {
            if (!type.IsOpt)
            {
                bcn = null;
                return false;
            }
            bcn = BndNullNode.Create(type);
            return true;
        }

        if (type.HasReq)
            type = type.ToReq();

        if (!TryEnsureSysType(type, out Type st))
        {
            bcn = null;
            return false;
        }

        if (!st.IsAssignableFrom(val.GetType()))
        {
            bcn = null;
            return false;
        }

        switch (type.Kind)
        {
        case DKind.Sequence:
            Validation.Assert(val is IEnumerable);
            if (!TryGetArray(type, (IEnumerable)val, out var stItem, out var arr))
                break;
            bcn = BndArrConstNode.Create(this, type, arr);
            return true;

        case DKind.Record:
            Validation.Assert(val.GetType() == st);
            bcn = BndRecConstNode.Create(this, type, (RecordBase)val);
            return true;
        case DKind.Tuple:
            Validation.Assert(val.GetType() == st);
            bcn = BndTupConstNode.Create(this, type, (TupleBase)val);
            return true;
        case DKind.Tensor:
            Validation.Assert(val.GetType() == st);
            bcn = BndTenConstNode.Create(this, type, (Tensor)val);
            return true;

        case DKind.Text:
            Validation.Assert(val is string);
            bcn = BndStrNode.Create((string)val);
            return true;

        case DKind.R8:
            Validation.Assert(val is double);
            bcn = BndFltNode.Create(type, (double)val);
            return true;
        case DKind.R4:
            Validation.Assert(val is float);
            bcn = BndFltNode.Create(type, (float)val);
            return true;

        case DKind.IA:
            Validation.Assert(val is Integer);
            bcn = BndIntNode.Create(type, (Integer)val);
            return true;
        case DKind.I8:
            Validation.Assert(val is long);
            bcn = BndIntNode.Create(type, (long)val);
            return true;
        case DKind.I4:
            Validation.Assert(val is int);
            bcn = BndIntNode.Create(type, (int)val);
            return true;
        case DKind.I2:
            Validation.Assert(val is short);
            bcn = BndIntNode.Create(type, (short)val);
            return true;
        case DKind.I1:
            Validation.Assert(val is sbyte);
            bcn = BndIntNode.Create(type, (sbyte)val);
            return true;
        case DKind.U8:
            Validation.Assert(val is ulong);
            bcn = BndIntNode.Create(type, (ulong)val);
            return true;
        case DKind.U4:
            Validation.Assert(val is uint);
            bcn = BndIntNode.Create(type, (uint)val);
            return true;
        case DKind.U2:
            Validation.Assert(val is ushort);
            bcn = BndIntNode.Create(type, (ushort)val);
            return true;
        case DKind.U1:
            Validation.Assert(val is byte);
            bcn = BndIntNode.Create(type, (byte)val);
            return true;
        case DKind.Bit:
            Validation.Assert(val is bool);
            bcn = BndIntNode.CreateBit((bool)val);
            return true;

        case DKind.Date:
        case DKind.Time:
        case DKind.Guid:
        case DKind.Uri:
            // REVIEW: Handle these.
            break;

        default:
            Validation.Assert(false);
            break;
        }

        bcn = null;
        return false;
    }

    /// <summary>
    /// Make an array from an enumerable.
    /// </summary>
    protected virtual bool TryGetArray(DType typeSeq, IEnumerable able, out Type stItem, out Array items)
    {
        Validation.Assert(typeSeq.IsSequence);
        Validation.AssertValue(able);

        var typeItem = typeSeq.ItemTypeOrThis;
        if (!TryEnsureSysType(typeItem, out stItem))
        {
            items = default;
            return false;
        }

        var stAble = able.GetType();
        if (stItem.MakeArrayType().IsAssignableFrom(stAble))
        {
            items = (Array)able;
            return true;
        }

        if (!typeof(IEnumerable<>).MakeGenericType(stItem).IsAssignableFrom(stAble))
        {
            items = default;
            return false;
        }

        var meth = new Func<IEnumerable<object>, object[]>(Enumerable.ToArray).Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
        var res = meth.Invoke(null, new object[] { able });
        items = (Array)res;
        return true;
    }
}

/// <summary>
/// Wraps a runtime value and type manager as a <see cref="BndCmpConstNode"/>.
/// </summary>
public abstract class BndTmConstNode : BndCmpConstNode
{
    /// <summary>
    /// The type manager.
    /// </summary>
    public TypeManager TypeManager { get; }

    /// <summary>
    /// The system type of the value. This is consistent with the <see cref="BndTmConstNode.TypeManager"/> and
    /// <see cref="BoundNode.Type"/> properties. Note that this is assignable from the actual value's runtime
    /// type, but not necessarily identical to that runtime type.
    /// </summary>
    public Type SysType { get; }

    private protected BndTmConstNode(TypeManager tm, DType type, Type st, object value)
        : base(type, value)
    {
        Validation.AssertValue(tm);
        Validation.AssertValue(st);
        Validation.Assert(value != null && st.IsAssignableFrom(value.GetType()));

        TypeManager = tm;
        SysType = st;
    }
}

/// <summary>
/// Wraps a runtime array and type manager as a <see cref="BndCmpConstNode"/>.
/// </summary>
public sealed class BndArrConstNode : BndTmConstNode
{
    /// <summary>
    /// The length of the array.
    /// </summary>
    public int Length => Items.Length;

    /// <summary>
    /// The system type of the item type of <see cref="BoundNode.Type"/>.
    /// </summary>
    public Type ItemSysType { get; }

    /// <summary>
    /// The actual array that is wrapped. Note that this should be considered immutable! It should never
    /// be modified!
    /// </summary>
    public Array Items { get; }

    private BndArrConstNode(TypeManager tm, DType type, Type st, Type stItem, Array arr)
        : base(tm, type, st, arr)
    {
        Validation.Assert(type.IsSequence);
        Validation.AssertValue(stItem);
        Validation.AssertValue(arr);
        Validation.Assert(stItem.MakeArrayType().IsAssignableFrom(arr.GetType()));

        ItemSysType = stItem;
        Items = arr;
    }

    /// <summary>
    /// Wraps the given array and type manager as a <see cref="BndCmpConstNode"/>.
    /// </summary>
    public static BndArrConstNode Create(TypeManager tm, DType type, Array arr)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(type.IsSequence, nameof(type));
        Validation.BugCheckParam(tm.TryEnsureSysType(type, out Type stSeq), nameof(type));
        Validation.BugCheckParam(tm.TryEnsureSysType(type.ItemTypeOrThis, out Type stItem), nameof(type));

        var st = stItem.MakeArrayType();
        Validation.Assert(stSeq.IsAssignableFrom(st));
        Validation.BugCheckParam(arr != null && st.IsAssignableFrom(arr.GetType()), nameof(arr));

        return new BndArrConstNode(tm, type, st, stItem, arr);
    }

    /// <summary>
    /// Wraps the given array as a <see cref="BndCmpConstNode"/>, using the same type manager and
    /// <see cref="DType"/> as this node.
    /// </summary>
    public BndArrConstNode SetItems(Array arr)
    {
        if (arr == Items)
            return this;

        // REVIEW: Is there any expectation that the length of the new array should match
        // the previous?
        Validation.BugCheckParam(
            arr != null &&
            SysType.IsAssignableFrom(arr.GetType()) &&
            ItemSysType.IsAssignableFrom(arr.GetType().GetElementType()), nameof(arr));

        return new BndArrConstNode(TypeManager, Type, SysType, ItemSysType, arr);
    }

    public override BoundNode MorphRefType(DType type)
    {
        if (type == Type)
            return this;

        Validation.BugCheckParam(type.IsSequence && BndCastRefNode.AreValid(this, type), nameof(type));
        Validation.BugCheckParam(TypeManager.TryEnsureSysType(type, out Type stSeq), nameof(type));
        Validation.BugCheckParam(TypeManager.TryEnsureSysType(type.ItemTypeOrThis, out Type stItem), nameof(type));

        var st = stItem.MakeArrayType();
        Validation.Assert(stSeq.IsAssignableFrom(st));
        Validation.BugCheckParam(st.IsAssignableFrom(SysType), nameof(type));

        return new BndArrConstNode(TypeManager, type, st, stItem, Items);
    }

    public override (long, long) GetItemCountRange()
    {
        Validation.Assert(Type.IsSequence);
        var count = Items.Length;
        return (count, count);
    }
}

/// <summary>
/// Wraps a runtime record and type manager as a <see cref="BndCmpConstNode"/>.
/// </summary>
public sealed class BndRecConstNode : BndTmConstNode
{
    private BndRecConstNode(TypeManager tm, DType type, Type st, RecordBase rec)
        : base(tm, type, st, rec)
    {
        Validation.Assert(type.IsRecordReq);
        Validation.AssertValue(rec);
    }

    public static BndRecConstNode Create(TypeManager tm, DType type, RecordBase rec)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(type.IsRecordReq, nameof(type));
        Validation.BugCheckParam(tm.TryEnsureSysType(type, out Type st), nameof(type));
        Validation.BugCheckParam(rec != null && st.IsAssignableFrom(rec.GetType()), nameof(rec));

        return new BndRecConstNode(tm, type, st, rec);
    }

    public override BoundNode MorphRefType(DType type)
    {
        if (type == Type)
            return this;

        Validation.BugCheckParam(type.IsRecordReq && BndCastRefNode.AreValid(this, type), nameof(type));
        Validation.BugCheckParam(TypeManager.TryEnsureSysType(type, out Type st), nameof(type));
        return new BndRecConstNode(TypeManager, type, st, (RecordBase)Value);
    }
}

/// <summary>
/// Wraps a runtime tuple and type manager as a <see cref="BndCmpConstNode"/>.
/// </summary>
public sealed class BndTupConstNode : BndTmConstNode
{
    private BndTupConstNode(TypeManager tm, DType type, Type st, TupleBase tup)
        : base(tm, type, st, tup)
    {
        Validation.Assert(type.IsTupleReq);
        Validation.AssertValue(tup);
    }

    public static BndTupConstNode Create(TypeManager tm, DType type, TupleBase tup)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(type.IsTupleReq, nameof(type));
        Validation.BugCheckParam(tm.TryEnsureSysType(type, out Type st), nameof(type));
        Validation.BugCheckParam(tup != null && st.IsAssignableFrom(tup.GetType()), nameof(tup));

        return new BndTupConstNode(tm, type, st, tup);
    }

    public override BoundNode MorphRefType(DType type)
    {
        if (type == Type)
            return this;

        Validation.BugCheckParam(type.IsTupleReq && BndCastRefNode.AreValid(this, type), nameof(type));
        Validation.BugCheckParam(TypeManager.TryEnsureSysType(type, out Type st), nameof(type));
        return new BndTupConstNode(TypeManager, type, st, (TupleBase)Value);
    }
}

/// <summary>
/// Wraps a runtime tensor and type manager as a <see cref="BndCmpConstNode"/>.
/// </summary>
public sealed class BndTenConstNode : BndTmConstNode
{
    /// <summary>
    /// The tensor value.
    /// </summary>
    public Tensor Tensor { get; }

    private BndTenConstNode(TypeManager tm, DType type, Type st, Tensor ten)
        : base(tm, type, st, ten)
    {
        Validation.Assert(type.IsTensorReq);
        Validation.AssertValue(ten);
        Tensor = ten;
    }

    public static BndTenConstNode Create(TypeManager tm, DType type, Tensor ten)
    {
        Validation.BugCheckValue(tm, nameof(tm));
        Validation.BugCheckParam(type.IsTensorReq, nameof(type));
        Validation.BugCheckParam(tm.TryEnsureSysType(type, out Type st), nameof(type));
        Validation.BugCheckParam(ten != null && st.IsAssignableFrom(ten.GetType()), nameof(ten));

        return new BndTenConstNode(tm, type, st, ten);
    }

    public override BoundNode MorphRefType(DType type)
    {
        if (type == Type)
            return this;

        Validation.BugCheckParam(type.IsTensorReq && BndCastRefNode.AreValid(this, type), nameof(type));
        Validation.BugCheckParam(TypeManager.TryEnsureSysType(type, out Type st), nameof(type));
        return new BndTenConstNode(TypeManager, type, st, Tensor);
    }
}
