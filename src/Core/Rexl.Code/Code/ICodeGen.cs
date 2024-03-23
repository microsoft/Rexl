// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using Microsoft.Rexl.Bind;

namespace Microsoft.Rexl.Code;

/// <summary>
/// The code generator interface, passed to a <see cref="RexlOperationGenerator"/> to generate
/// code for a <see cref="BndCallNode"/>.
/// </summary>
public interface ICodeGen
{
    // REVIEW: Consider making the Gen... methods for this return ICodeGen to allow chaining.
    // This would be most useful if coupled with a change to put ILWriter's chaining methods behind an
    // interface and have ICodeGen extend that interface, passing those calls through to Generator.Il.
    // Most of the Gen methods here aren't used sequentially without calling into Generator.Il in
    // between, so there's limited benefit from allowing chaining unless unified with ILWriter.

    /// <summary>
    /// Gets the current <see cref="ILWriter"/>.
    /// </summary>
    ILWriter Writer => Generator.Il;

    /// <summary>
    /// Gets the active method generator.
    /// </summary>
    MethodGenerator Generator { get; }

    /// <summary>
    /// Acquire a local of the given type. Note that <see cref="Local"/> is disposable. Generally, it should
    /// be disposed asap so the local can be "shared" when multiple locals of the same type are needed, in a
    /// non-nested way.
    /// </summary>
    MethodGenerator.Local AcquireLocal(Type st) => Generator.AcquireLocal(st);

    /// <summary>
    /// Generate IL to load the indicated field. The record object should be on the execution stack.
    /// </summary>
    void GenLoadField(DType typeRec, Type stRec, DName nameFld, DType typeFld)
    {
        TypeManager.GenLoadField(Generator, typeRec, stRec, nameFld, typeFld);
    }

    /// <summary>
    /// Create a record generator. Note that <see cref="TypeManager.RecordGenerator"/> is disposable.
    /// </summary>
    TypeManager.RecordGenerator CreateRecordGenerator(DType typeRec);

    /// <summary>
    /// Generate IL to create a tuple of the indicated type.
    /// </summary>
    void GenCreateTuple(DType typeTup, Type stTup)
    {
        TypeManager.GenCreateTuple(Generator, typeTup, stTup);
    }

    /// <summary>
    /// Generate IL to store into a slot of a tuple. The tuple and the value to store should be the
    /// top two values on the execution stack.
    /// </summary>
    void GenStoreSlot(DType typeTup, Type stTup, int slot, DType typeFld)
    {
        TypeManager.GenStoreSlot(Generator, typeTup, stTup, slot, typeFld);
    }

    /// <summary>
    /// Returns the system type for the given <see cref="DType"/>. Throws if there isn't one. If you'd rather
    /// not throw, then use <see cref="TypeManager.TryEnsureSysType(DType, out Type)"/>.
    /// </summary>
    Type GetSystemType(DType type);

    /// <summary>
    /// Gets the active type manager.
    /// </summary>
    TypeManager TypeManager { get; }

    /// <summary>
    /// Gets the active code gen host. Never null.
    /// </summary>
    CodeGenHost Host { get; }

    /// <summary>
    /// Generate the code for the given bound node and return the system type of
    /// the value that was "pushed" on the stack.
    /// </summary>
    Type GenCode(BoundNode bnd, ref int cur);

    /// <summary>
    /// Loads the default value of the given system type. Assumes that <paramref name="st"/> is a value type,
    /// for which that is a reasonable thing to do.
    /// </summary>
    void GenDefValType(Type st);

    /// <summary>
    /// Loads the Rexl default value of the type onto the stack.
    /// Note that this may not be the same as the C# default value.
    /// <seealso cref="TypeManager.TryEnsureDefaultValue(DType, out (object value, bool special))"/>
    /// </summary>
    void GenLoadDefault(DType type);

    /// <summary>
    /// Given the source system type and the system type of the sequence item, generate code to wrap the source
    /// as the proper sequence type.
    /// </summary>
    Type GenSequenceWrap(Type stSrc, Type stItem);

    /// <summary>
    /// Stores the given value as a constant object and generates code to load it onto the evaluation stack.
    /// Note that the value should be immutable!
    /// </summary>
    void GenLoadConst<T>(T value) where T : class;

    /// <summary>
    /// Stores the given value as a constant object and generates code to load it onto the evaluation stack.
    /// The given <paramref name="st"/> should be the type of the <paramref name="value"/>.
    /// </summary>
    void GenLoadConst(object value, Type st);

    /// <summary>
    /// Load an equality comparer for the given equatable type. This BugChecks that
    /// <paramref name="type"/> is equatable. If the equality comparer would be the standard
    /// <c>EqualityComparer{T}.Default</c>, then loads <c>null</c> instead and returns false.
    /// </summary>
    bool GenLoadEqCmpOrNull(DType type, bool ti, bool ci, out Type stEq, out Type stAgg);

    /// <summary>
    /// Generates code to load the action host. This is only available when generating code for an
    /// action/procedure.
    /// </summary>
    void GenLoadActionHost();

    /// <summary>
    /// Allocates an ID range for the given bound node, then generates code to load the
    /// execution context and minimum generated ID. Note only the minimum ID needs to be
    /// loaded since the IDs are contiguous.
    /// This is typically used to load part of the argument list to an <c>Exec</c> function
    /// that invokes the execution context's methods.
    /// </summary>
    void GenLoadExecCtxAndId(BoundNode bnd, int count = 1)
    {
        int id = EnsureIdRange(bnd, count);
        GenLoadExecCtx();
        Writer.Ldc_I4(id);
    }

    /// <summary>
    /// Generates code to load the execution context.
    /// </summary>
    void GenLoadExecCtx();

    /// <summary>
    /// Ensures the given bound node has a corresponding range of <paramref name="count"/>
    /// contiguous IDs. Returns the minimum ID in the range.
    /// </summary>
    int EnsureIdRange(BoundNode bnd, int count);

    /// <summary>
    /// Allocate a new execution context cache slot.
    /// </summary>
    int AllocCacheSlot();

    /// <summary>
    /// Start a function. Must be balanced with a call to EndFunction. The return value
    /// should be passed to EndFunction as the "cookie". Calling this alters the current
    /// <see cref="Generator"/>. Note that the function can use <see cref="GenLoadConst(object, Type)"/>
    /// but cannot use <see cref="GenLoadExecCtx"/>.
    /// REVIEW: Do we need support for execution context?
    /// </summary>
    int StartFunction(string name, Type stRet, params Type[] stsParams);

    /// <summary>
    /// Start a function. Must be balanced with a call to EndFunction. The return value
    /// should be passed to EndFunction as the "cookie". Calling this alters the current
    /// <see cref="Generator"/>. Note that the function can use <see cref="GenLoadConst(object, Type)"/>
    /// but cannot use <see cref="GenLoadExecCtx"/>.
    /// REVIEW: Do we need support for execution context?
    /// </summary>
    int StartFunction(string name, Type stRet, ReadOnly.Array<Type> stsParams);

    /// <summary>
    /// Balances a call to StartFunction. Returns the delegate type and delegate value.
    /// </summary>
    (Type, Delegate) EndFunction(int cookie);
}
