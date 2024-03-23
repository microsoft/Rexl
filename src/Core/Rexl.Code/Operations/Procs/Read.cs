// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Data;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

public abstract class ReadFromStreamProcGen<TOper> : RexlOperationGenerator<TOper>
    where TOper : ReadFromStreamProc
{
    private readonly MethodInfo _meth;

    protected ReadFromStreamProcGen()
    {
        _meth = new Func<Link, ActionHost, ReadFromStreamProcGen<TOper>, TOper, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var proc = GetOper(call);

        var ilw = codeGen.Writer;

        // Convert string to Link.
        var typeSrc = call.Args[0].Type;
        Validation.Assert(typeSrc.Kind == DKind.Text | typeSrc.Kind == DKind.Uri);
        if (call.Args[0].Type.Kind == DKind.Text)
            ilw.Call(LinkHelpers.MethLinkFromPath);

        codeGen.GenLoadActionHost();
        codeGen.GenLoadConst<ReadFromStreamProcGen<TOper>>(this);
        codeGen.GenLoadConst<TOper>(proc);

        var meth = new Func<Link, ActionHost, ReadFromStreamProcGen<TOper>, TOper, ActionRunner>(Exec).Method;
        ilw.Call(meth);

        stRet = meth.ReturnType;
        return true;
    }

    private static ActionRunner Exec(Link link, ActionHost host, ReadFromStreamProcGen<TOper> gen, TOper proc)
    {
        return gen.CreateRunner(proc, host, link);
    }

    private protected abstract ActionRunner CreateRunner(TOper proc, ActionHost host, Link link);

    private protected abstract class ThreadRunnerBase : ThreadActionRunner
    {
        protected const int k_idLink = 0;
        protected const int k_idFull = 1;
        protected const int k_idLimBase = 2;

        protected readonly ActionHost _host;
        protected readonly Link _link;

        // These are set when priming.
        protected Link _full;
        protected Stream _stream;

        protected ThreadRunnerBase(ActionHost host, Link link)
            : base()
        {
            Validation.BugCheckValue(host, nameof(host));
            Validation.BugCheckValueOrNull(link);

            _host = host;
            _link = link;

            Validation.Verify(AddStableResult("Link", UriFlavors.UriData).Index == k_idLink);
        }

        protected async Task LoadStreamAsync()
        {
            Validation.Assert(!IsPrimed);

            // REVIEW: Ask for seekable when needed.
            (_full, _stream) = await _host.LoadStreamAsync(_link).ConfigureAwait(false);
            if (_full == null)
                _full = _link;

            Validation.Verify(AddStableResult("FullLink", UriFlavors.UriData).Index == k_idFull);
        }

        protected abstract override Task PrimeCoreAsync();

        protected override Task CleanupCoreAsync()
        {
            _stream?.Dispose();
            return Task.CompletedTask;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            case k_idLink:
                return _link;
            case k_idFull:
                return _full;
            default:
                throw Validation.BugExceptParam(nameof(info));
            }
        }
    }
}

public sealed class ReadParquetProcGen : ReadFromStreamProcGen<ReadParquetProc>
{
    public static readonly ReadParquetProcGen Instance = new ReadParquetProcGen();

    private ReadParquetProcGen()
    {
    }

    private protected override ActionRunner CreateRunner(ReadParquetProc proc, ActionHost host, Link link)
    {
        return new ThreadRunner(host, link, proc.SuppressOpt);
    }

    private sealed class ThreadRunner : ThreadRunnerBase
    {
        private const int k_idRowCount = k_idLimBase;
        private const int k_idSData = k_idLimBase + 1;
        private const int k_idFData = k_idLimBase + 2;

        private readonly bool _suppressOpt;

        // These are set when priming.
        private RexlParquetReader _rdr;
        private DType _type;
        private IEnumerable _seq;

        private long _crow;

        public ThreadRunner(ActionHost host, Link link, bool suppressOpt)
            : base(host, link)
        {
            _suppressOpt = suppressOpt;
        }

        protected override async Task PrimeCoreAsync()
        {
            await LoadStreamAsync().ConfigureAwait(false);

            _rdr = RexlParquetReader.Create(_host.TypeManager, _stream, suppressOpt: _suppressOpt, Progress, freq: 10);
            _type = _rdr.TypeSeq;
            _seq = _rdr.Data;
            Validation.Assert(_type.IsSequence);

            Validation.Verify(AddResult("RowCount", DType.I8Req).Index == k_idRowCount);
            Validation.Verify(AddStreamingResult("SData", _type).Index == k_idSData);
        }

