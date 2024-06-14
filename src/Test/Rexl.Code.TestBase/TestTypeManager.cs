// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace RexlTest;

using EncodingOptions = TypeManager.JsonWriter.EncodingOptions;

public sealed class TestEnumTypeManager : EnumerableTypeManager
{
    public TestEnumTypeManager()
        : base()
    {
    }

    protected override bool TryEnsureGenTypeCore(string name, Type stBase, int arity,
        bool wantEquatable, bool wantFlags,
        out Type stDefn)
    {
        if (stBase == typeof(TupleBase))
        {
            Validation.Assert(wantEquatable);
            Validation.Assert(!wantFlags);

            // Use our known types for tests, so we can easily construct them without compiling
            // and running rexl.
            switch (arity)
            {
            case 1: stDefn = typeof(TupleImpl<>); return true;
            case 2: stDefn = typeof(TupleImpl<,>); return true;
            case 3: stDefn = typeof(TupleImpl<,,>); return true;
            case 4: stDefn = typeof(TupleImpl<,,,>); return true;
            }
        }
        return base.TryEnsureGenTypeCore(name, stBase, arity, wantEquatable, wantFlags, out stDefn);
    }

    /// <summary>
    /// Creates a json writer.
    /// </summary>
    public JsonWriter CreateJsonWriter(JsonWriter.Customizer customizer = null, EncodingOptions options = EncodingOptions.None)
    {
        return new JsonWriterImpl(this, customizer, options);
    }

    private sealed class JsonWriterImpl : JsonWriter
    {
        public JsonWriterImpl(TestEnumTypeManager parent, Customizer customizer, EncodingOptions options)
            : base(parent, customizer, options)
        {
        }

        protected override bool TryWriteUri(RawJsonWriter wrt, DType type, Type st, Link value)
        {
            // This class  is only intended for testing the serialization code in TypeManager, which doesn't implement
            // Uri serialization.
            return false;
        }
    }

    /// <summary>
    /// Create a reader for deserialization JSON.
    /// </summary>
    public JsonReader CreateJsonReader()
    {
        return new JsonReaderImpl(this);
    }

    private sealed class JsonReaderImpl : JsonReader
    {
        public JsonReaderImpl(TestEnumTypeManager parent)
            : base(parent)
        {
        }

        protected override bool TryReadUri(DType type, JsonElement jelm, out Link value)
        {
            // This class  is only intended for testing the serialization code in TypeManager, which doesn't implement
            // Uri serialization
            throw new NotImplementedException();
        }
    }
}
