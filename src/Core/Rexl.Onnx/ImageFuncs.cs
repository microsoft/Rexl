// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.ML.OnnxRuntime;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Onnx;

/// <summary>
/// Wraps a resnet onnx model as a rexl func. Invoking with zero args produces the labels tensor.
/// Invoking with one arg runs inference. The input may be either a u1[h, w, c] pixel tensor where
/// c is either 3 or 4, or may be an r4[3, 224, 224] raw tensor.
/// </summary>
public sealed partial class ResNetFunc : ImageNetFunc
{
    public static readonly ResNetFunc Instance = new ResNetFunc();

    private ResNetFunc()
        : base("ResNet")
    {
    }

    public static Gen MakeGen() => new Gen();

    public sealed class Gen : ImageNetGen<ResNetFunc>
    {
        /// <summary>
        /// The normalization tensor, with mean and std deviation for each color channel.
        /// </summary>
        private readonly Tensor<(float mean, float sdev)> _norm;

        private readonly int[] _dimsIn;
        private readonly int[] _dimsOut;

        public Gen()
            : base(@"Onnx/resnet50-v2-7.onnx")
        {
            _norm = Tensor<(float mean, float sdev)>.CreateFrom(
                new[] { (0.485f, 0.229f), (0.456f, 0.224f), (0.406f, 0.225f) },
                3, 1, 1);

            _dimsIn = new[] { 1, 3, k_dim, k_dim };
            _dimsOut = new[] { 1, 1000 };
        }

        protected override Tensor<float> ExecCore(Tensor<byte> src)
        {
            Validation.AssertValue(src);
            Validation.Assert(src.Rank == 3);

            if (!TryGetCrop(src, out var crop))
                return null;

            // Transpose from HWC to CHW.
            var chw = crop.Transpose(2, 0, 1);

            // REVIEW: Would be nice to make this faster but the DNN application will almost
            // certainly dwarf this.
            var raw = Tensor._Zip(chw, _norm,
                static (c, n) => ((float)c / 255f - n.mean) / n.sdev,
                out bool shrunk);
            if (shrunk)
            {
                Validation.Assert(false);
                return null;
            }

            return ExecRawCore(raw);
        }

        protected override Tensor<float> ExecRawCore(Tensor<float> src)
        {
            Validation.AssertValue(src);
            Validation.Assert(src.Rank == 3);

            // REVIEW: Support a batch?
            var shape = src.Shape;
            if (shape[0] != _dimsIn[1])
                return null;
            if (shape[1] != _dimsIn[2])
                return null;
            if (shape[2] != _dimsIn[3])
                return null;

            if (!Tensor.TryGetMemory(src, out var mem))
                return null;

            var inputs = new List<NamedOnnxValue> { MakeTensorInput<float>("data", mem, _dimsIn) };

            var bufOut = new float[_dimsOut[1]];
            var outputs = new List<NamedOnnxValue> { MakeTensorOutput<float>("resnetv24_dense0_fwd", bufOut, _dimsOut) };

            // Run inference.
            try
            {
                RunCore(inputs, outputs);
            }
            catch (Exception)
            {
                return null;
            }

            return Tensor<float>._CreateRaw(Shape.Create(bufOut.Length), bufOut);
        }
    }
}

/// <summary>
/// Wraps an efficient net onnx model as a rexl func. Invoking with zero args produces the labels tensor.
/// Invoking with one arg runs inference. The input may be either a u1[h, w, c] pixel tensor where
/// c is either 3 or 4, or may be an r4[224, 224, 3] raw tensor.
/// </summary>
public sealed partial class EfficientNetFunc : ImageNetFunc
{
    public static readonly EfficientNetFunc Instance = new EfficientNetFunc();

    private EfficientNetFunc()
        : base("EfficientNet")
    {
    }

    public static Gen MakeGen() => new Gen();

    public sealed class Gen : ImageNetGen<EfficientNetFunc>
    {
        private readonly int[] _dimsIn;
        private readonly int[] _dimsOut;

        public Gen()
            : base(@"Onnx/efficientnet-lite4-11.onnx")
        {
            _dimsIn = new[] { 1, k_dim, k_dim, 3 };
            _dimsOut = new[] { 1, 1000 };
        }

