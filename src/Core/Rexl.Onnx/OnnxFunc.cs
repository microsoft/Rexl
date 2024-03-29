// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.ML.OnnxRuntime;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Onnx;

using OnnxTensors = Microsoft.ML.OnnxRuntime.Tensors;
using SysPath = System.IO.Path;

/// <summary>
/// Base class for onnx model functions.
/// </summary>
public abstract partial class OnnxFunc : RexlOper
{
    protected OnnxFunc(DName name, int arityMin, int arityMax)
        : base(isFunc: true, name, arityMin, arityMax)
    {
    }
}

/// <summary>
/// Base class for onnx model code generators / executors.
/// </summary>
public abstract partial class OnnxGen<TFunc> : RexlOperationGenerator<TFunc>, IDisposable
    where TFunc : OnnxFunc
{
    private readonly OnnxModel _model;

    protected OnnxGen(string path)
    {
        Validation.AssertNonEmpty(path);

        _model = new OnnxModel(path);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _model.Dispose();
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Run the onnx model with the given inputs and outputs.
    /// </summary>
    protected void RunCore(IReadOnlyCollection<NamedOnnxValue> inputs, IReadOnlyCollection<NamedOnnxValue> outputs)
    {
        Validation.AssertValue(inputs);
        Validation.AssertValue(outputs);

        _model.RunCore(inputs, outputs);
    }

    /// <summary>
    /// Make a named onnx input tensor, for use in the inputs of <see cref="RunCore"/>.
    /// </summary>
    protected static NamedOnnxValue MakeTensorInput<T>(string name, ReadOnlyMemory<T> mem, ReadOnlySpan<int> dims)
    {
        return NamedOnnxValue.CreateFromTensor(name,
            new OnnxTensors.DenseTensor<T>(MemoryMarshal.AsMemory(mem), dims));
    }

    /// <summary>
    /// Make a named onnx output tensor, for use in the inputs of <see cref="RunCore"/>.
    /// </summary>
    protected static NamedOnnxValue MakeTensorOutput<T>(string name, Memory<T> mem, ReadOnlySpan<int> dims)
    {
        return NamedOnnxValue.CreateFromTensor(name, new OnnxTensors.DenseTensor<T>(mem, dims));
    }

    /// <summary>
    /// Get a full path from the assmebly containing the given type combined with the given relative path.
    /// </summary>
    protected static string GetFullPath(Type st, string pathRel)
    {
        Validation.AssertValue(st);
        return SysPath.Combine(SysPath.GetDirectoryName(st.Assembly.Location), pathRel);
    }
}

/// <summary>
/// Wrapper around an onnx model and inference session.
/// </summary>
public sealed class OnnxModel : IDisposable
{
    /// <summary>
    /// The (relative) path of the onnx model.
    /// </summary>
    private readonly string _path;

    // The inference session.
    private readonly Lazy<InferenceSession> _sess;

    // Zero if Dispose(bool) hasn't yet been called.
    private volatile int _disposed;

    public OnnxModel(string path)
    {
        Validation.AssertNonEmpty(path);

        // REVIEW: Where should we look for the model? Should it be in a resource? In a nuget?
        _path = GetFullPath(GetType(), path);
        _sess = new Lazy<InferenceSession>(CreateSession);
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;
        if (_sess.IsValueCreated)
            _sess.Value.Dispose();
    }

    /// <summary>
    /// This is used by the lazy wrapper to create the instance. One of the advantages of using lazy is
    /// that if this throws, we don't try it again, but instead accessing the Value property will throw
    /// the same exception.
    /// </summary>
    private InferenceSession CreateSession()
    {
        return new InferenceSession(_path);
    }

    /// <summary>
    /// Run the onnx model with the given inputs and outputs.
    /// </summary>
    public void RunCore(IReadOnlyCollection<NamedOnnxValue> inputs, IReadOnlyCollection<NamedOnnxValue> outputs)
    {
        Validation.AssertValue(inputs);
        Validation.AssertValue(outputs);

        if (_disposed != 0)
            throw Validation.BugExceptDisposed(GetType().Name);

        var sess = _sess.Value;
        sess.Run(inputs, outputs);
    }

    /// <summary>
    /// Get a full path from the assmebly containing the given type combined with the given relative path.
    /// </summary>
    public static string GetFullPath(Type st, string pathRel)
    {
        Validation.AssertValue(st);
        return SysPath.Combine(SysPath.GetDirectoryName(st.Assembly.Location), pathRel);
    }
}
