// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Compression;
using Microsoft.Rexl.Data;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

public sealed class WriteParquetProcGen : RexlOperationGenerator<WriteParquetProc>
{
    public static readonly WriteParquetProcGen Instance = new WriteParquetProcGen();

    private readonly MethodInfo _meth;

    private WriteParquetProcGen()
    {
        _meth =
            new Func<
                IEnumerable<RecordBase>, Link, ActionHost,
                Func<IEnumerable<RecordBase>, Stream, Action<long>, long, long>, ActionRunner>(Exec)
            .Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;

        // Convert string to Link.
        var typeDst = call.Args[1].Type;
        Validation.Assert(typeDst.Kind == DKind.Text | typeDst.Kind == DKind.Uri);
        if (typeDst.Kind == DKind.Text)
            ilw.Call(LinkHelpers.MethLinkFromPath);

        var typeTbl = call.Args[0].Type;
        Validation.Assert(typeTbl.IsTableXxx);
        var typeRec = typeTbl.ItemTypeOrThis;

        var stRec = codeGen.GetSystemType(typeRec);
        var stRecSeq = typeof(IEnumerable<>).MakeGenericType(stRec);
        Validation.Assert(stRecSeq.IsAssignableFrom(sts[0]));
        var stPwrt = typeof(Func<,,,,>).MakeGenericType(stRecSeq, typeof(Stream), typeof(Action<long>),
            typeof(long), typeof(long));

        // If there are zero fields, the action runner will throw.
        Delegate pwrt = null;
        if (typeRec.FieldCount > 0)
        {
            // REVIEW: Allow the caller to specify recsPerGroup?
            // REVIEW: Simplify the writer for the non thread case?
            pwrt = RexlParquetWriter.GetWriter(codeGen.TypeManager, typeTbl);
            Validation.Assert(stPwrt.IsAssignableFrom(pwrt.GetType()));
        }

        codeGen.GenLoadActionHost();
        codeGen.GenLoadConst(pwrt, stPwrt);

        var meth = _meth.MakeGenericMethod(stRec);
        ilw.Call(meth);

        stRet = meth.ReturnType;
        return true;
    }

    private static ActionRunner Exec<TRec>(
            IEnumerable<TRec> recs, Link link, ActionHost host,
            Func<IEnumerable<TRec>, Stream, Action<long>, long, long> pwrt)
        where TRec : RecordBase
    {
        return new ThreadRunner<TRec>(host, recs, link, pwrt);
    }

    private sealed class ThreadRunner<TRec> : ThreadActionRunner
    {
        private const int k_idLink = 0;
        private const int k_idFull = 1;
        private const int k_idRowCount = 2;
        private const int k_idGroupCount = 3;
        private const int k_idSize = 4;

        private readonly ActionHost _host;
        private readonly IEnumerable<TRec> _recs;
        private readonly Link _link;
        private readonly Func<IEnumerable<TRec>, Stream, Action<long>, long, long> _pwrt;

        // These are set when priming.
        private Link _full;
        private Stream _stream;

        private long _crow;
        private long _cgrp;
        private long _size;

        public ThreadRunner(
            ActionHost host, IEnumerable<TRec> recs, Link link,
            Func<IEnumerable<TRec>, Stream, Action<long>, long, long> pwrt)
        {
            Validation.BugCheckValue(host, nameof(host));
            Validation.BugCheckValueOrNull(recs);
            Validation.BugCheckValueOrNull(link);

            // When there are no fields, the pwrt is null.
            Validation.AssertValueOrNull(pwrt);

            _host = host;
            _recs = recs;
            _link = link;
            _pwrt = pwrt;
            _size = -1;

            Validation.Verify(AddStableResult("Link", UriFlavors.UriData).Index == k_idLink);
        }

        protected override async Task PrimeCoreAsync()
        {
            Validation.Assert(!IsPrimed);
            Validation.Check(_pwrt != null, "Can't write parquet with zero columns");

            if (_link is null)
                throw new IOException("Null link");

            (_full, _stream) = await _host.CreateStreamAsync(_link).ConfigureAwait(false);
            if (_full == null)
                _full = _link;

            Validation.Assert(!_stream.CanSeek || _stream.Length == 0);

            Validation.Verify(AddStableResult("FullLink", UriFlavors.UriData).Index == k_idFull);
            Validation.Verify(AddResult("RowCount", DType.I8Req).Index == k_idRowCount);
        }