        protected override Tensor<float> ExecCore(Tensor<byte> src)
        {
            Validation.AssertValue(src);
            Validation.Assert(src.Rank == 3);

            if (!TryGetCrop(src, out var crop))
                return null;

            // REVIEW: Would be nice to make this faster but the DNN application will almost
            // certainly dwarf this.
            var raw = Tensor._Map(crop, static (c) => ((float)c - 127f) / 128f);

            return ExecRawCore(raw);
        }

        protected override Tensor<float> ExecRawCore(Tensor<float> src)
        {
            Validation.AssertValue(src);
            Validation.Assert(src.Rank == 3);

            // REVIEW: Support a batch?
            var shape = src.Shape;
            if (shape[0] != _dimsIn[1])
                return null;
            if (shape[1] != _dimsIn[2])
                return null;
            if (shape[2] != _dimsIn[3])
                return null;

            if (!Tensor.TryGetMemory(src, out var mem))
                return null;

            var inputs = new List<NamedOnnxValue> { MakeTensorInput<float>("images:0", mem, _dimsIn) };

            var bufOut = new float[_dimsOut[1]];
            var outputs = new List<NamedOnnxValue> { MakeTensorOutput<float>("Softmax:0", bufOut, _dimsOut) };

            // Run inference.
            try
            {
                RunCore(inputs, outputs);
            }
            catch (Exception)
            {
                return null;
            }

            return Tensor<float>._CreateRaw(Shape.Create(bufOut.Length), bufOut);
        }
    }
}

/// <summary>
/// Base class for image-net onnx model functions. This assumes a common scaling and cropping
/// strategy, as well as the standard 1000 category image-net labels.
/// </summary>
public abstract partial class ImageNetFunc : OnnxFunc
{
    /// <summary>
    /// The source raw type (after preprocessing). The non-raw type is <see cref="TensorUtil.TypePixU1"/>.
    /// </summary>
    public DType TypeSrcRaw { get; }

    /// <summary>
    /// The destination type.
    /// </summary>
    protected readonly DType _typeDst;

    /// <summary>
    /// The type for the labels tensor.
    /// </summary>
    protected readonly DType _typeLab;

    protected ImageNetFunc(string name)
        : base(new DName(name), 0, 1)
    {
        TypeSrcRaw = DType.R4Req.ToTensor(opt: false, rank: 3);
        _typeDst = DType.R4Req.ToTensor(opt: true, rank: 1);
        _typeLab = DType.Text.ToTensor(opt: false, rank: 1);
    }

    protected override ArgTraits GetArgTraitsCore(int carg)
    {
        Validation.Assert(SupportsArity(carg));
        if (carg == 0)
            return ArgTraitsSimple.Create(this, eager: false, 0);
        return ArgTraitsLifting.Create(this, carg, maskLiftSeq: 0x01, maskLiftOpt: 0x01);
    }

    protected override (DType, Immutable.Array<DType>) SpecializeTypesCore(InvocationInfo info)
    {
        Validation.AssertValue(info);
        Validation.Assert(SupportsArity(info.Arity));

        if (info.Arity == 0)
            return (_typeLab, Immutable.Array<DType>.Empty);

        var type = info.Args[0].Type;
        if (type.IsTensorXxx)
            type = type.GetTensorItemType();
        var typeSrc = type.RootKind.IsFractional() ? TypeSrcRaw : TensorUtil.TypePixU1;
        return (_typeDst, Immutable.Array.Create(typeSrc));
    }

