// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This allows easily turning off most of the logging code, in case we'd rather ship without it.
#define LOG_IL

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Code;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// This wraps an index to either an arg or local, together with the (system) type. The type
/// is primarily for asserts. A negative index means an arg. Note that this cannot represent
/// arg 0, which is typically a special "this" arg, so this limitation shouldn't cause issues.
/// </summary>
public struct LocArgInfo
{
    /// <summary>
    /// Whether this is "active".
    /// </summary>
    public readonly bool IsActive => SysType != null;

    /// <summary>
    /// The index, non-negative for local, negative for argument. Note that this cannot
    /// represent argument zero, which is assumed to be "special" and doesn't need to
    /// be represented.
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// The system type of the local or argument.
    /// </summary>
    public readonly Type SysType;

    /// <summary>
    /// Whether this is a local rather than an argument.
    /// </summary>
    public bool IsLoc => Index >= 0;

    private LocArgInfo(int index, Type st)
    {
        Validation.Assert(Math.Abs(index) <= short.MaxValue);
        Validation.AssertValue(st);
        Index = index;
        SysType = st;
    }

    /// <summary>
    /// Create an instance from an argument index and type.
    /// </summary>
    public static LocArgInfo FromArg(int index, Type st)
    {
        Validation.BugCheckParam(0 < index && index <= short.MaxValue, nameof(index));
        Validation.BugCheckValue(st, nameof(st));
        return new LocArgInfo(-index, st);
    }

    /// <summary>
    /// Create an instance from a <see cref="LocalBuilder"/>.
    /// </summary>
    public LocArgInfo(LocalBuilder loc)
    {
        Validation.BugCheckValue(loc, nameof(loc));
        Validation.AssertValue(loc.LocalType);
        Index = loc.LocalIndex;
        SysType = loc.LocalType;
    }

    /// <summary>
    /// Implicit conversion from a <see cref="LocalBuilder"/>.
    /// </summary>
    public static implicit operator LocArgInfo(LocalBuilder loc) => new LocArgInfo(loc);
}

/// <summary>
/// The kind of IL logging to perform.
/// </summary>
public enum ILLogKind
{
    /// <summary>
    /// No logging.
    /// </summary>
    None,

    /// <summary>
    /// Log with each instruction prefixed with its position as a byte index.
    /// </summary>
    Position,

    /// <summary>
    /// Log with each instruction prefixed with its size in bytes.
    /// </summary>
    Size
}

/// <summary>
/// A wrapper around an ILGenerator. The methods support chaining so, rather than typing something like:
///   ilw.Emit(OpCodes.Ldarg_0);
///   ilw.Emit(OpCodes.Ldarg_1);
///   ilw.Emit(OpCodes.Ldc_I4, i);
///   ilw.Emit(OpCodes.Ldelem_Ref);
///   ilw.Emit(OpCodes.Stfld, literalFields[i]);
/// You can do:
///   ilw
///       .Ldarg(0)
///       .Ldarg(1)
///       .Ldc_I4(i)
///       .Ldelem_Ref()
///       .Stfld(literalFields[i]);
/// It also provides type safety over the Emit methods by ensuring that you don't pass any args when
/// using Add or that you only pass a Label when using Br, etc.
/// Additionally, it handles logging the generated code.
/// </summary>
public sealed class ILWriter
{
    /// <summary>
    /// The low-level il generator. This is not exposed publicly.
    /// </summary>
    private readonly ILGenerator _ilg;

    /// <summary>
    /// The argument types, mostly for asserts.
    /// </summary>
    private readonly ReadOnly.Array<Type> _argTypes;

    /// <summary>
    /// The allocated locals. Generally, the domain of this should be contiguous, but the
    /// index is assigned by the <see cref="_ilg"/>, so we don't want to bake that assumption
    /// into this data structure.
    /// This is mostly for asserts.
    /// REVIEW: Should this be DEBUG only?
    /// </summary>
    private Dictionary<int, LocalBuilder> _locMap;

    /// <summary>
    /// The maximum local index allocated.
    /// This is mostly for asserts.
    /// </summary>
    private int _ilocMax;

    /// <summary>
    /// The set of labels actually referenced.
    /// </summary>
    private HashSet<Label> _labelsUsed;

#if LOG_IL
    /// <summary>
    /// The lines generated when logging.
    /// </summary>
    private readonly List<string> _lines;

    /// <summary>
    /// Used to build a line.
    /// </summary>
    private readonly SbSysTypeSink _sink;

    /// <summary>
    /// Whether the prefix to a log line should include the size of the instruction, rather than its offset.
    /// </summary>
    private bool _logSize;

    /// <summary>
    /// The current position in the generated IL. This is used to compute the <see cref="_sizeLast"/>.
    /// </summary>
    private int _pos;

    /// <summary>
    /// The size of the last IL instruction logged.
    /// </summary>
    private int _sizeLast;
#endif

    /// <summary>
    /// Gets the current position within the generated il stream. Typically just used to
    /// assert that things have or haven't changed, as expected.
    /// </summary>
    public int Position { get { return _ilg.ILOffset; } }

    /// <summary>
    /// The number of arguments.
    /// </summary>
    public int ArgCount => _argTypes.Length;

    /// <summary>
    /// The number of locals allocated (so far).
    /// </summary>
    public int LocCount => _ilocMax + 1;

    internal ILWriter(ReadOnly.Array<Type> argTypes, ILGenerator ilg, ILLogKind kind = ILLogKind.None)
    {
        Validation.AssertValue(ilg);
        _argTypes = argTypes;
        _ilocMax = -1;
        _ilg = ilg;
        _labelsUsed = null;
#if LOG_IL
        if (kind != ILLogKind.None)
        {
            _lines = new List<string>();
            _sink = new SbSysTypeSink();
            _logSize = kind == ILLogKind.Size;
        }
#endif
        // We want default(Label) to be "invalid". so we always create the first label and
        // set it to the beginning of the method.
        var labBad = _ilg.DefineLabel();
        Validation.Assert(labBad == default);
    }