        protected override Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            _cgrp = _pwrt(_recs, _stream, Progress, 10);
            Validation.Verify(AddResult("GroupCount", DType.I8Req).Index == k_idGroupCount);
            if (_stream.CanSeek)
            {
                _size = _stream.Length;
                // REVIEW: Perhaps this should always publish it with a value of -1
                // if it is unknown? Otherwise, no subsequent values can be published (the id
                // values would be off by one). Should there be a way to "skip" an index?
                Validation.Verify(AddResult("Size", DType.I8Req, isPrimary: true).Index == k_idSize);
            }
            return Task.CompletedTask;
        }

        protected override Task CleanupCoreAsync()
        {
            _stream?.Dispose();
            return Task.CompletedTask;
        }

        private void Progress(long count)
        {
            Interlocked.Exchange(ref _crow, count);
            Yield();
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
            case k_idRowCount:
                return Interlocked.Read(ref _crow);
            case k_idGroupCount:
                return _cgrp;
            default:
                Validation.Assert(_size >= 0 && info.Index == k_idSize);
                return _size;
            }
        }
    }
}

public sealed class WriteRbinProcGen : RexlOperationGenerator<WriteRbinProc>
{
    public static readonly WriteRbinProcGen Instance = new WriteRbinProcGen();

    private readonly MethodInfo _meth;

    private WriteRbinProcGen()
    {
        _meth = new Func<object, Link, bool, long, string, Tuple<DType>, ActionHost, ActionRunner>(Exec)
            .Method.GetGenericMethodDefinition();
    }