    protected override bool CertifyCore(BndCallNode call, ref bool full)
    {
        if (call.Args.Length == 0)
        {
            if (call.Type != _typeLab)
                return false;
        }
        else
        {
            Validation.Assert(call.Args.Length == 1);
            if (call.Type != _typeDst)
                return false;
            var typeSrc = call.Args[0].Type;
            if (typeSrc != TensorUtil.TypePixU1 && typeSrc != TypeSrcRaw)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Base class for image-net onnx model functions. This assumes a common scaling and cropping
/// strategy, as well as the standard 1000 category image-net labels.
/// </summary>
public abstract class ImageNetGen<TFunc> : OnnxGen<TFunc>
    where TFunc : ImageNetFunc
{
    /// <summary>
    /// If the smaller dimension of an image is less than this, grow the image so its smaller
    /// dimension equals this. Also crop to this size in both directions.
    /// </summary>
    protected const int k_dim = 224;

    /// <summary>
    /// If the smaller dimension of an image is more than this, shrink the image so its smaller
    /// dimension equals this.
    /// </summary>
    protected const int k_dimBig = 256;

    private readonly MethodInfo _methR4;
    private readonly MethodInfo _methU1;

    protected ImageNetGen(string path)
        : base(path)
    {
        _methR4 = new Func<Tensor<float>, ImageNetGen<TFunc>, Tensor<float>>(ExecRaw).Method;
        _methU1 = new Func<Tensor<byte>, ImageNetGen<TFunc>, Tensor<float>>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var func = GetOper(call);

        if (call.Args.Length == 0)
        {
            codeGen.GenLoadConst(ImageNetLabels.Labels);
            stRet = typeof(Tensor<string>);
            return true;
        }

        var typeSrc = call.Args[0].Type;
        Validation.Assert(typeSrc == TensorUtil.TypePixU1 || typeSrc == func.TypeSrcRaw);

        var meth = typeSrc == func.TypeSrcRaw ? _methR4 : _methU1;
        stRet = GenCallExtra(codeGen, meth, sts, this);
        return true;
    }

    private static Tensor<float> Exec(Tensor<byte> src, ImageNetGen<TFunc> self)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(self);
        return self.ExecCore(src);
    }

    private static Tensor<float> ExecRaw(Tensor<float> src, ImageNetGen<TFunc> self)
    {
        Validation.AssertValue(src);
        Validation.AssertValue(self);
        return self.ExecRawCore(src);
    }

    protected abstract Tensor<float> ExecCore(Tensor<byte> src);

    protected abstract Tensor<float> ExecRawCore(Tensor<float> src);

    /// <summary>
    /// Do the standard resizing and cropping for image net. If the smaller of (height, width) is less than
    /// 224, grow to make it 224. If it is larger than 256, shrink to make it 256. Then crop to the central
    /// 224 x 224.
    /// </summary>
    protected bool TryGetCrop(Tensor<byte> src, out Tensor<byte> res)
    {
        res = null;
        if (!TryGetSrcShape(src, out int dy, out int dx, out int dc))
            return false;

        var raw = src;
        {
            int ds = Math.Min(dy, dx);
            int dsNew = Math.Min(Math.Max(ds, k_dim), k_dimBig);
            if (ds != dsNew)
            {
                // Need to resize before cropping.
                if (!Tensor.TryResizePixels(raw, dsNew, 0, out var tmp))
                    return false;
                raw = tmp;
                if (!TryGetSrcShape(raw, out dy, out dx, out dc))
                {
                    Validation.Assert(false);
                    return false;
                }
            }
        }
        Validation.Assert(raw.Shape[0] == dy);
        Validation.Assert(raw.Shape[1] == dx);
        Validation.Assert(dy >= k_dim);
        Validation.Assert(dx >= k_dim);
        Validation.Assert(Math.Min(dy, dx) <= k_dimBig);

        if (dy != k_dim || dx != k_dim || dc > 3)
        {
            // Need to slice.
            int sy = (dy - k_dim) / 2;
            int sx = (dx - k_dim) / 2;
            raw = raw.GetSlice(
                SliceItem.CreateRange(sy, sy + k_dim, 1),
                SliceItem.CreateRange(sx, sx + k_dim, 1),
                SliceItem.CreateRange(0, 3, 1),
                out var shapeTmp);

            if (shapeTmp[0] != k_dim || shapeTmp[1] != k_dim || shapeTmp[2] != 3)
            {
                Validation.Assert(false);
                return false;
            }
        }

        res = raw;
        return true;
    }

    /// <summary>
    /// Get the source shape. Fails if height or width is not positive or bigger than int.MaxValue.
    /// Also fails if the number of channels is not 3 or 4.
    /// </summary>
    protected bool TryGetSrcShape(Tensor<byte> src, out int dy, out int dx, out int dc)
    {
        Validation.AssertValue(src);

        var shape = src.Shape;
        dy = (int)shape[0];
        dx = (int)shape[1];
        dc = (int)shape[2];
        if (dy != shape[0])
            return false;
        if (dy <= 0)
            return false;
        if (dx != shape[1])
            return false;
        if (dx <= 0)
            return false;
        if (dc != shape[2])
            return false;
        if (dc < 3 || dc > 4)
            return false;

        return true;
    }
}