    [Conditional("DEBUG")]
    private void AssertArg(int index, Type st = null)
    {
#if DEBUG
        Validation.AssertIndex(index, _argTypes.Length);
        Validation.Assert(st == _argTypes[index] || st == null);
#endif
    }

    [Conditional("DEBUG")]
    private void AssertLoc(int index, Type st = null)
    {
#if DEBUG
        Validation.Assert(_locMap != null);
        _locMap.TryGetValue(index, out var bldr).Verify();
        Validation.Assert(bldr.LocalType == st || st == null);
#endif
    }

    [Conditional("DEBUG")]
    private void AssertLoc(LocalBuilder loc)
    {
#if DEBUG
        Validation.AssertValue(loc);
        Validation.Assert(_locMap != null);
        _locMap.TryGetValue(loc.LocalIndex, out var bldr).Verify();
        Validation.Assert(bldr == loc);
#endif
    }

    [Conditional("DEBUG")]
    private void AssertLocArg(in LocArgInfo lai)
    {
#if DEBUG
        if (lai.Index >= 0)
        {
            Validation.Assert(_locMap != null);
            _locMap.TryGetValue(lai.Index, out var bldr).Verify();
            Validation.Assert(bldr.LocalIndex == lai.Index);
            Validation.Assert(bldr.LocalType == lai.SysType);
        }
        else
        {
            Validation.AssertIndex(-lai.Index, _argTypes.Length);
            Validation.Assert(_argTypes[-lai.Index] == lai.SysType);
        }
#endif
    }

    public Immutable.Array<string> ResetLines()
    {
#if LOG_IL
        if (_lines != null)
        {
            if (_logSize)
                _lines.Add(string.Format("Total Size: {0}", Position));
            var res = _lines.ToImmutableArray();
            _lines.Clear();
            return res;
        }
#endif
        return Immutable.Array<string>.Empty;
    }

#if LOG_IL
    private SbSysTypeSink StartLine()
    {
        Validation.Assert(_lines != null);
        _sink.Builder.Clear();
        return _sink;
    }

    private void AddLine(TextSink sink)
    {
        Validation.Assert(_lines != null);
        Validation.Assert(sink == _sink);
        _lines.Add(_sink.Builder.ToString());
    }

    /// <summary>
    /// Returns numeric value to prefix logged IL instructions with.
    /// This allows us to change easily, eg, the instruction offset,
    /// or the size (in bytes) of the instruction.
    /// </summary>
    private int LogPrefix()
    {
        int cur = _ilg.ILOffset;
        Validation.Assert(cur >= _pos);
        if (cur > _pos)
        {
            _sizeLast = cur - _pos;
            _pos = cur;
        }
        // Returns either instruction size or position, based on the _logSize setting.
        return _logSize ? _sizeLast : _pos - _sizeLast;
    }
#endif

    /// <summary>
    /// Get the type of the local with the given index.
    /// </summary>
    private Type GetLocType(int index)
    {
        Validation.AssertIndexInclusive(index, _ilocMax);
        if (Util.TryGetValue(_locMap, index, out var bldr))
            return bldr.LocalType;
        // If we get here, why/how?
        Validation.Assert(false);
        return null;
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op)
    {
#if LOG_IL
        if (_lines != null)
            AddLine(StartLine().TWrite("{0,5}) {1}", LogPrefix(), op));
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, object arg)
    {
#if LOG_IL
        if (_lines != null)
            AddLine(StartLine().TWrite("{0,5}) {1} [{2}]", LogPrefix(), op, arg));
#endif
    }

    [Conditional("LOG_IL")]
    private void Log<T>(OpCode op, T arg)
        where T: struct
    {
#if LOG_IL
        if (_lines != null)
            AddLine(StartLine().TWrite("{0,5}) {1} [{2}]", LogPrefix(), op, arg));
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, float arg)
    {
#if LOG_IL
        if (_lines != null)
            AddLine(StartLine().TWrite("{0,5}) {1} [{2}]", LogPrefix(), op, arg.ToStr()));
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, double arg)
    {
#if LOG_IL
        if (_lines != null)
            AddLine(StartLine().TWrite("{0,5}) {1} [{2}]", LogPrefix(), op, arg.ToStr()));
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, int arg, Type st)
    {
#if LOG_IL
        if (_lines != null)
        {
            var sink = StartLine().TWrite("{0,5}) {1} [", LogPrefix(), op);
            if (st != null)
                sink.TWriteRawType(st).TWrite(" (").TWrite(arg).Write(")]");
            else
                sink.TWrite(arg).Write(']');
            AddLine(sink);
        }
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, Type arg)
    {
#if LOG_IL
        if (_lines != null)
        {
            var sink = StartLine().TWrite("{0,5}) {1}", LogPrefix(), op);
            if (arg != null)
                sink.TWrite(" [").TWriteRawType(arg).TWrite(']');
            AddLine(sink);
        }
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, LocalBuilder arg)
    {
        // REVIEW: Should do something better.
        Log(op, (object)arg);
    }

