// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;
using Microsoft.Rexl.Harness;

namespace Microsoft.Rexl.Kernel;

using CodeGenerator = CachingEnumerableCodeGenerator;

internal sealed class Executor : SimpleHarnessWithSinkStack
{
    private readonly SemaphoreSlim _sem;

    private readonly Logger _logger;
    private readonly Publisher _pub;

    public HarnessConfig Config { get; }

    public static Executor Create(Logger logger, Publisher pub)
    {
        var config = new HarnessConfig(verbose: false);
        var opers = new KernelBuiltins(config.SetShowIL);
        var codeGen = new CodeGenerator(new StdEnumerableTypeManager(), new KernelGenerators());
        return new Executor(config, opers, codeGen, logger, pub);
    }

    private Executor(HarnessConfig config, OperationRegistry opers, CodeGeneratorBase codeGen,
            Logger logger, Publisher pub)
        : base(config, opers, codeGen)
    {
        Validation.AssertValue(logger);
        Validation.AssertValue(pub);

        Config = config;
        _sem = new SemaphoreSlim(1, 1);
        _logger = logger;
        _pub = pub;
    }

    public async Task ExecAsync(ExecuteMessage msg, int min, int lim)
    {
        Validation.AssertValue(msg);

        var code = msg.Request.Code;
        Validation.AssertIndexInclusive(min, lim);
        Validation.AssertIndexInclusive(lim, code.Length);
        if (min >= lim)
            return;

        if (min > 0 || lim < code.Length)
            code = code[min..lim];
        if (string.IsNullOrWhiteSpace(code))
            return;

        await _sem.WaitAsync().ConfigureAwait(false);
        try
        {
            var source = SourceContext.Create(code);
            var (ret, state) = await RunAsync(new SinkImpl(this, msg), source, resetBefore: false)
                .ConfigureAwait(false);
            state?.Dispose();
            Flush();
        }
        finally
        {
            _sem.Release();
        }
    }

    private void PublishText(ExecuteMessage msg, string text)
    {
        Validation.AssertValue(msg);

        if (string.IsNullOrEmpty(text))
            return;

        _pub.PublishData(msg, text);
    }

    private void PublishHtml(ExecuteMessage msg, string html)
    {
        Validation.AssertValue(msg);

        if (string.IsNullOrEmpty(html))
            return;

        _pub.PublishData(msg, null, html);
    }

    private record struct DiagInfo(DiagSource src, BaseDiagnostic diag, RexlNode nodeCtx);

    private void PublishDiags(ExecuteMessage msg, List<DiagInfo> diagsRaw)
    {
        Validation.AssertValue(msg);

        if (Util.Size(diagsRaw) == 0)
            return;

        var sink = new SbTypeSink();
        sink.WriteLine("Diagnostics:");
        foreach (var info in diagsRaw)
        {
            sink.Write("  ");
            info.diag.Format(sink);
            sink.WriteLine();
        }
        sink.WriteLine();

        _pub.PublishError(msg, sink.Builder.ToString(), GetErrorHtml(diagsRaw));
    }

    private string GetErrorHtml(List<DiagInfo> diags)
    {
        // Wrap the first error as html.
        // REVIEW: Highlight all of them, both errors and warnings.
        RexlDiagnostic rdShow = null;
        DiagInfo recShow = default;

        foreach (var rec in diags)
        {
            if (rec.diag is not RexlDiagnostic rd)
                continue;
            if (rdShow is not null && !rd.IsError)
                continue;
            rdShow = rd;
            recShow = rec;
            if (rd.IsError)
                break;
        }

        if (rdShow is null)
            return null;

        var sbHtml = new StringBuilder();
        sbHtml.Append("<pre style=\"margin:0 0 0 2em\"><code>");

        SourceRange rngTok = rdShow.Tok.Range;
        SourceRange rngFull = rngTok;
        if (rdShow.Node != null)
            rngFull = rdShow.Node.GetFullRange();

        // REVIEW: Should we really show the whole code block containing a UDF?
        var nodeCtx = recShow.nodeCtx;
        SourceRange rngSrc = rngFull.Source.RangeAll;
        if (nodeCtx != null && nodeCtx.Token.Stream.Source == rngFull.Source)
        {
            var rngCtx = nodeCtx.GetFullRange();
            if (rngCtx.Min <= rngFull.Min && rngFull.Lim <= rngCtx.Lim)
                rngSrc = rngCtx;
        }

        Validation.Assert(rngSrc.Min <= rngFull.Min);
        Validation.Assert(rngFull.Min <= rngTok.Min);
        Validation.Assert(rngTok.Min <= rngTok.Lim);
        Validation.Assert(rngTok.Lim <= rngFull.Lim);
        Validation.Assert(rngFull.Lim <= rngSrc.Lim);

        const string k_bck = "LightCyan";
        const string k_err = "Orange";
        const string k_wrn = "Yellow";
        const string k_ctx = "Bisque";

        static void AddHtml(StringBuilder sb, string color, string txt)
        {
            sb.AppendFormat("<font style=\"background-color:{0}\">{1}</font>", color, HttpUtility.HtmlEncode(txt));
        }

        int min = Math.Min(rngTok.Min, rngFull.Min);
        if (rngSrc.Min < min)
            AddHtml(sbHtml, k_bck, new SourceRange(rngSrc.Source, rngSrc.Min, min).GetFragment());

        if (min < rngTok.Min)
            AddHtml(sbHtml, k_ctx, new SourceRange(rngSrc.Source, min, rngTok.Min).GetFragment());

        AddHtml(sbHtml, rdShow.IsError ? k_err : k_wrn,
            rngTok.Min < rngTok.Lim ? new SourceRange(rngSrc.Source, rngTok.Min, rngTok.Lim).GetFragment() : "|");

        int lim = Math.Max(rngFull.Lim, rngTok.Lim);
        if (rngTok.Lim < lim)
            AddHtml(sbHtml, k_ctx, new SourceRange(rngSrc.Source, rngTok.Lim, lim).GetFragment());

        if (lim < rngSrc.Lim)
            AddHtml(sbHtml, k_bck, new SourceRange(rngSrc.Source, lim, rngSrc.Lim).GetFragment());

        sbHtml.Append(HttpUtility.HtmlEncode("\n\n"));
        sbHtml.Append("</code></pre>");
        return sbHtml.ToString();
    }