    protected override bool TryGenSpecialCore(ICodeGen codeGen, BndCallNode call, int idx,
        out Type stRet, out SeqWrapKind wrap)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));

        var args = call.Args;

        var typeVal = args[0].Type;
        var stVal = codeGen.GetSystemType(typeVal);

        int cur = idx + 1;

        var stTmp = codeGen.GenCode(args[0], ref cur);
        Validation.Assert(stVal.IsAssignableFrom(stTmp));

        var ilw = codeGen.Writer;
        var argPath = args[1];
        stTmp = codeGen.GenCode(argPath, ref cur);
        var typePath = argPath.Type;
        Validation.Assert(typePath.Kind == DKind.Text | typePath.Kind == DKind.Uri);
        if (typePath.Kind == DKind.Text)
        {
            Validation.Assert(stTmp == typeof(string));
            ilw.Call(LinkHelpers.MethLinkFromPath);
        }
        else
            Validation.Assert(stTmp == typeof(Link));

        if (args.Length <= 2)
        {
            // Whether to chunk defaults to true.
            ilw.Ldc_I4(1);
        }
        else
        {
            stTmp = codeGen.GenCode(args[2], ref cur);
            Validation.Assert(stTmp == typeof(bool));
        }

        if (args.Length <= 3)
        {
            // Use default compression.
            ilw
                .Ldc_I8(-1)
                .Ldnull();
        }
        else
        {
            var argComp = args[3];
            switch (argComp.Type.Kind)
            {
            case DKind.Bit:
                stTmp = codeGen.GenCode(argComp, ref cur);
                Validation.Assert(stTmp == typeof(bool));
                // Need 0L if the bool is false and -1L if the bool is true.
                ilw
                    .Neg()
                    .Conv_I8()
                    .Ldnull();
                break;
            case DKind.I8:
                stTmp = codeGen.GenCode(argComp, ref cur);
                Validation.Assert(stTmp == typeof(long));
                ilw.Ldnull();
                break;
            default:
                ilw.Ldc_I8(-1);
                stTmp = codeGen.GenCode(argComp, ref cur);
                Validation.Assert(stTmp == typeof(string));
                break;
            }
        }
        Validation.Assert(cur == idx + call.NodeCount);

        codeGen.GenLoadConst(new Tuple<DType>(typeVal));
        codeGen.GenLoadActionHost();
        var meth = _meth.MakeGenericMethod(stVal);
        ilw.Call(meth);

        stRet = meth.ReturnType;
        wrap = default;
        return true;
    }

    private static ActionRunner Exec<TVal>(TVal val, Link link, bool chunk,
        long compI8, string compStr, Tuple<DType> type, ActionHost host)
    {
        return new ThreadRunner<TVal>(host, val, link, chunk, compI8, compStr, type);
    }

    private sealed class ThreadRunner<TVal> : ThreadActionRunner
    {
        // Default compression is brotli.
        private const CompressionKind k_compDef = CompressionKind.Brotli;

        private const int k_idLink = 0;
        private const int k_idComp = 1;
        private const int k_idFull = 2;
        private const int k_idSize = 3;

        private readonly ActionHost _host;
        private readonly TVal _val;
        private readonly DType _type;
        private readonly Link _link;
        private readonly bool _chunk;
        private readonly CompressionKind _comp;
        private readonly string _compName;

        // These are set when priming.
        private Link _full;
        private Stream _stream;

        private long _size;

        public ThreadRunner(ActionHost host, TVal val, Link link, bool chunk,
            long compI8, string compStr, Tuple<DType> type)
        {
            Validation.BugCheckValue(host, nameof(host));
            Validation.BugCheckValue(type, nameof(type));
            Validation.BugCheck(type.Item1.IsValid, nameof(type));
            Validation.BugCheckValueOrNull(link);

            _host = host;
            _val = val;
            _type = type.Item1;
            _link = link;
            _chunk = chunk;

            // Determine the compression.
            CompressionKind comp;
            if (compI8 != -1)
            {
                // The I8 should be used. The str shouldn't be there.
                Validation.Assert(compStr == null);

                // If the value isn't a standard one, use the default.
                comp = (CompressionKind)compI8;
                if ((long)comp != compI8)
                    comp = k_compDef;
                else if (!comp.IsValid())
                    comp = k_compDef;
                else if (comp != 0 && !comp.IsSupported())
                    comp = k_compDef;
            }
            else if (compStr != null)
            {
                if (!CodecUtil.TryGetCompKind(compStr, out comp))
                {
                    // Use the default.
                    comp = k_compDef;
                }
            }
            else
                comp = k_compDef;

            _comp = comp;
            _compName = _comp.ToName();

            Validation.Verify(AddStableResult("Link", UriFlavors.UriData).Index == k_idLink);
            Validation.Verify(AddStableResult("Compression", DType.Text).Index == k_idComp);
        }

        protected override async Task PrimeCoreAsync()
        {
            Validation.Assert(!IsPrimed);

            if (_link is null)
                throw new IOException("Null link");

            (_full, _stream) = await _host.CreateStreamAsync(_link, StreamOptions.NeedSeek).ConfigureAwait(false);
            if (_full == null)
                _full = _link;

            Validation.Assert(_stream.CanSeek);
            Validation.Assert(_stream.Length == 0);

            Validation.Verify(AddStableResult("FullLink", UriFlavors.UriData).Index == k_idFull);
        }

        protected override Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            var settings = _chunk ? TypeManager.RbinSettings.Default : TypeManager.RbinSettings.NoChunk;
            settings = settings.SetCompression(_comp);

            Validation.Assert(_stream.Length == 0);
            if (!_host.TypeManager.TryWriteFull(_stream, _type, _val, settings))
                throw new IOException("Cannot write value to stream");
            _size = _stream.Length;
            Validation.Verify(AddResult("Size", DType.I8Req, isPrimary: true).Index == k_idSize);
            return Task.CompletedTask;
        }

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
            case k_idComp:
                return _compName;
            case k_idFull:
                return _full;
            default:
                Validation.Assert(info.Index == k_idSize);
                return _size;
            }
        }
    }
}

public sealed class WriteBytesProcGen : RexlOperationGenerator<WriteBytesProc>
{
    public static readonly WriteBytesProcGen Instance = new WriteBytesProcGen();

    private readonly MethodInfo _meth;