    // REVIEW: FieldOnTypeBuilderInstantiation.FieldType throws! The CLR should really fix this.
    private Type GetFieldType(FieldInfo fin)
    {
        Validation.AssertValue(fin);
        try
        {
            return fin.FieldType;
        }
        catch (NotImplementedException)
        {
            // There is no good way to get the underlying field other than by name!
            var stDec = fin.DeclaringType;
            var stGen = stDec.GetGenericTypeDefinition();
            var finGen = stGen.GetField(fin.Name);
            var stFin = finGen.FieldType;
            // REVIEW: Instantiate it.
            return stFin;
        }
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, MemberInfo arg)
    {
#if LOG_IL
        if (_lines != null)
        {
            var sink = StartLine().TWrite("{0,5}) {1} ", LogPrefix(), op);
            if (arg is MethodBase mb)
            {
                // Method or constructor.
                var meth = mb as MethodInfo;
                Validation.Assert(!mb.IsStatic || meth != null);
                if (meth != null && meth.IsStatic)
                    sink.Write("static ");
                sink.WriteRawType(mb.DeclaringType);
                if (meth != null)
                    sink.TWrite("::").Write(meth.Name);
                sink.Write('(');
                var pre = "";
                foreach (var pi in mb.GetParameters())
                {
                    sink.Write(pre);
                    pre = ", ";
                    var t = pi.ParameterType;
                    if (t.IsByRef)
                    {
                        sink.Write(pi.IsOut ? "out " : "ref ");
                        t = t.GetElementType();
                    }
                    sink.WriteRawType(t);
                }
                sink.Write(')');
                if (meth != null)
                    sink.TWrite(':').WriteRawType(meth.ReturnType);
            }
            else if (arg is FieldInfo fld)
            {
                if (fld.IsStatic)
                    sink.Write("static ");
                sink.TWriteRawType(fld.DeclaringType).TWrite("::").Write(fld.Name);
                sink.TWrite(':').WriteRawType(GetFieldType(fld));
            }
            else
                sink.Write("Member:[{0}] Type:[{1}] Sig:[{2}]", arg.Name, arg.DeclaringType?.Name, arg);

            AddLine(sink);
        }
#endif
    }

    [Conditional("LOG_IL")]
    private void LogMark(Label arg)
    {
#if LOG_IL
        if (_lines != null)
            AddLine(StartLine().TWrite("Label [{0}]:", arg.GetHashCode() - 1));
#endif
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, Label arg)
    {
        Log(op, arg.GetHashCode() - 1);
    }

    [Conditional("LOG_IL")]
    private void Log(OpCode op, Label[] arg)
    {
        // REVIEW: Should do something better, if we ever use switch.
        Log(op, (object)arg);
    }

    private void Emit(OpCode op, Type arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, string arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, float arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, MethodInfo arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, FieldInfo arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, Label[] arg)
    {
        foreach (var lab in arg)
        {
            Validation.Assert(lab != default);
            Util.Add(ref _labelsUsed, lab);
        }

        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, LocalBuilder arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, ConstructorInfo arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, long arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, int arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, short arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, byte arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, sbyte arg)
    {
        // NOTE: We must use ILGenerator.Emit(OpCode, byte) even though the value is signed.
        // Using ILGenerator.Emit(OpCode, sbyte) results in an invalid program exception.
        _ilg.Emit(op, (byte)arg);
        Log(op, arg);
    }

    private void Emit(OpCode op, double arg)
    {
        _ilg.Emit(op, arg);
        Log(op, arg);
    }

    /// <summary>
    /// Emit the given <see cref="OpCode"/> with no args.
    /// </summary>
    public void Emit(OpCode op)
    {
        _ilg.Emit(op);
        Log(op);
    }

    private void Emit(OpCode op, Label label)
    {
        Validation.Assert(label != default);
        Util.Add(ref _labelsUsed, label);

        _ilg.Emit(op, label);
        Log(op, label);
    }

    /// <summary>
    /// This is a low level api for declaring a local. Most code should use the MethodGenerator's
    /// AcquireLocal and AcquireRefLocal instead.
    /// </summary>
    public LocalBuilder DeclareLocalRaw(Type st)
    {
        var loc = _ilg.DeclareLocal(st);
        int index = loc.LocalIndex;
        Validation.Assert(index > _ilocMax);
        // If this assert evers go off, why?
        Validation.Assert(index == _ilocMax + 1);
        Validation.Assert(!Util.ContainsKey(_locMap, index));
        Util.Add(ref _locMap, index, loc);
        if (_ilocMax < index)
            _ilocMax = index;
        AssertLoc(loc);
        return loc;
    }

    public Label DefineLabel()
    {
        return _ilg.DefineLabel();
    }

    public Label Ensure(ref Label label)
    {
        if (label == default)
            label = DefineLabel();
        return label;
    }

    public ILWriter MarkLabel(Label label)
    {
        Validation.Assert(label != default);

        _ilg.MarkLabel(label);
        LogMark(label);
        return this;
    }

    public ILWriter MarkLabelIfUsed(Label label)
    {
        if (Util.Contains(_labelsUsed, label))
        {
            Validation.Assert(label != default);
            _ilg.MarkLabel(label);
            LogMark(label);
        }
        return this;
    }

    public bool IsLabelUsed(Label label)
    {
        return Util.Contains(_labelsUsed, label);
    }

    public ILWriter Add()
    {
        Emit(OpCodes.Add);
        return this;
    }

    public ILWriter Add_Ovf()
    {
        Emit(OpCodes.Add_Ovf);
        return this;
    }

    public ILWriter Add_Ovf_Un()
    {
        Emit(OpCodes.Add_Ovf_Un);
        return this;
    }

    /// <summary>
    /// Bitwise and.
    /// </summary>
    public ILWriter And()
    {
        Emit(OpCodes.And);
        return this;
    }

    public ILWriter Arglist()
    {
        Emit(OpCodes.Arglist);
        return this;
    }

    public ILWriter Beq(ref Label label)
    {
        Emit(OpCodes.Beq, Ensure(ref label));
        return this;
    }

    public ILWriter Beq_Non(Label label)
    {
        Emit(OpCodes.Beq, label);
        return this;
    }

    public ILWriter Bge(ref Label label)
    {
        Emit(OpCodes.Bge, Ensure(ref label));
        return this;
    }

    public ILWriter Bge_Non(Label label)
    {
        Emit(OpCodes.Bge, label);
        return this;
    }

