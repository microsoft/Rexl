// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Sink;

using Date = RDate;
using Time = System.TimeSpan;

/// <summary>
/// Configuration settings for a <see cref="ValueWriterBase"/>. The property values
/// are permitted to change at arbitrary times.
/// </summary>
public interface IValueWriterConfig
{
    /// <summary>
    /// The maximum number of items to render.
    /// </summary>
    int Max { get; }

    /// <summary>
    /// Whether items of an indexed enumerable should be displayed in the order they
    /// were received (true) as opposed to index ordering (false).
    /// </summary>
    bool LazyOrder { get; }

    /// <summary>
    /// Whether to print a new line after every tuple element.
    /// </summary>
    bool TupleNewLine { get; }

    /// <summary>
    /// Whether to include the strides for tensors.
    /// 
    /// Strides should not be included when comparing for equality, because two tensors could
    /// be logically identical, but stored with different strides.
    /// </summary>
    bool ShowTensorStrides { get; }

    /// <summary>
    /// Whether integer values should be shown using hexadecimal.
    /// </summary>
    bool ShowHex { get; }

    /// <summary>
    /// Whether to display tensors as a terse contiguous list (true)
    /// instead of with their structure (false).
    /// </summary>
    bool ShowTerseTensor { get; }

    /// <summary>
    /// Whether the domain of module variables should be shown (or just the value).
    /// </summary>
    bool ShowModuleVarDomain { get; }
}

/// <summary>
/// Configuration settings for a <see cref="ValueWriterBase"/>. The property values
/// are permitted to change at arbitrary times.
/// </summary>
public class ValueWriterConfig : IValueWriterConfig
{
    public virtual int Max { get; set; }
    public virtual bool LazyOrder { get; set; }
    public virtual bool TupleNewLine { get; set; }
    public virtual bool ShowTensorStrides { get; set; }
    public virtual bool ShowHex { get; set; }
    public virtual bool ShowTerseTensor { get; set; }
    public virtual bool ShowModuleVarDomain { get; set; }

    public ValueWriterConfig(int max = 200, bool lazyOrder = false,
        bool tupNewLine = false, bool showStrides = false, bool showDoms = false)
    {
        Max = max;
        LazyOrder = lazyOrder;
        TupleNewLine = tupNewLine;
        ShowTensorStrides = showStrides;
        ShowModuleVarDomain = showDoms;
    }
}