    private WriteBytesProcGen()
    {
        _meth = new Func<Tensor<byte>, Link, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;

        // Convert string to Link.
        var typeDst = call.Args[1].Type;
        Validation.Assert(typeDst.Kind == DKind.Text | typeDst.Kind == DKind.Uri);
        if (typeDst.Kind == DKind.Text)
            ilw.Call(LinkHelpers.MethLinkFromPath);

        codeGen.GenLoadActionHost();
        ilw.Call(_meth);

        stRet = _meth.ReturnType;
        return true;
    }

    private static ActionRunner Exec(Tensor<byte> bytes, Link link, ActionHost host)
    {
        return new ThreadRunner(host, bytes, link);
    }

    private sealed class ThreadRunner : ThreadActionRunner
    {
        private const int k_idLink = 0;
        private const int k_idFull = 1;
        private const int k_idByteCount = 2;
        private const int k_idSize = 3;

        // REVIEW: What should the page size be?
        private const int k_cbPage = 0x1000;

        private readonly ActionHost _host;
        private readonly Tensor<byte> _bytes;
        private readonly Link _link;

        // These are set when priming.
        private Link _full;
        private Stream _stream;

        private long _cb;

        public ThreadRunner(ActionHost host, Tensor<byte> bytes, Link link)
        {
            Validation.BugCheckValue(host, nameof(host));
            Validation.BugCheckValueOrNull(bytes);
            Validation.BugCheckValueOrNull(link);

            _host = host;
            _bytes = bytes;
            _link = link;

            Validation.Verify(AddStableResult("Link", UriFlavors.UriData).Index == k_idLink);
        }

        protected override async Task PrimeCoreAsync()
        {
            Validation.Assert(!IsPrimed);

            if (_bytes is null)
                throw new IOException("Null data");

            if (_link is null)
                throw new IOException("Null link");

            (_full, _stream) = await _host.CreateStreamAsync(_link).ConfigureAwait(false);
            if (_full == null)
                _full = _link;

            Validation.Assert(!_stream.CanSeek || _stream.Length == 0);

            Validation.Verify(AddStableResult("FullLink", UriFlavors.UriData).Index == k_idFull);
            Validation.Verify(AddResult("ByteCount", DType.I8Req).Index == k_idByteCount);
        }

        protected override Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            Validation.Assert(_cb == 0);
            if (Tensor.TryGetMemory(_bytes, out var mem, canAlloc: false))
                return WriteMemAsync(mem);
            return WritePagedAsync();
        }

        private async Task WriteMemAsync(ReadOnlyMemory<byte> mem)
        {
            long cb = 0;
            while (mem.Length > k_cbPage)
            {
                await _stream.WriteAsync(mem.Slice(0, k_cbPage)).ConfigureAwait(false);
                Progress(cb += k_cbPage);
                mem = mem.Slice(k_cbPage);
            }
            if (mem.Length > 0)
            {
                await _stream.WriteAsync(mem).ConfigureAwait(false);
                Progress(cb += mem.Length);
            }
            Validation.Assert(_cb == cb);

            Validation.Verify(AddResult("Size", DType.I8Req, isPrimary: true).Index == k_idSize);
        }

        private async Task WritePagedAsync()
        {
            long size = _bytes.Count;
            int cbPage = (int)Math.Min(size, k_cbPage);
            var page = new byte[cbPage];
            int ib = 0;
            long cb = 0;
            foreach (var b in _bytes.GetValues())
            {
                page[ib++] = b;
                if (ib >= cbPage)
                {
                    await _stream.WriteAsync(page, 0, ib).ConfigureAwait(false);
                    Progress(cb += ib);
                    ib = 0;
                }
            }
            if (ib > 0)
            {
                await _stream.WriteAsync(page, 0, ib).ConfigureAwait(false);
                Progress(cb += ib);
            }
            Validation.Assert(_cb == cb);

            Validation.Verify(AddResult("Size", DType.I8Req, isPrimary: true).Index == k_idSize);
        }

        protected override Task CleanupCoreAsync()
        {
            _stream?.Dispose();
            return Task.CompletedTask;
        }

        private void Progress(long count)
        {
            Interlocked.Exchange(ref _cb, count);
            Yield();
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
            case k_idByteCount:
                return Interlocked.Read(ref _cb);
            default:
                Validation.Assert(info.Index == k_idSize);
                return _cb;
            }
        }
    }
}