    public ILWriter Bge_Un(ref Label label)
    {
        Emit(OpCodes.Bge_Un, Ensure(ref label));
        return this;
    }

    public ILWriter Bge_Un_Non(Label label)
    {
        Emit(OpCodes.Bge_Un, label);
        return this;
    }

    public ILWriter Bgt(ref Label label)
    {
        Emit(OpCodes.Bgt, Ensure(ref label));
        return this;
    }

    public ILWriter Bgt_Non(Label label)
    {
        Emit(OpCodes.Bgt, label);
        return this;
    }

    public ILWriter Bgt_Un(ref Label label)
    {
        Emit(OpCodes.Bgt_Un, Ensure(ref label));
        return this;
    }

    public ILWriter Bgt_Un_Non(Label label)
    {
        Emit(OpCodes.Bgt_Un, label);
        return this;
    }

    public ILWriter Ble(ref Label label)
    {
        Emit(OpCodes.Ble, Ensure(ref label));
        return this;
    }

    public ILWriter Ble_Non(Label label)
    {
        Emit(OpCodes.Ble, label);
        return this;
    }

    public ILWriter Ble_Un(ref Label label)
    {
        Emit(OpCodes.Ble_Un, Ensure(ref label));
        return this;
    }

    public ILWriter Ble_Un_Non(Label label)
    {
        Emit(OpCodes.Ble_Un, label);
        return this;
    }

    public ILWriter Blt(ref Label label)
    {
        Emit(OpCodes.Blt, Ensure(ref label));
        return this;
    }

    public ILWriter Blt_Non(Label label)
    {
        Emit(OpCodes.Blt, label);
        return this;
    }

    public ILWriter Blt_Un(ref Label label)
    {
        Emit(OpCodes.Blt_Un, Ensure(ref label));
        return this;
    }

    public ILWriter Blt_Un_Non(Label label)
    {
        Emit(OpCodes.Blt_Un, label);
        return this;
    }

    public ILWriter Bne_Un(ref Label label)
    {
        Emit(OpCodes.Bne_Un, Ensure(ref label));
        return this;
    }

    public ILWriter Bne_Un_Non(Label label)
    {
        Emit(OpCodes.Bne_Un, label);
        return this;
    }