        protected override Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            _rdr.Run();
            Validation.Verify(AddResult("Data", _type, isPrimary: true).Index == k_idFData);
            return Task.CompletedTask;
        }

        private void Progress(long count)
        {
            _crow = count;
            Yield();
        }

        protected override Task CleanupCoreAsync()
        {
            _rdr?.Dispose();
            _rdr = null;
            return base.CleanupCoreAsync();
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            default:
                return base.GetResultValueCore(info);
            case k_idRowCount:
                return _crow;
            case k_idSData:
                return _seq;
            case k_idFData:
                return _seq;
            }
        }
    }
}

public sealed class ReadRbinProcGen : ReadFromStreamProcGen<ReadRbinProc>
{
    public static readonly ReadRbinProcGen Instance = new ReadRbinProcGen();

    private ReadRbinProcGen()
    {
    }

    private protected override ActionRunner CreateRunner(ReadRbinProc proc, ActionHost host, Link link)
    {
        return new ThreadRunner(host, link);
    }

    private sealed class ThreadRunner : ThreadRunnerBase
    {
        private const int k_idRowCount = k_idLimBase;
        private const int k_idSData = k_idLimBase + 1;
        private const int k_idFData = k_idLimBase + 2;

        private TypeManager.RbinReader _rdr;
        private DType _type;
        private Type _st;
        private object _streaming;
        private object _final;

        private long _crow;

        public ThreadRunner(ActionHost host, Link link)
            : base(host, link)
        {
        }

        protected override async Task PrimeCoreAsync()
        {
            await LoadStreamAsync().ConfigureAwait(false);

            _rdr = _host.TypeManager.CreateReader(_stream, Progress, freq: 10);
            _type = _rdr.Type;
            Validation.Assert(_type.IsValid);
            _st = _rdr.SysType;
            Validation.Assert(_st != null);

            Validation.Verify(AddResult("RowCount", DType.I8Req).Index == k_idRowCount);
            if (_type.IsSequence)
            {
                _streaming = _rdr.DataStream;
                Validation.Verify(AddStreamingResult("SData", _type).Index == k_idSData);
            }
            else
            {
                // Pretend we have a streaming sequence containing no items (null).
                Validation.Verify(AddStreamingResult("SData", _type.ToSequence()).Index == k_idSData);
            }
        }

        protected override Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            var val = _rdr.Run();
            Validation.Assert(val != null || _type.IsOpt);
            Validation.Assert(val == null || _st.IsAssignableFrom(val.GetType()));
            _final = val;

            Validation.Verify(AddResult("Data", _type, isPrimary: true).Index == k_idFData);
            return Task.CompletedTask;
        }

        private void Progress(long count)
        {
            _crow = count;
            Yield();
        }

        protected override Task CleanupCoreAsync()
        {
            _rdr?.Dispose();
            _rdr = null;
            return base.CleanupCoreAsync();
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            default:
                return base.GetResultValueCore(info);
            case k_idRowCount:
                return _crow;
            case k_idSData:
                return _streaming;
            case k_idFData:
                return _final;
            }
        }
    }
}

public sealed class ReadBytesProcGen : ReadFromStreamProcGen<ReadBytesProc>
{
    public static readonly ReadBytesProcGen Instance = new ReadBytesProcGen();

    private ReadBytesProcGen()
    {
    }

    private protected override ActionRunner CreateRunner(ReadBytesProc proc, ActionHost host, Link link)
    {
        return new ThreadRunner(host, link);
    }

    private sealed class ThreadRunner : ThreadRunnerBase
    {
        private const int k_idData = k_idLimBase;

        private Tensor<byte> _val;

        public ThreadRunner(ActionHost host, Link link)
            : base(host, link)
        {
        }

        protected override Task PrimeCoreAsync()
        {
            return LoadStreamAsync();
        }

        protected override async Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            _val = await Tensor.ReadAllBytesAsync(_stream).ConfigureAwait(false);