/// <summary>
/// Base class for writing rexl values as text.
/// </summary>
public abstract partial class ValueWriterBase<TThis>
    where TThis : ValueWriterBase<TThis>
{
    private readonly IValueWriterConfig _config;
    private readonly TypeManager _tm;

    /// <summary>
    /// The maximum number of items to render.
    /// </summary>
    public int Max => _config.Max;

    /// <summary>
    /// Whether items of an indexed enumerable should be displayed in the order they
    /// were received (true) as opposed to index ordering (false).
    /// </summary>
    public bool LazyOrder => _config.LazyOrder;

    /// <summary>
    /// Whether to print a new line after every tuple element.
    /// </summary>
    public bool TupleNewLine => _config.TupleNewLine;

    /// <summary>
    /// Whether to include the strides for tensors.
    /// 
    /// Strides should not be included when comparing for equality, because two tensors could
    /// be logically identical, but stored with different strides.
    /// </summary>
    public bool ShowTensorStrides => _config.ShowTensorStrides;

    /// <summary>
    /// Whether integer values should be shown using hexadecimal.
    /// </summary>
    public bool ShowHex => _config.ShowHex;

    /// <summary>
    /// Whether to display tensors as a terse contiguous list (true)
    /// instead of with their structure (false).
    /// </summary>
    public bool ShowTerseTensor => _config.ShowTerseTensor;

    protected ValueWriterBase(IValueWriterConfig config, TypeManager tm)
    {
        Validation.AssertValue(config);
        Validation.AssertValue(tm);
        _config = config;
        _tm = tm;
    }

    /// <summary>
    /// Whether the object looks like a sequence.
    /// </summary>
    public bool IsSeq(object val)
    {
        return val is IEnumerable && !(val is string);
    }

    /// <summary>
    /// Whether the object is a tensor.
    /// </summary>
    public bool IsTen(object val)
    {
        return val is Tensor;
    }

    /// <summary>
    /// Writes a representation of a value.
    /// </summary>
    public void WriteValue(DType type, object val, string prefix = "", bool startOfLine = true)
    {
        var line = WriteValueCore(type, val, prefix, startOfLine: startOfLine);
        if (!line)
            WriteLine();
    }

    /// <summary>
    /// Writes a representation of a value. Returns whether it ended with a new line.
    /// </summary>
    private bool WriteValueCore(DType type, object val, string prefix, bool startOfLine)
    {
        switch (val)
        {
        case bool b:
            return WriteBool(b);
        case int i4:
            return WriteIx(i4);
        case long i8:
            return WriteIx(i8);
        case short i2:
            return WriteIx(i2);
        case sbyte i1:
            return WriteIx(i1);
        case uint u4:
            return WriteUx(u4);
        case ulong u8:
            return WriteUx(u8);
        case ushort u2:
            return WriteUx(u2);
        case byte u1:
            return WriteUx(u1);
        case float r4:
            return WriteRx(r4);
        case double r8:
            return WriteRx(r8);
        case string s:
            Write(s);
            return false;
        case Date dt:
            return WriteDate(dt);
        case Time ts:
            return WriteTime(ts);
        case Link link:
            return WriteLink(link);

        case RecordBase rec:
            return WriteRec(type, rec, prefix, startOfLine);
        case RuntimeModule mod:
            return WriteMod(type, mod, prefix, startOfLine);
        case TupleBase tup:
            return WriteTup(type, tup, prefix, startOfLine);
        case Tensor ten:
            return WriteTen(type, ten, prefix, startOfLine);
        case IEnumerable seq:
            return WriteSeq(type, seq, prefix, startOfLine);

        case null:
            Write("<null>");
            return false;

        default:
            Write("{0}", val);
            return false;
        }
    }

    private bool WriteRec(DType type, RecordBase rec, string prefix, bool startOfLine)
    {
        Validation.AssertValue(rec);

        if (!type.IsRecordXxx)
        {
            Validation.Assert(type == DType.General);
            if (rec.Rrti is null)
            {
                Write("NoRrti<{0}>", rec);
                return false;
            }
            type = rec.GetDType();
        }
        Validation.Assert(type.IsRecordXxx);

        if (startOfLine)
        {
            Write(prefix);
            startOfLine = false;
        }

        Write("{ ");
        string sep = "";
        int index = 0;
        string prefixInner = prefix + "  ";
        string prefixValue = prefixInner + "  ";

        foreach (var (tn, stFld, v) in _tm.GetRecordFieldValues(type, rec))
        {
            if (!startOfLine && 0 < index && index < Max && IsSeq(v))
            {
                WriteLine(sep);
                startOfLine = true;
            }

            if (startOfLine)
                Write(prefixInner);
            else
                Write(sep);
            sep = ", ";
            if (index++ >= Max)
            {
                Write("...");
                break;
            }

            Write(tn.Name.Value).Write(": ");
            startOfLine = WriteValueCore(tn.Type, v, prefixValue, false);
        }

        Write(startOfLine ? prefix : " ").Write("}");
        return false;
    }

    private bool WriteMod(DType type, RuntimeModule mod, string prefix, bool startOfLine)
    {
        Validation.Assert(mod != null);

        if (mod.Bnd.Type != type.ToReq())
        {
            Write("{0}", mod);
            return false;
        }
        Validation.Assert(type.IsModuleXxx);

        if (startOfLine)
        {
            Write(prefix);
            startOfLine = false;
        }

        bool doms = _config.ShowModuleVarDomain;
        WriteLine("module symbols:");
        startOfLine = true;
        string prefixInner = prefix + "  ";
        string prefixValue = prefixInner + "  ";

        var infos = _tm.GetTupleSlotValues(mod.Bnd.TypeItems, mod.GetItems());
        var types = mod.Bnd.TypeItems.GetTupleSlotTypes();

        foreach (var sym in mod.Bnd.Symbols)
        {
            if (!startOfLine)
            {
                WriteLine();
                startOfLine = true;
            }

            Write(prefixInner).Write(sym.SymKind.ToStr()).Write(" ");
            // REVIEW: Should escape into the underlying sink, not fully realize a string.
            Write(sym.Name.Escape());
            startOfLine = false;

            int ifma;
            switch (sym)
            {
            case ModFmaSym mfs:
                // Note that this uses ":" not ":=" to emphasize that this is the current value,
                // but not necessarily the actual formula.
                Write(": ");
                var (t, st, val) = infos[mfs.IfmaValue];
                startOfLine = WriteValueCore(t, val, prefixValue, startOfLine);
                break;
            case ModItemVar miv:
                if (doms)
                {
                    Write(" in ");
                    (t, st, val) = infos[miv.FormulaIn];
                    startOfLine = WriteValueCore(t, val, prefixValue, startOfLine);
                }
                ifma = miv.FormulaDefault;
                Validation.Assert(ifma >= 0);
                {
                    if (startOfLine)
                        Write(prefixValue);
                    Write(doms ? " def " : ": ");
                    (t, st, val) = infos[ifma];
                    startOfLine = WriteValueCore(t, val, prefixValue, startOfLine: false);
                }
                break;
            case ModSimpleVar msv:
                if (doms && (ifma = msv.FormulaFrom) >= 0)
                {
                    Write(" from ");
                    (t, st, val) = infos[ifma];
                    startOfLine = WriteValueCore(t, val, prefixValue, startOfLine);
                }
                if (doms && (ifma = msv.FormulaTo) >= 0)
                {
                    if (startOfLine)
                        Write(prefixValue);
                    Write(" to ");
                    (t, st, val) = infos[ifma];
                    startOfLine = WriteValueCore(t, val, prefixValue, startOfLine: false);
                }
                ifma = msv.FormulaDefault;
                Validation.Assert(ifma >= 0);
                {
                    if (startOfLine)
                        Write(prefixValue);
                    Write(doms ? " def " : ": ");
                    (t, st, val) = infos[ifma];
                    startOfLine = WriteValueCore(t, val, prefixValue, startOfLine: false);
                }
                break;
            default:
                Validation.Assert(false);
                break;
            }
        }
        if (!startOfLine)
            WriteLine();
        return true;
    }

    private bool WriteTup(DType type, TupleBase tup, string prefix, bool startOfLine)
    {
        if (!type.IsTupleXxx)
        {
            // REVIEW: Handle g.
            Write("{0}", tup);
            return false;
        }

        Validation.Assert(type.IsTupleXxx);
        Validation.Assert(tup != null);

        if (startOfLine)
        {
            Write(prefix);
            startOfLine = false;
        }

        Write("(");

        string prefixInner = prefix + "  ";
        string prefixValue = prefixInner + "  ";
        string sep = TupleNewLine ? "\n" + prefixValue : "";

        int slot = 0;
        foreach (var (typeCur, stCur, valCur) in _tm.GetTupleSlotValues(type, tup))
        {
            var sepWrite = false;
            if (!startOfLine && 0 < slot && slot < Max && IsSeq(valCur))
            {
                WriteLine(sep);
                startOfLine = true;
                sepWrite = true;
            }

            if (startOfLine)
                Write(prefixInner);
            if (!sepWrite)
                Write(sep);
            sep = TupleNewLine ? ",\n" + prefixValue : ", ";
            if (slot >= Max)
            {
                Write("...");
                break;
            }
            startOfLine = WriteValueCore(typeCur, valCur, prefixValue, false);
            slot++;
        }
        Validation.Assert(slot == type.TupleArity);

        if (startOfLine)
            Write(prefix);
        if (slot == 1)
            Write(",");
        Write(")");
        return false;
    }

    private bool WriteTen(DType type, Tensor ten, string prefix, bool startOfLine)
    {
        if (!type.IsTensorXxx)
        {
            // REVIEW: Handle g.
            Write("{0}", ten);
            return false;
        }

        if (ShowTerseTensor)
            return WriteTenTerse(type, ten, prefix, startOfLine);

        Validation.Assert((object)ten != null);
        var shape = ten.Shape;
        var rank = ten.Rank;
        DType typeItem = type.GetTensorItemType();
        Validation.Assert(rank >= 0);

        if (startOfLine)
        {
            Write(prefix);
            startOfLine = false;
        }

        Type st = ten.GetType();
        WriteType(st).Write("(");
        string sep = "";
        for (int i = 0; i < shape.Rank; i++)
        {
            Write(sep).Write("{0}", shape[i]);
            sep = ",";
        }
        Write(")");

        if (ShowTensorStrides)
        {
            Write("<");
            sep = "";
            for (int i = 0; i < ten.Strides.Rank; i++)
            {
                Write(sep).Write("{0}", ten.Strides[i]);
                sep = ",";
            }
            Write(">");
        }

        var ator = ten.GetObjValues().GetEnumerator();
        if (!ator.MoveNext())
        {
            Validation.Assert(ten.Count == 0);
            Write(" []");
            return false;
        }
        if (rank == 0)
        {
            Validation.Assert(ten.Count == 1);
            Write(" [");
            WriteValueCore(typeItem, ator.Current, "", false);
            Write("]");
            Validation.Assert(!ator.MoveNext());
            return false;
        }

        prefix += "  ";
        if (rank == 1)
            Write(" ");
        else
            WriteLine().Write(prefix);

        int index = 0;
        bool stop = false;
        void WriteCore(int dim)
        {
            Validation.Assert(dim < rank);
            Write("[");
            string prefixInner = prefix + new string(' ', dim + 1);
            string prefixValue = prefixInner + "  ";
            string sep = "";
            for (int i = 0; i < shape[dim]; i++)
            {
                if (stop)
                    break;

                if (startOfLine)
                    Write(prefixInner);
                else
                    Write(sep);

                if (dim < rank - 1)
                {
                    if (sep == "")
                        sep = new string('\n', rank - dim - 1) + prefixInner;
                    WriteCore(dim + 1);
                }
                else
                {
                    if (index >= Max)
                    {
                        stop = true;
                        Write("...");
                        break;
                    }
                    sep = ", ";
                    startOfLine = WriteValueCore(typeItem, ator.Current, prefixValue, false);
                    index++;
                    var tmp = ator.MoveNext();
                    Validation.Assert(tmp || index == ten.Count);
                }
            }

            Write("]");
        }

        WriteCore(0);
        if (rank > 1)
            WriteLine();
        Validation.Assert(!ator.MoveNext() || index >= Max);
        Validation.Assert(index == Math.Min(Max, ten.Count));
        return rank > 1;
    }

    private bool WriteTenTerse(DType type, Tensor ten, string prefix, bool startOfLine)
    {
        Validation.Assert(type.IsTensorXxx);
        Validation.Assert((object)ten != null);

        if (startOfLine)
        {
            Write(prefix);
            startOfLine = false;
        }

        Type st = ten.GetType();
        WriteType(st).Write("(");
        var shape = ten.Shape;
        string sep = "";
        for (int i = 0; i < shape.Rank; i++)
        {
            Write(sep).Write("{0}", shape[i]);
            sep = ",";
        }
        Write(") [:");

        DType typeItem = type.GetTensorItemType();
        string prefixInner = prefix + "  ";
        string prefixValue = prefixInner + "  ";
        sep = "";
        long index = 0;
        foreach (var v in ten.GetObjValues())
        {
            Validation.Assert(index < ten.Count);
            if (startOfLine)
                Write(prefixInner);
            else
                Write(sep);
            sep = ", ";
            if (index >= Max)
            {
                Write("...");
                break;
            }
            startOfLine = WriteValueCore(typeItem, v, prefixValue, false);
            index++;
        }

        if (startOfLine)
            Write(prefix);
        Write(":]");
        return false;
    }

    private bool WriteSeq(DType type, IEnumerable seq, string prefix, bool startOfLine)
    {
        if (!type.IsSequence)
        {
            // REVIEW: Handle g.
            Write("{0}", seq);
            return false;
        }

        Validation.Assert(type.IsSequence);
        Validation.Assert(seq != null);

        if (startOfLine)
            Write(prefix);

        var st = seq.GetType();
        WriteSeqHeader(type, st);

        if (LazyOrder)
        {
            // See if it is indexed.
            var ifaces = st.FindInterfaces((t, o) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IIndexedEnumerable<>), null);
            if (ifaces != null && ifaces.Length == 1)
            {
                var stItem = ifaces[0].GetGenericArguments()[0];
                var meth = new Func<DType, IIndexedEnumerable<object>, string, bool>(WriteIndexedSeq)
                    .Method.GetGenericMethodDefinition().MakeGenericMethod(stItem);
                return (bool)meth.Invoke(this, new object[] { type, seq, prefix });
            }
        }

        var typeItem = type.ItemTypeOrThis;
        int index = 0;
        string prefixInner = AdjustSeqPrefix(ref prefix);
        foreach (var v in seq)
        {
            Write(prefix);
            if (index >= Max)
            {
                WriteLine("...");
                break;
            }
            Write("{0,2}) ", index);
            if (!WriteValueCore(typeItem, v, prefixInner, false))
                WriteLine();
            index++;
        }

        return true;
    }

    private bool WriteIndexedSeq<T>(DType type, IIndexedEnumerable<T> seq, string prefix)
    {
        Validation.Assert(type.IsSequence);
        Validation.Assert(seq != null);

        var typeItem = type.ItemTypeOrThis;
        string prefixInner = prefix + "    ";
        using var ator = seq.GetIndexedEnumerator();
        int count = 0;
        while (ator.MoveNext())
        {
            Write(prefix);
            if (++count > Max)
            {
                WriteLine("...");
                break;
            }
            Write("{0,2}) ", ator.Index);
            if (!WriteValueCore(typeItem, ator.Value, prefixInner, false))
                WriteLine();
        }
        return true;
    }

    protected virtual void WriteSeqHeader(DType type, Type st)
    {
        WriteType(st).WriteLine();
    }

    protected virtual string AdjustSeqPrefix(ref string prefix)
    {
        prefix += "  ";
        return prefix + "    ";
    }

    protected virtual bool WriteBool(bool b)
    {
        Write(b ? "true" : "false");
        return false;
    }

    protected virtual bool WriteUx(byte val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteUx(ushort val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteUx(uint val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteUx(ulong val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteIx(sbyte val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteIx(short val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteIx(int val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteIx(long val)
    {
        Write(val.ToString(ShowHex ? "X" : "D"));
        return false;
    }

    protected virtual bool WriteRx(float val)
    {
        Write(val.ToStr());
        return false;
    }

    protected virtual bool WriteRx(double val)
    {
        Write(val.ToStr());
        return false;
    }

    protected virtual bool WriteDate(Date dt)
    {
        Write(dt.ToString(dt.TimeOfDay == default ? "yyyy/MM/dd" : "yyyy/MM/dd HH:mm:ss.fffffff"));
        return false;
    }

    protected virtual bool WriteTime(Time ts)
    {
        // When the time is just an integer number of days, just display the number of days.
        // Note that the %d format specifier displays the absolute value, so we can't just
        // use that.
        if (ts.Ticks % Time.TicksPerDay == 0)
            Write("{0}", ts.Days);
        else
            Write("{0:c}", ts);
        return false;
    }

    protected virtual bool WriteLink(Link link)
    {
        Write("Link<{0}>({1}, {2})", link.Kind.ToString(), link.AccountId ?? "<null>", link.Path ?? "<null>");
        return false;
    }

    protected abstract TThis WriteType(Type st);
    protected abstract TThis Write(string value);
    protected abstract TThis Write(string fmt, params object[] args);
    protected abstract TThis WriteLine();
    protected abstract TThis WriteLine(string value);
    protected abstract TThis WriteLine(string fmt, params object[] args);
}

/// <summary>
/// Base class for writing rexl values as text.
/// </summary>
public abstract partial class ValueWriter : ValueWriterBase<ValueWriter>
{
    protected abstract SysTypeSink Sink { get; }

    protected ValueWriter(IValueWriterConfig config, TypeManager tm)
         : base(config, tm)
    {
    }

    protected override ValueWriter WriteType(Type st) { Sink.WritePrettyType(st); return this; }
    protected override ValueWriter Write(string value) { Sink.Write(value); return this; }
    protected override ValueWriter Write(string fmt, params object[] args) { Sink.Write(fmt, args); return this; }
    protected override ValueWriter WriteLine() { Sink.WriteLine(); return this; }
    protected override ValueWriter WriteLine(string value) => Write(value).WriteLine();
    protected override ValueWriter WriteLine(string fmt, params object[] args) => Write(fmt, args).WriteLine();
}

/// <summary>
/// Standard rexl value writer implementation. Intended for product code such as harnesses,
/// not necessarily for tests.
/// </summary>
public sealed class StdValueWriter : ValueWriter
{
    protected override SysTypeSink Sink { get; }

    public StdValueWriter(IValueWriterConfig config, SysTypeSink sink, TypeManager tm)
        : base(config, tm)
    {
        Validation.BugCheckValue(sink, nameof(sink));
        Sink = sink;
    }
}

/// <summary>
/// Rexl value writer implementation used for baseline tests.
/// REVIEW: This is used by tests. It should probably be in a test assembly.
/// </summary>
public sealed class TestValueWriter : ValueWriter
{
    protected override SysTypeSink Sink { get; }

    public TestValueWriter(IValueWriterConfig config, SysTypeSink sink, TypeManager tm)
        : base(config, tm)
    {
        Validation.BugCheckValue(sink, nameof(sink));
        Sink = sink;
    }

    protected override void WriteSeqHeader(DType type, Type st)
    {
        // REVIEW: Fix?
        // Write("Sequence: ").WriteType(st).WriteLine();
        Write("Sequence: ");
        WriteType(st);
        WriteLine();
    }

    protected override string AdjustSeqPrefix(ref string prefix)
    {
        return prefix + "    ";
    }

    protected override bool WriteBool(bool b)
    {
        // Note the space at the end of "true ". This is so values align vertically.
        // REVIEW: Perhaps there should be a config setting for this?
        Write(b ? "true " : "false");
        return false;
    }

    protected override bool WriteDate(Date dt)
    {
        var ticks = dt.Ticks % Time.TicksPerMillisecond;
        Write(
            ticks == 0 ? "DT({0}, {1}, {2}, {3}, {4}, {5}, {6})" : "DT({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
            dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, ticks);
        return false;
    }

    protected override bool WriteTime(Time ts)
    {
        Write("{0}", ts);
        return false;
    }
}
