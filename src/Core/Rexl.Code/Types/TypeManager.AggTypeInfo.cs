// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Reflection;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using FinTuple = Immutable.Array<FieldInfo>;

// This partial is for aggregate type info functionality.
public abstract partial class TypeManager
{
    /// <summary>
    /// System type information for an aggregate type, such as a record or tuple type. In particular, this
    /// holds the relevant <see cref="FieldInfo"/> and <see cref="ConstructorInfo"/> values. Note that the
    /// standard lookup methods on <see cref="Type"/> are O(N) where N is the number of members, so this
    /// caching is critical for performance (to avoid O(N^2) when processing all fields).
    /// It also holds equality comparers, when the type is equatable. There are separate comparers for case
    /// sensitive and insensitive. The latter is null when there are no text values.
    /// </summary>
    protected abstract class AggSysTypeInfo
    {
        /// <summary>
        /// The system type.
        /// </summary>
        public Type SysType => EqCmpInfo.StItem;

        /// <summary>
        /// Whether this agg type is equatable.
        /// </summary>
        public bool IsEquatable => EqCmpInfo.Eq is not null;

        /// <summary>
        /// The equality comparer information. This also includes the agg system type. If the agg type isn't
        /// equatable, everything except the agg system type will be <c>null</c>.
        /// </summary>
        public readonly EqCmpInfo EqCmpInfo;

        /// <summary>
        /// The <see cref="FieldInfo"/>s for the "group" fields. When there are some of these, each
        /// corresponds to eight logical fields.
        /// </summary>
        public readonly FinTuple GroupFields;

        /// <summary>
        /// The number of "values" in the record/tuple DType. Each one has a corresponding field
        /// and perhaps other information, depending on the kind of type.
        /// </summary>
        public int ValueCount => ValueFields.Length;

        /// <summary>
        /// The <see cref="FieldInfo"/>s for the "value" fields.
        /// </summary>
        public readonly FinTuple ValueFields;

        /// <summary>
        /// The zero-arg constructor.
        /// </summary>
        public readonly ConstructorInfo Ctor;

        /// <summary>
        /// The cached Equals2 static method info.
        /// This is <c>null</c> if the type isn't equatable.
        /// </summary>
        public readonly MethodInfo Equals2;