    /// <summary>
    /// This sink publishes through its parent in a serialized fashion.
    /// Consecutive diagnostics are published together, as is consecutive values and text.
    /// These two groups are interleaved as they occur, to keep them in order.
    /// </summary>
    private sealed class SinkImpl : SbEvalSink
    {
        private readonly Executor _parent;
        private readonly ExecuteMessage _msg;

        private readonly ValueWriterConfig _config;
        private readonly StdValueWriter _valueWriter;

        /// <summary>
        /// The number of characters that have been "dumped" out of <see cref="SbEvalSink._sbOut"/>.
        /// </summary>
        private long _cchOut;

        /// <summary>
        /// The current diagnostics. If this is non-empty and there is also content in <see cref="_sbOut"/>,
        /// these logically come before the text.
        /// </summary>
        private List<DiagInfo> _diags;

        public override long CchOut => _cchOut + _sbOut.Length;

        public SinkImpl(Executor parent, ExecuteMessage msg)
            : base(null)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(msg);

            _parent = parent;
            _msg = msg;

            _config = new ValueWriterConfig();
            _valueWriter = new StdValueWriter(_config, this, parent.TypeManager);
        }

        /// <summary>
        /// If there are any diagnostics, publish them.
        /// </summary>
        private void FlushDiags()
        {
            if (Util.Size(_diags) > 0)
            {
                _parent.PublishDiags(_msg, _diags);
                _diags.Clear();
            }
        }

        /// <summary>
        /// If there is any text, publish it.
        /// </summary>
        private void FlushText()
        {
            if (_sbOut.Length > 0)
            {
                _cchOut += _sbOut.Length;
                _parent.PublishText(_msg, _sbOut.ToString());
                _sbOut.Clear();
            }
        }

        protected override void FlushCore()
        {
            FlushDiags();
            FlushText();
        }

        protected override bool PostExecExceptionCore(Exception ex, RexlFormula fma)
        {
            Validation.AssertValue(ex);
            Validation.AssertValue(fma);

            PostDiagnostic(DiagSource.ExecException, MessageDiag.Exception(ex), fma.ParseTree);
            return true;
        }

        protected override void PostDiagnosticCore(DiagSource src, BaseDiagnostic diag, RexlNode? nodeCtx)
        {
            Validation.AssertValue(diag);

            if (_sbOut.Length > 0)
            {
                // Flush the previous batch of diagnostics (if any) and then the text.
                Flush();
            }
            Util.Add(ref _diags, new DiagInfo(src, diag, nodeCtx));
        }

        protected override void PostValueCore(DType type, object? value)
        {
            Validation.Assert(type.IsValid);

            DType typeTen;
            if (value is not null &&
                (typeTen = type.ItemTypeOrThis).IsTensorXxx &&
                TensorUtil.IsPixTypeReq(typeTen.ToReq()))
            {
                if (!type.IsSequence)
                {
                    var ten = value as Tensor;
                    Validation.Assert(ten is not null);
                    if (ten is not null && TryWriteImage(ten))
                        return;
                }
                else
                {
                    var tens = value as IEnumerable<Tensor>;
                    int count = 0;
                    foreach (var ten in tens)
                    {
                        if (count >= 10)
                        {
                            _parent.PublishText(_msg, "<...>");
                            break;
                        }
                        if (ten is null || !TryWriteImage(ten))
                        {
                            if (count == 0)
                                break;
                            Flush();
                            _parent.PublishText(_msg, "<Bad image tensor>");
                        }
                        count++;
                    }

                    if (count > 0)
                        return;
                }
            }
            WriteValue(type, value, max: 1000);
        }

        private bool TryWriteImage(Tensor ten)
        {
            Validation.AssertValue(ten);

            if (!Tensor.TryGetPngFromPixels(ten, out var bytes) || !Tensor.TryGetBase64(bytes, out var b64))
                return false;

            Flush();
            _parent.PublishHtml(_msg, $"<img src=\"data:image/png;base64,{b64}\" alt=\"byte-tensor image\" />");
            return true;
        }

        protected override void WriteValueCore(DType type, object? value, int max)
        {
            Validation.Assert(type.IsValid);
            _config.Max = max;
            _valueWriter.WriteValue(type, value);
        }
    }
}
