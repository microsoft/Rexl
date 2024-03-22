// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sink;

using Date = RDate;
using Time = System.TimeSpan;

/// <summary>
/// Helper extension methods to append a pretty type representation to a string builder.
/// </summary>
public static class TypeWriterExtensions
{
    /// <summary>
    /// Maps from system type to string representation for that type. This contains only well-known
    /// (non-generic) types.
    /// </summary>
    private static readonly Dictionary<Type, string> _stToStr = CreateTypeToStr();

    private static Dictionary<Type, string> CreateTypeToStr()
    {
        var res = new Dictionary<Type, string>();
        res.Add(typeof(object), "obj");
        res.Add(typeof(bool), "bool");
        res.Add(typeof(sbyte), "i1");
        res.Add(typeof(short), "i2");
        res.Add(typeof(int), "i4");
        res.Add(typeof(long), "i8");
        res.Add(typeof(byte), "u1");
        res.Add(typeof(ushort), "u2");
        res.Add(typeof(uint), "u4");
        res.Add(typeof(ulong), "u8");
        res.Add(typeof(BigInteger), "ia");
        res.Add(typeof(float), "r4");
        res.Add(typeof(double), "r8");
        res.Add(typeof(string), "str");
        res.Add(typeof(Guid), "Guid");
        res.Add(typeof(Date), "Date");
        res.Add(typeof(Time), "Time");
        return res;
    }

    /// <summary>
    /// Writes a string representation for the type as a raw system type. This is typically
    /// used for dumping IL or generated method signatures.
    /// </summary>
    public static TSink AppendRawType<TSink>(this TSink sink, Type st)
        where TSink : TextSink
    {
        AppendTypeCore(sink, st, asSeq: false);
        return sink;
    }

    /// <summary>
    /// Generate the string representation for the type. This displays any IEnumerable type as 'Seq' rather
    /// than the actual type. This is typically used for friendly output in tests and harness applications.
    /// </summary>
    public static TSink AppendPrettyType<TSink>(this TSink sink, Type st)
        where TSink : TextSink
    {
        AppendTypeCore(sink, st, asSeq: true);
        return sink;
    }

    private static TextSink AppendTypeCore(this TextSink sink, Type st, bool asSeq)
    {
        Validation.BugCheckValue(sink, nameof(sink));

        if (st == null)
            return sink.TWrite("<null>");

        if (_stToStr.TryGetValue(st, out var str))
            return sink.TWrite(str);

        // REVIEW: IsSubclassOf doesn't work when st is a "type builder instantiation" of a generic type!
        // So we use the BaseType property instead.
        if (typeof(RecordBase).IsAssignableFrom(st))
        {
            // Record type.
            sink.Write('{');
            string pre = "";
            var types = st.IsGenericType ? st.GenericTypeArguments : Array.Empty<Type>();
            int num = 0;
            foreach (var t in types)
            {
                if (num++ >= 20)
                {
                    sink.TWrite(pre).Write("...");
                    break;
                }
                sink.TWrite(pre).AppendRawType(t);
                pre = ",";
            }
            return sink.TWrite('}');
        }

        if (typeof(TupleBase).IsAssignableFrom(st))
        {
            // Tuple type. This assumes that the slot types are the generic type arguments.
            sink.Write('(');
            string pre = "";
            var types = st.IsGenericType ? st.GenericTypeArguments : Array.Empty<Type>();
            int num = 0;
            foreach (var t in types)
            {
                if (num++ >= 20)
                {
                    sink.TWrite(pre).Write("...");
                    break;
                }
                sink.TWrite(pre).AppendRawType(t);
                pre = ",";
            }
            return sink.TWrite(')');
        }

        if (st.BaseType == typeof(Tensor))
        {
            Validation.Assert(st.IsGenericType);
            Validation.Assert(st.GetGenericTypeDefinition() == typeof(Tensor<>));
            Type stItem = st.GetGenericArguments()[0];
            return sink.TWrite("Ten<").AppendRawType(stItem).TWrite(">");
        }

        if (asSeq)
        {
            var ifaces = st.GetInterfaces();
            foreach (var iface in ifaces)
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    st = iface;
                    break;
                }
            }
        }

        if (st.IsArray)
            return sink.TWrite("Arr<").AppendTypeCore(st.GetElementType(), asSeq).TWrite('>');

        if (st.IsGenericType)
        {
            var defn = st.GetGenericTypeDefinition();
            var args = st.GetGenericArguments();
            if (defn == typeof(Nullable<>))
                return sink.TWrite("Opt<").AppendTypeCore(args[0], asSeq).TWrite('>');
            if (defn == typeof(IEnumerable<>))
                return sink.TWrite("Seq<").AppendTypeCore(args[0], asSeq).TWrite('>');
            var name = st.Name;
            int ich = name.IndexOf('`');
            if (ich > 0 && int.TryParse(name.Substring(ich + 1), out int arity) && arity == args.Length)
                name = name.Substring(0, ich);
            sink.TWrite(name).TWrite('<');
            string pre = "";
            foreach (var t in args)
            {
                sink.TWrite(pre).AppendTypeCore(t, asSeq);
                pre = ",";
            }
            return sink.TWrite('>');
        }

        return sink.TWrite(st.Name);
    }
}