        private protected AggSysTypeInfo(TypeManager tm, Type stAgg, Type[] stsVal,
            bool hasFlags, bool eqable, bool needCi)
        {
            Validation.AssertValue(tm);
            Validation.AssertValue(stAgg);
            Validation.AssertValue(stsVal);

            int cval = stsVal.Length;
            IEqualityComparer eqCi = null;
            IEqualityComparer eqTi = null;
            IEqualityComparer eqTiCi = null;
            if (cval <= 0)
            {
                Validation.Assert(eqable);
                Validation.Assert(!needCi);
                GroupFields = FinTuple.Empty;
                ValueFields = FinTuple.Empty;
                Ctor = stAgg.GetConstructor(Type.EmptyTypes).VerifyValue();
                Equals2 = stAgg.GetMethod("Equals2", BindingFlags.Public | BindingFlags.Static,
                    new Type[] { stAgg, stAgg }).VerifyValue();
                eqTi = GetStrictEqCmpRef(stAgg);
            }
            else
            {
                int cgrp = hasFlags ? (cval + 7) >> 3 : 0;
                var bldrGrp = hasFlags ? FinTuple.CreateBuilder(cgrp, init: true) : null;
                var bldrVal = FinTuple.CreateBuilder(cval, init: true);
                int cg = 0;
                int cv = 0;

                // Note that GetField and GetConstructor are O(N), so to avoid O(N^2), we scan
                // through all the members stashing what we need.
                foreach (var min in stAgg.GetMembers())
                {
                    if (min is FieldInfo fin)
                    {
                        int ind;
                        if ((ind = tm.FieldNameToSlot(fin.Name)) >= 0)
                        {
                            Validation.AssertIndex(ind, cval);
                            Validation.Assert(bldrVal[ind] == null);
                            bldrVal[ind] = fin;
                            cv++;
                        }
                        else if (hasFlags && (ind = tm.FieldNameToBitGrp(fin.Name)) >= 0)
                        {
                            Validation.AssertIndex(ind, cgrp);
                            Validation.Assert(bldrGrp[ind] == null);
                            Validation.Assert(fin.FieldType == typeof(byte));
                            bldrGrp[ind] = fin;
                            cg++;
                        }
                    }
                    else if (min is ConstructorInfo ctor)
                    {
                        if (ctor.GetParameters().Length == 0)
                        {
                            Validation.Assert(Ctor == null);
                            Ctor = ctor;
                        }
                    }
                    else if (min is MethodInfo meth)
                    {
                        if (eqable && meth.Name == "Equals2" && meth.IsStatic && meth.ReturnType == typeof(bool))
                        {
                            var parms = meth.GetParameters();
                            if (parms.Length == 2 &&
                                parms[0].ParameterType == stAgg &&
                                parms[1].ParameterType == stAgg)
                            {
                                Validation.Assert(Equals2 == null);
                                Equals2 = meth;
                            }
                        }
                    }
                }
                Validation.BugCheck(Ctor is not null);
                Validation.BugCheck((Equals2 is not null) == eqable);
                Validation.BugCheck(cg == cgrp);
                Validation.BugCheck(cv == cval);
                GroupFields = hasFlags ? bldrGrp.ToImmutable() : FinTuple.Empty;
                ValueFields = bldrVal.ToImmutable();

                // Get the special equality comparers, if needed.
                if (eqable)
                {
                    tm.CreateSpecialEqCmpsForAgg(stAgg, ValueFields, out eqTi, out eqCi, out eqTiCi);
                    Validation.Assert(eqTi is not null);
                    Validation.Assert(needCi == (eqCi is not null));
                    Validation.Assert(needCi == (eqTiCi is not null));
                }
            }

            if (!eqable)
                EqCmpInfo = EqCmpInfo.CreateNon(stAgg);
            else
                EqCmpInfo = EqCmpInfo.CreateStd(stAgg, eqTi, eqCi, eqTiCi);
        }
    }

    /// <summary>
    /// Encapsulates sytem type information for a record type. In particular, this holds
    /// the relevant <see cref="FieldInfo"/> and <see cref="ConstructorInfo"/> values.
    /// Note that the standard lookup methods on <see cref="Type"/> are O(N) where N is
    /// the number of members, so this caching is critical for performance (to avoid O(N^2)
    /// when processing all fields).
    /// </summary>
    protected sealed class RecordSysTypeInfo : AggSysTypeInfo
    {
        public RecordSysTypeInfo(TypeManager tm, Type stRec, Type[] sts, bool eqable, bool needCi)
            : base(tm, stRec, sts, hasFlags: true, eqable, needCi)
        {
            Validation.Assert(stRec.IsSubclassOf(typeof(RecordBase)));
        }
    }

    /// <summary>
    /// Encapsulates sytem type information for a tuple type. In particular, this holds
    /// the relevant <see cref="FieldInfo"/> and <see cref="ConstructorInfo"/> values.
    /// Note that the standard lookup methods on <see cref="Type"/> are O(N) where N is
    /// the number of members, so this caching is critical for performance (to avoid O(N^2)
    /// when processing all fields).
    /// </summary>
    protected sealed class TupleSysTypeInfo : AggSysTypeInfo
    {
        public TupleSysTypeInfo(TypeManager tm, Type stTup, Type[] sts, bool eqable, bool needCi)
            : base(tm, stTup, sts, hasFlags: false, eqable, needCi)
        {
            Validation.Assert(stTup.IsSubclassOf(typeof(TupleBase)));
        }
    }

    /// <summary>
    /// Represents the runtime type information for a record instance. This does NOT include indication of
    /// which fields are null. That is included in the record instance fields.
    /// </summary>
    protected sealed class RrtiImpl : RecordRuntimeTypeInfo
    {
        public readonly RecordSysTypeInfo Rsti;

        public override Type RecSysType => Rsti.SysType;

        public RrtiImpl(RecordSysTypeInfo rsti, DType type)
            : base(type)
        {
            Validation.AssertValue(rsti);
            Rsti = rsti;
        }
    }
}