    public ILWriter Box(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Box, type);
        return this;
    }

    /// <summary>
    /// Emit a box instruction if <paramref name="type"/> is a value type.
    /// </summary>
    public ILWriter BoxOpt(Type type)
    {
        Validation.AssertValue(type);

        // Only emit the opcode if type is a value type.
        if (type.IsValueType)
            Emit(OpCodes.Box, type);

        return this;
    }

    public ILWriter Br(ref Label label)
    {
        Emit(OpCodes.Br, Ensure(ref label));
        return this;
    }

    public ILWriter Br_Non(Label label)
    {
        Emit(OpCodes.Br, label);
        return this;
    }

    public ILWriter Break()
    {
        Emit(OpCodes.Break);
        return this;
    }

    public ILWriter Brfalse(ref Label label)
    {
        Emit(OpCodes.Brfalse, Ensure(ref label));
        return this;
    }

    public ILWriter Brfalse_Non(Label label)
    {
        Emit(OpCodes.Brfalse, label);
        return this;
    }

    public ILWriter Brtrue(ref Label label)
    {
        Emit(OpCodes.Brtrue, Ensure(ref label));
        return this;
    }

    public ILWriter Brtrue_Non(Label label)
    {
        Emit(OpCodes.Brtrue, label);
        return this;
    }

    public ILWriter Call(ConstructorInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Call, info);
        return this;
    }

    public ILWriter Call(MethodInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(!info.IsVirtual);
        Emit(OpCodes.Call, info);
        return this;
    }

    public ILWriter CallVirtAsNonVirt(MethodInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(info.IsVirtual);
        Emit(OpCodes.Call, info);
        return this;
    }

    public ILWriter Callvirt(MethodInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(!info.IsStatic);
        Emit(OpCodes.Callvirt, info);
        return this;
    }

    public ILWriter Castclass(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Castclass, type);
        return this;
    }

    public ILWriter Ceq()
    {
        Emit(OpCodes.Ceq);
        return this;
    }

    public ILWriter Cgt()
    {
        Emit(OpCodes.Cgt);
        return this;
    }

    public ILWriter Cgt_Un()
    {
        Emit(OpCodes.Cgt_Un);
        return this;
    }

    public ILWriter Ckfinite()
    {
        Emit(OpCodes.Ckfinite);
        return this;
    }

    public ILWriter Clt()
    {
        Emit(OpCodes.Clt);
        return this;
    }

    public ILWriter Clt_Un()
    {
        Emit(OpCodes.Clt_Un);
        return this;
    }

    public ILWriter Constrained(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Constrained, type);
        return this;
    }

    public ILWriter Conv_XX(int cb, bool uns)
    {
        switch (cb)
        {
        case 1: Emit(uns ? OpCodes.Conv_U1 : OpCodes.Conv_I1); break;
        case 2: Emit(uns ? OpCodes.Conv_U2 : OpCodes.Conv_I2); break;
        case 4: Emit(uns ? OpCodes.Conv_U4 : OpCodes.Conv_I4); break;
        case 8: Emit(uns ? OpCodes.Conv_U8 : OpCodes.Conv_I8); break;
        default:
            Validation.Assert(false);
            break;
        }
        return this;
    }

    public ILWriter Conv_I()
    {
        Emit(OpCodes.Conv_I);
        return this;
    }

    public ILWriter Conv_I1()
    {
        Emit(OpCodes.Conv_I1);
        return this;
    }

    public ILWriter Conv_I2()
    {
        Emit(OpCodes.Conv_I2);
        return this;
    }

    public ILWriter Conv_I4()
    {
        Emit(OpCodes.Conv_I4);
        return this;
    }

    public ILWriter Conv_I8()
    {
        Emit(OpCodes.Conv_I8);
        return this;
    }

    public ILWriter Conv_Ovf_I()
    {
        Emit(OpCodes.Conv_Ovf_I);
        return this;
    }

    public ILWriter Conv_Ovf_I_Un()
    {
        Emit(OpCodes.Conv_Ovf_I_Un);
        return this;
    }

    public ILWriter Conv_Ovf_I1()
    {
        Emit(OpCodes.Conv_Ovf_I1);
        return this;
    }

    public ILWriter Conv_Ovf_I1_Un()
    {
        Emit(OpCodes.Conv_Ovf_I1_Un);
        return this;
    }

    public ILWriter Conv_Ovf_I2()
    {
        Emit(OpCodes.Conv_Ovf_I2);
        return this;
    }

    public ILWriter Conv_Ovf_I2_Un()
    {
        Emit(OpCodes.Conv_Ovf_I2_Un);
        return this;
    }

    public ILWriter Conv_Ovf_I4()
    {
        Emit(OpCodes.Conv_Ovf_I4);
        return this;
    }

    public ILWriter Conv_Ovf_I4_Un()
    {
        Emit(OpCodes.Conv_Ovf_I4_Un);
        return this;
    }

    public ILWriter Conv_Ovf_I8()
    {
        Emit(OpCodes.Conv_Ovf_I8);
        return this;
    }

    public ILWriter Conv_Ovf_I8_Un()
    {
        Emit(OpCodes.Conv_Ovf_I8_Un);
        return this;
    }

    public ILWriter Conv_Ovf_U()
    {
        Emit(OpCodes.Conv_Ovf_U);
        return this;
    }

    public ILWriter Conv_Ovf_U_Un()
    {
        Emit(OpCodes.Conv_Ovf_U_Un);
        return this;
    }

    public ILWriter Conv_Ovf_U1()
    {
        Emit(OpCodes.Conv_Ovf_U1);
        return this;
    }

    public ILWriter Conv_Ovf_U1_Un()
    {
        Emit(OpCodes.Conv_Ovf_U1_Un);
        return this;
    }

    public ILWriter Conv_Ovf_U2()
    {
        Emit(OpCodes.Conv_Ovf_U2);
        return this;
    }

    public ILWriter Conv_Ovf_U2_Un()
    {
        Emit(OpCodes.Conv_Ovf_U2_Un);
        return this;
    }

    public ILWriter Conv_Ovf_U4()
    {
        Emit(OpCodes.Conv_Ovf_U4);
        return this;
    }

    public ILWriter Conv_Ovf_U4_Un()
    {
        Emit(OpCodes.Conv_Ovf_U4_Un);
        return this;
    }

    public ILWriter Conv_Ovf_U8()
    {
        Emit(OpCodes.Conv_Ovf_U8);
        return this;
    }

    public ILWriter Conv_Ovf_U8_Un()
    {
        Emit(OpCodes.Conv_Ovf_U8_Un);
        return this;
    }

    public ILWriter Conv_R_Un()
    {
        Emit(OpCodes.Conv_R_Un);
        return this;
    }

    public ILWriter Conv_R4(bool keep = true)
    {
        if (keep)
            Emit(OpCodes.Conv_R4);
        return this;
    }

    public ILWriter Conv_R8(bool keep = true)
    {
        if (keep)
            Emit(OpCodes.Conv_R8);
        return this;
    }

    public ILWriter Conv_U()
    {
        Emit(OpCodes.Conv_U);
        return this;
    }

    public ILWriter Conv_U1()
    {
        Emit(OpCodes.Conv_U1);
        return this;
    }

    public ILWriter Conv_U2()
    {
        Emit(OpCodes.Conv_U2);
        return this;
    }

    public ILWriter Conv_U4()
    {
        Emit(OpCodes.Conv_U4);
        return this;
    }

    public ILWriter Conv_U8()
    {
        Emit(OpCodes.Conv_U8);
        return this;
    }

    public ILWriter Conv_X8(bool uns)
    {
        Emit(uns ? OpCodes.Conv_U8 : OpCodes.Conv_I8);
        return this;
    }

    public ILWriter Cpblk()
    {
        Emit(OpCodes.Cpblk);
        return this;
    }

    public ILWriter Cpobj()
    {
        Emit(OpCodes.Cpobj);
        return this;
    }

    public ILWriter DivMod(bool uns, bool div)
    {
        Emit(div ? uns ? OpCodes.Div_Un : OpCodes.Div : uns ? OpCodes.Rem_Un : OpCodes.Rem);
        return this;
    }

    public ILWriter Div()
    {
        Emit(OpCodes.Div);
        return this;
    }

    public ILWriter Div_Un()
    {
        Emit(OpCodes.Div_Un);
        return this;
    }

    public ILWriter Dup()
    {
        Emit(OpCodes.Dup);
        return this;
    }

    public ILWriter Endfilter()
    {
        Emit(OpCodes.Endfilter);
        return this;
    }

    public ILWriter Endfinally()
    {
        Emit(OpCodes.Endfinally);
        return this;
    }

    public ILWriter Initblk()
    {
        Emit(OpCodes.Initblk);
        return this;
    }

    public ILWriter Initobj(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Initobj, type);
        return this;
    }

    public ILWriter Isinst(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Isinst, type);
        return this;
    }

    public ILWriter Jmp(MethodInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Jmp, info);
        return this;
    }

    public ILWriter Ldarg(int ind, Type st = null)
    {
        AssertArg(ind, st);

        OpCode op;
        switch (ind)
        {
        default:
            if (ind <= byte.MaxValue)
                _ilg.Emit(op = OpCodes.Ldarg_S, (byte)ind);
            else
                _ilg.Emit(op = OpCodes.Ldarg, (short)ind);
            Log(op, ind, st ?? _argTypes[ind]);
            return this;

        case 0: op = OpCodes.Ldarg_0; break;
        case 1: op = OpCodes.Ldarg_1; break;
        case 2: op = OpCodes.Ldarg_2; break;
        case 3: op = OpCodes.Ldarg_3; break;
        }
        _ilg.Emit(op);
        Log(op, st ?? _argTypes[ind]);
        return this;
    }

    public ILWriter Ldarga(int ind, Type st = null)
    {
        AssertArg(ind, st);

        OpCode op;
        if (ind <= byte.MaxValue)
            _ilg.Emit(op = OpCodes.Ldarga_S, (byte)ind);
        else
            _ilg.Emit(op = OpCodes.Ldarga, (short)ind);
        Log(op, ind, st ?? _argTypes[ind]);
        return this;
    }

    public ILWriter Ldc_B(bool b)
    {
        Emit(b ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        return this;
    }

    public ILWriter Ldc_I4(int arg)
    {
        switch (arg)
        {
        case -1:
            Emit(OpCodes.Ldc_I4_M1);
            break;
        case 0:
            Emit(OpCodes.Ldc_I4_0);
            break;
        case 1:
            Emit(OpCodes.Ldc_I4_1);
            break;
        case 2:
            Emit(OpCodes.Ldc_I4_2);
            break;
        case 3:
            Emit(OpCodes.Ldc_I4_3);
            break;
        case 4:
            Emit(OpCodes.Ldc_I4_4);
            break;
        case 5:
            Emit(OpCodes.Ldc_I4_5);
            break;
        case 6:
            Emit(OpCodes.Ldc_I4_6);
            break;
        case 7:
            Emit(OpCodes.Ldc_I4_7);
            break;
        case 8:
            Emit(OpCodes.Ldc_I4_8);
            break;
        default:
            if (sbyte.MinValue <= arg && arg <= sbyte.MaxValue)
                Emit(OpCodes.Ldc_I4_S, (sbyte)arg);
            else
                Emit(OpCodes.Ldc_I4, arg);
            break;
        }
        return this;
    }

    public ILWriter Ldc_I8(long arg)
    {
        if (int.MinValue <= arg && arg <= int.MaxValue)
        {
            this
                .Ldc_I4((int)arg)
                .Conv_I8();
        }
        else
            Emit(OpCodes.Ldc_I8, arg);
        return this;
    }

    public ILWriter Ldc_U4(uint arg)
    {
        return Ldc_I4((int)arg);
    }

    public ILWriter Ldc_U8(ulong arg)
    {
        if (arg <= uint.MaxValue)
        {
            this
                .Ldc_U4((uint)arg)
                .Conv_U8();
            return this;
        }
        return Ldc_I8((long)arg);
    }

    public ILWriter Ldc_Ix(long val, bool is8)
    {
        if (is8)
            return Ldc_I8(val);
        Validation.Assert((int)val == val);
        return Ldc_I4((int)val);
    }

    public ILWriter Ldc_Ux(ulong val, bool is8)
    {
        if (is8)
            return Ldc_U8(val);
        Validation.Assert((uint)val == val);
        return Ldc_U4((uint)val);
    }

    public ILWriter Ldc_R4(float arg)
    {
        Emit(OpCodes.Ldc_R4, arg);
        return this;
    }

    public ILWriter Ldc_R8(double arg)
    {
        Emit(OpCodes.Ldc_R8, arg);
        return this;
    }

    public ILWriter Ldc_Rx(double val, bool is8)
    {
        if (is8)
            return Ldc_R8(val);

        // Assert that the value isn't changed when cast to float and back. The complication is for NaN.
        Validation.Assert(!double.IsNaN(val) == ((double)(float)val == val));
        return Ldc_R4((float)val);
    }

    public ILWriter Ldelem(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Ldelem, type);
        return this;
    }

    public ILWriter Ldelem_I()
    {
        Emit(OpCodes.Ldelem_I);
        return this;
    }

    public ILWriter Ldelem_I1()
    {
        Emit(OpCodes.Ldelem_I1);
        return this;
    }

    public ILWriter Ldelem_I2()
    {
        Emit(OpCodes.Ldelem_I2);
        return this;
    }

    public ILWriter Ldelem_I4()
    {
        Emit(OpCodes.Ldelem_I4);
        return this;
    }

    public ILWriter Ldelem_I8()
    {
        Emit(OpCodes.Ldelem_I8);
        return this;
    }

    public ILWriter Ldelem_R4()
    {
        Emit(OpCodes.Ldelem_R4);
        return this;
    }

    public ILWriter Ldelem_R8()
    {
        Emit(OpCodes.Ldelem_R8);
        return this;
    }

    public ILWriter Ldelem_Ref()
    {
        Emit(OpCodes.Ldelem_Ref);
        return this;
    }

    public ILWriter Ldelem_U1()
    {
        Emit(OpCodes.Ldelem_U1);
        return this;
    }

    public ILWriter Ldelem_U2()
    {
        Emit(OpCodes.Ldelem_U2);
        return this;
    }

    public ILWriter Ldelem_U4()
    {
        Emit(OpCodes.Ldelem_U4);
        return this;
    }

    public ILWriter Ldelema(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Ldelema, type);
        return this;
    }

    public ILWriter Ldfld(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldfld, info);
        return this;
    }

    public ILWriter Ldflda(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldflda, info);
        return this;
    }

    public ILWriter Ldftn(MethodInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldftn, info);
        return this;
    }

    public ILWriter Ldind_I()
    {
        Emit(OpCodes.Ldind_I);
        return this;
    }

    public ILWriter Ldind_I1()
    {
        Emit(OpCodes.Ldind_I1);
        return this;
    }

    public ILWriter Ldind_I2()
    {
        Emit(OpCodes.Ldind_I2);
        return this;
    }

    public ILWriter Ldind_I4()
    {
        Emit(OpCodes.Ldind_I4);
        return this;
    }

    public ILWriter Ldind_I8()
    {
        Emit(OpCodes.Ldind_I8);
        return this;
    }

    public ILWriter Ldind_R4()
    {
        Emit(OpCodes.Ldind_R4);
        return this;
    }

    public ILWriter Ldind_R8()
    {
        Emit(OpCodes.Ldind_R8);
        return this;
    }

    public ILWriter Ldind_Ref()
    {
        Emit(OpCodes.Ldind_Ref);
        return this;
    }

    public ILWriter Ldind_U1()
    {
        Emit(OpCodes.Ldind_U1);
        return this;
    }

    public ILWriter Ldind_U2()
    {
        Emit(OpCodes.Ldind_U2);
        return this;
    }

    public ILWriter Ldind_U4()
    {
        Emit(OpCodes.Ldind_U4);
        return this;
    }

    public ILWriter Ldlen()
    {
        Emit(OpCodes.Ldlen);
        return this;
    }

    public ILWriter LdLocArg(in LocArgInfo lai)
    {
        AssertLocArg(in lai);
        if (lai.Index >= 0)
            return LdlocRaw(lai.Index, lai.SysType);
        return Ldarg(-lai.Index, lai.SysType);
    }

    public ILWriter Ldloc(LocalBuilder loc)
    {
        AssertLoc(loc);
        LdlocRaw(loc.LocalIndex, loc.LocalType);
        return this;
    }

    public ILWriter LdlocRaw(int ind, Type st = null)
    {
        AssertLoc(ind, st);

        OpCode op;
        switch (ind)
        {
        default:
            if (ind <= byte.MaxValue)
                _ilg.Emit(op = OpCodes.Ldloc_S, (byte)ind);
            else
                _ilg.Emit(op = OpCodes.Ldloc, (short)ind);
            Log(op, ind, st ?? GetLocType(ind));
            return this;

        case 0: op = OpCodes.Ldloc_0; break;
        case 1: op = OpCodes.Ldloc_1; break;
        case 2: op = OpCodes.Ldloc_2; break;
        case 3: op = OpCodes.Ldloc_3; break;
        }
        _ilg.Emit(op);
        Log(op, st ?? GetLocType(ind));
        return this;
    }

    public ILWriter LdLocArgA(in LocArgInfo lai)
    {
        AssertLocArg(in lai);
        if (lai.Index >= 0)
            return LdlocaRaw(lai.Index, lai.SysType);
        return Ldarga(-lai.Index, lai.SysType);
    }

    public ILWriter Ldloca(LocalBuilder loc)
    {
        AssertLoc(loc);
        LdlocaRaw(loc.LocalIndex, loc.LocalType);
        return this;
    }

    public ILWriter LdlocaRaw(int ind, Type st = null)
    {
        AssertLoc(ind, st);

        OpCode op;
        if (ind <= byte.MaxValue)
            _ilg.Emit(op = OpCodes.Ldloca_S, (byte)ind);
        else
            _ilg.Emit(op = OpCodes.Ldloca, (short)ind);
        Log(op, ind, st ?? GetLocType(ind));
        return this;
    }

    public ILWriter Ldnull()
    {
        Emit(OpCodes.Ldnull);
        return this;
    }

    public ILWriter Ldobj(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Ldobj, type);
        return this;
    }

    public ILWriter Ldsfld(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldsfld, info);
        return this;
    }

    public ILWriter Ldsflda(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldsflda, info);
        return this;
    }

    public ILWriter Ldstr(string str)
    {
        Validation.AssertValue(str);
        Emit(OpCodes.Ldstr, str);
        return this;
    }

    public ILWriter Ldtoken(MethodInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldtoken, info);
        return this;
    }

    public ILWriter Ldtoken(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldtoken, info);
        return this;
    }

    public ILWriter Ldtoken(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Ldtoken, type);
        return this;
    }

    public ILWriter Ldvirtftn(MethodInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Ldvirtftn, info);
        return this;
    }

    public ILWriter Leave(Label label)
    {
        Emit(OpCodes.Leave, label);
        return this;
    }

    public ILWriter Localloc()
    {
        Emit(OpCodes.Localloc);
        return this;
    }

    public ILWriter Mkrefany(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Mkrefany, type);
        return this;
    }

    public ILWriter Mul()
    {
        Emit(OpCodes.Mul);
        return this;
    }

    public ILWriter Mul_Ovf()
    {
        Emit(OpCodes.Mul_Ovf);
        return this;
    }

    public ILWriter Mul_Ovf_Un()
    {
        Emit(OpCodes.Mul_Ovf_Un);
        return this;
    }

    public ILWriter Neg()
    {
        Emit(OpCodes.Neg);
        return this;
    }

    public ILWriter Newarr(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Newarr, type);
        return this;
    }

    public ILWriter Newobj(ConstructorInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Newobj, info);
        return this;
    }

    public ILWriter Nop()
    {
        Emit(OpCodes.Nop);
        return this;
    }

    /// <summary>
    /// This is bitwise not, and NOT logical not!
    /// </summary>
    public ILWriter Not()
    {
        Emit(OpCodes.Not);
        return this;
    }

    /// <summary>
    /// Bitwise or.
    /// </summary>
    public ILWriter Or()
    {
        Emit(OpCodes.Or);
        return this;
    }

    public ILWriter Pop()
    {
        Emit(OpCodes.Pop);
        return this;
    }

    public ILWriter Readonly()
    {
        Emit(OpCodes.Readonly);
        return this;
    }

    public ILWriter Refanytype()
    {
        Emit(OpCodes.Refanytype);
        return this;
    }

    public ILWriter Refanyval(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Refanyval, type);
        return this;
    }

    public ILWriter Rem()
    {
        Emit(OpCodes.Rem);
        return this;
    }

    public ILWriter Rem_Un()
    {
        Emit(OpCodes.Rem_Un);
        return this;
    }

    public ILWriter Ret()
    {
        Emit(OpCodes.Ret);
        return this;
    }

    public ILWriter Rethrow()
    {
        Emit(OpCodes.Rethrow);
        return this;
    }

    public ILWriter Shl()
    {
        Emit(OpCodes.Shl);
        return this;
    }

    public ILWriter Shr()
    {
        Emit(OpCodes.Shr);
        return this;
    }

    public ILWriter Shr_Un()
    {
        Emit(OpCodes.Shr_Un);
        return this;
    }

    public ILWriter Shift(bool left, bool uns)
    {
        Emit(left ? OpCodes.Shl : uns ? OpCodes.Shr_Un : OpCodes.Shr);
        return this;
    }

    public ILWriter Sizeof(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Sizeof, type);
        return this;
    }

    public ILWriter Starg(int ind, Type st = null)
    {
        AssertArg(ind, st);

        OpCode op;
        if (ind <= byte.MaxValue)
            _ilg.Emit(op = OpCodes.Starg_S, (byte)ind);
        else
            _ilg.Emit(op = OpCodes.Starg, (short)ind);
        Log(op, ind, st ?? _argTypes[ind]);
        return this;
    }

    public ILWriter Stelem(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Stelem, type);
        return this;
    }

    public ILWriter Stelem_I()
    {
        Emit(OpCodes.Stelem_I);
        return this;
    }

    public ILWriter Stelem_I1()
    {
        Emit(OpCodes.Stelem_I1);
        return this;
    }

    public ILWriter Stelem_I2()
    {
        Emit(OpCodes.Stelem_I2);
        return this;
    }

    public ILWriter Stelem_I4()
    {
        Emit(OpCodes.Stelem_I4);
        return this;
    }

    public ILWriter Stelem_I8()
    {
        Emit(OpCodes.Stelem_I8);
        return this;
    }

    public ILWriter Stelem_R4()
    {
        Emit(OpCodes.Stelem_R4);
        return this;
    }

    public ILWriter Stelem_R8()
    {
        Emit(OpCodes.Stelem_R8);
        return this;
    }

    public ILWriter Stelem_Ref()
    {
        Emit(OpCodes.Stelem_Ref);
        return this;
    }

    public ILWriter Stfld(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Stfld, info);
        return this;
    }

    public ILWriter Stind_I()
    {
        Emit(OpCodes.Stind_I);
        return this;
    }

    public ILWriter Stind_I1()
    {
        Emit(OpCodes.Stind_I1);
        return this;
    }

    public ILWriter Stind_I2()
    {
        Emit(OpCodes.Stind_I2);
        return this;
    }

    public ILWriter Stind_I4()
    {
        Emit(OpCodes.Stind_I4);
        return this;
    }

    public ILWriter Stind_I8()
    {
        Emit(OpCodes.Stind_I8);
        return this;
    }

    public ILWriter Stind_R4()
    {
        Emit(OpCodes.Stind_R4);
        return this;
    }

    public ILWriter Stind_R8()
    {
        Emit(OpCodes.Stind_R8);
        return this;
    }

    public ILWriter Stind_Ref()
    {
        Emit(OpCodes.Stind_Ref);
        return this;
    }

    public ILWriter Stloc(LocalBuilder loc)
    {
        AssertLoc(loc);
        StlocRaw(loc.LocalIndex, loc.LocalType);
        return this;
    }

    public ILWriter StlocRaw(int ind, Type st = null)
    {
        AssertLoc(ind, st);

        OpCode op;
        switch (ind)
        {
        default:
            if (ind <= byte.MaxValue)
                _ilg.Emit(op = OpCodes.Stloc_S, (byte)ind);
            else
                _ilg.Emit(op = OpCodes.Stloc, (short)ind);
            Log(op, ind, st ?? GetLocType(ind));
            return this;

        case 0: op = OpCodes.Stloc_0; break;
        case 1: op = OpCodes.Stloc_1; break;
        case 2: op = OpCodes.Stloc_2; break;
        case 3: op = OpCodes.Stloc_3; break;
        }
        _ilg.Emit(op);
        Log(op, st ?? GetLocType(ind));
        return this;
    }

    public ILWriter Stobj(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Stobj, type);
        return this;
    }

    public ILWriter Stsfld(FieldInfo info)
    {
        Validation.AssertValue(info);
        Emit(OpCodes.Stsfld, info);
        return this;
    }

    public ILWriter Sub()
    {
        Emit(OpCodes.Sub);
        return this;
    }

    public ILWriter Sub_Ovf()
    {
        Emit(OpCodes.Sub_Ovf);
        return this;
    }

    public ILWriter Sub_Ovf_Un()
    {
        Emit(OpCodes.Sub_Ovf_Un);
        return this;
    }

    public ILWriter Switch(Label[] labels)
    {
        Validation.AssertValue(labels);
        Emit(OpCodes.Switch, labels);
        return this;
    }

    public ILWriter Tailcall()
    {
        Emit(OpCodes.Tailcall);
        return this;
    }

    public ILWriter Throw()
    {
        Emit(OpCodes.Throw);
        return this;
    }

    public ILWriter Unaligned(Label label)
    {
        Emit(OpCodes.Unaligned, label);
        return this;
    }

    public ILWriter Unaligned(long arg)
    {
        Validation.Assert(arg >= 0);
        Emit(OpCodes.Unaligned, arg);
        return this;
    }

    public ILWriter Unbox(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Unbox, type);
        return this;
    }

    public ILWriter Unbox_Any(Type type)
    {
        Validation.AssertValue(type);
        Emit(OpCodes.Unbox_Any, type);
        return this;
    }

    public ILWriter Volatile()
    {
        Emit(OpCodes.Volatile);
        return this;
    }

    public ILWriter Xor()
    {
        Emit(OpCodes.Xor);
        return this;
    }
}