            Validation.Verify(
                AddResult("Data", TensorUtil.TypeBytes, isPrimary: true).Index == k_idData);
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            default:
                return base.GetResultValueCore(info);
            case k_idData:
                return _val;
            }
        }
    }
}

public sealed class ReadByteBlocksProcGen : ReadFromStreamProcGen<ReadByteBlocksProc>
{
    public static readonly ReadByteBlocksProcGen Instance = new ReadByteBlocksProcGen();

    private readonly MethodInfo _meth;

    private ReadByteBlocksProcGen()
    {
        _meth = new Func<Link, long, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx, out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var args = call.Args;

        // For one arg, do the standard thing (ends up calling CreateRunner).
        if (args.Length == 1)
            return base.TryGenSpecialCore(codeGen, call, idx, out stRet, out wrap);

        Validation.Assert(args.Length == 2);
        int cur = idx + 1;

        var stTmp = codeGen.GenCode(args[0], ref cur);

        var ilw = codeGen.Writer;

        // Convert string to Link.
        var typeSrc = args[0].Type;
        Validation.Assert(typeSrc.Kind == DKind.Text | typeSrc.Kind == DKind.Uri);
        if (typeSrc.Kind == DKind.Text)
        {
            Validation.Assert(stTmp == typeof(string));
            ilw.Call(LinkHelpers.MethLinkFromPath);
        }
        else
            Validation.Assert(stTmp == typeof(Link));

        stTmp = codeGen.GenCode(args[1], ref cur);
        Validation.Assert(stTmp == typeof(long));
        Validation.Assert(cur == idx + call.NodeCount);

        codeGen.GenLoadActionHost();

        ilw.Call(_meth);

        stRet = _meth.ReturnType;
        wrap = default;
        return true;
    }

    private protected override ActionRunner CreateRunner(ReadByteBlocksProc proc, ActionHost host, Link link)
    {
        return new ThreadRunner(host, link, 0);
    }

    private static ActionRunner Exec(Link link, long cbBlock, ActionHost host)
    {
        return new ThreadRunner(host, link, cbBlock);
    }

    private sealed class ThreadRunner : ThreadRunnerBase
    {
        private const int k_idBlockSize = k_idLimBase;
        private const int k_idBlockCount = k_idLimBase + 1;
        private const int k_idByteCount = k_idLimBase + 2;
        private const int k_idSData = k_idLimBase + 3;
        private const int k_idFData = k_idLimBase + 4;

        private const int k_cbPageDef = 1 << 12;
        private const int k_cbPageMax = 1 << 20;
        private readonly int _cbPage;

        private readonly DType _type;

        // These are set when priming.
        private BuildableSequence<Tensor<byte>>.Builder _bldr;
        private BuildableSequence<Tensor<byte>> _seq;

        private long _cpage;
        private long _cb;

        public ThreadRunner(ActionHost host, Link link, long cbPage)
            : base(host, link)
        {
            if (cbPage <= 0)
                cbPage = k_cbPageDef;
            else if (cbPage > k_cbPageMax)
                cbPage = k_cbPageMax;
            _cbPage = (int)cbPage;
            _type = TensorUtil.TypeBytes.ToSequence();
        }

        protected override async Task PrimeCoreAsync()
        {
            await LoadStreamAsync().ConfigureAwait(false);

            _bldr = BuildableSequence<Tensor<byte>>.Builder.Create(-1, out _seq);

            Validation.Verify(AddStableResult("BlockSize", DType.I8Req).Index == k_idBlockSize);
            Validation.Verify(AddStableResult("BlockCount", DType.I8Req).Index == k_idBlockCount);
            Validation.Verify(AddStableResult("ByteCount", DType.I8Req).Index == k_idByteCount);
            Validation.Verify(AddStreamingResult("SData", _type).Index == k_idSData);
        }

        protected override async Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            try
            {
                for (; ; )
                {
                    var buf = new byte[_cbPage];
                    int ib = 0;
                    for (; ; )
                    {
                        Validation.AssertIndex(ib, _cbPage);
                        int cb = await _stream.ReadAsync(buf, ib, _cbPage - ib).ConfigureAwait(false);
                        if (cb == 0)
                            break;
                        ib += cb;
                        if (ib >= _cbPage)
                            break;
                    }
                    Validation.AssertIndexInclusive(ib, _cbPage);

                    if (ib == 0)
                        break;
                    if (ib < _cbPage)
                        Array.Resize(ref buf, ib);

                    Validation.Assert(_bldr.IsActive);
                    _bldr.Add(Tensor<byte>._CreateRaw(Shape.Create(ib), Shape.One1, buf, 0));
                    _cpage++;
                    _cb += ib;

                    // We're done when a page isn't full.
                    if (ib < _cbPage)
                        break;

                    await YieldAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _bldr.Quit(ex);
                Validation.Assert(!_bldr.IsActive);
                throw;
            }

            Validation.Assert(_bldr.IsActive);
            _bldr.Done();
            Validation.Assert(!_bldr.IsActive);

            Validation.Verify(AddResult("Data", _type, isPrimary: true).Index == k_idFData);
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            default:
                return base.GetResultValueCore(info);
            case k_idBlockSize:
                return (long)_cbPage;
            case k_idBlockCount:
                return _cpage;
            case k_idByteCount:
                return _cb;
            case k_idSData:
            case k_idFData:
                return _seq;
            }
        }
    }
}

public sealed class ReadTextProcGen : ReadFromStreamProcGen<ReadTextProc>
{
    public static readonly ReadTextProcGen Instance = new ReadTextProcGen();

    private ReadTextProcGen()
    {
    }

    private protected override ActionRunner CreateRunner(ReadTextProc proc, ActionHost host, Link link)
    {
        return new ThreadRunner(host, link);
    }

    private sealed class ThreadRunner : ThreadRunnerBase
    {
        private const int k_idData = k_idLimBase;

        private string _val;

        public ThreadRunner(ActionHost host, Link link)
            : base(host, link)
        {
        }

        protected override Task PrimeCoreAsync()
        {
            return LoadStreamAsync();
        }

        protected override async Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            using var rdr = new StreamReader(_stream);
            _val = await rdr.ReadToEndAsync().ConfigureAwait(false);

            Validation.Verify(AddResult("Data", DType.Text, isPrimary: true).Index == k_idData);
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            default:
                return base.GetResultValueCore(info);
            case k_idData:
                return _val;
            }
        }
    }
}

public sealed class ReadLinesProcGen : ReadFromStreamProcGen<ReadLinesProc>
{
    public static readonly ReadLinesProcGen Instance = new ReadLinesProcGen();

    private ReadLinesProcGen()
    {
    }

    private protected override ActionRunner CreateRunner(ReadLinesProc proc, ActionHost host, Link link)
    {
        return new ThreadRunner(host, link);
    }

    private sealed class ThreadRunner : ThreadRunnerBase
    {
        private const int k_idLineCount = k_idLimBase;
        private const int k_idSData = k_idLimBase + 1;
        private const int k_idFData = k_idLimBase + 2;

        // These are set when priming.
        private BuildableSequence<string>.Builder _bldr;
        private BuildableSequence<string> _seq;

        private long _cline;

        public ThreadRunner(ActionHost host, Link link)
            : base(host, link)
        {
        }

        protected override async Task PrimeCoreAsync()
        {
            await LoadStreamAsync().ConfigureAwait(false);

            _bldr = BuildableSequence<string>.Builder.Create(-1, out _seq);

            Validation.Verify(AddResult("LineCount", DType.I8Req).Index == k_idLineCount);
            Validation.Verify(AddStreamingResult("SData", DType.Text.ToSequence()).Index == k_idSData);
        }

        protected override async Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            using (var rdr = new StreamReader(_stream))
            {
                try
                {
                    for (; ; )
                    {
                        var cur = await rdr.ReadLineAsync().ConfigureAwait(false);
                        if (cur is null)
                            break;
                        Validation.Assert(_bldr.IsActive);
                        _bldr.Add(cur);
                        _cline++;
                        await YieldAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _bldr.Quit(ex);
                    Validation.Assert(!_bldr.IsActive);
                    throw;
                }

                Validation.Assert(_bldr.IsActive);
                _bldr.Done();
                Validation.Assert(!_bldr.IsActive);
            }

            Validation.Verify(AddResult("Data", DType.Text.ToSequence(), isPrimary: true).Index == k_idFData);
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            default:
                return base.GetResultValueCore(info);
            case k_idLineCount:
                return _cline;
            case k_idSData:
            case k_idFData:
                return _seq;
            }
        }
    }
}
