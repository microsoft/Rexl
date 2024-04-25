// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sequence;

namespace Microsoft.Rexl.Code;

public sealed partial class EchoProcGen : RexlOperationGenerator<EchoProc>
{
    public static readonly EchoProcGen Instance = new EchoProcGen();

    private readonly MethodInfo _meth;

    private EchoProcGen()
    {
        _meth = new Func<object, Tuple<DType>, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;

        var st = sts[0];
        ilw.BoxOpt(st);
        // Load the DType wrapped in a tuple class (need a reference type).
        codeGen.GenLoadConst(new Tuple<DType>(call.Args[0].Type));
        codeGen.GenLoadActionHost();
        ilw.Call(_meth);

        stRet = typeof(ActionRunner);
        return true;
    }

    private static ActionRunner Exec(object value, Tuple<DType> type, ActionHost host)
    {
        Validation.AssertValue(type);
        Validation.AssertValue(host);
        return new Runner(type.Item1, value);
    }

    private sealed class Runner : ActionRunner
    {
        private const int StateNone = 0;
        private const int StateFinished = 1;
        private const int StateAborted = 2;

        private readonly DType _type;
        private readonly object _value;

        private volatile int _state;

        public Runner(DType type, object value)
        {
            Validation.Assert(type.IsValid);
            Validation.Assert(value != null || type.IsOpt);
            _type = type;
            _value = value;
        }

        public override RunnerState State => _state != StateNone ? RunnerState.Done : RunnerState.None;

        public override bool IsPrimed => true;

        public override bool WasSuccessful => _state == StateFinished;

        public override Exception GetException()
        {
            return _state == StateAborted ? new OperationCanceledException("Aborted") : null;
        }

        public override string GetErrorMessage()
        {
            return _state == StateAborted ? "Aborted" : null;
        }

        public override void Play()
        {
            // Set _state to finished if it is still none.
            var was = Interlocked.CompareExchange(ref _state, StateFinished, StateNone);
            if (was == StateNone)
                AddResult("Value", _type, isPrimary: true);
        }

        public override Task PauseAsync()
        {
            // Does nothing since we don't support pausing and any execution is immediate.
            return Task.CompletedTask;
        }

        public sealed override Task PrimeAsync()
        {
            // This runner is always primed.
            return Task.CompletedTask;
        }

        public override Task WaitAsync()
        {
            // Set _state to finished if it is still none.
            Interlocked.CompareExchange(ref _state, StateFinished, StateNone);
            return Task.CompletedTask;
        }

        public override void BeginAbort()
        {
            // Set _state to aborted if it is still none.
            Interlocked.CompareExchange(ref _state, StateAborted, StateNone);
        }

        public override Task AbortAsync()
        {
            BeginAbort();
            return Task.CompletedTask;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            return _value;
        }
    }
}

public sealed partial class SyncProcGen : RexlOperationGenerator<SyncProc>
{
    public static readonly SyncProcGen Instance = new SyncProcGen();

    private readonly MethodInfo _meth;

    private SyncProcGen()
    {
        _meth = new Func<TimeSpan, TimeSpan, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(sts[0] == typeof(TimeSpan));

        codeGen.GenLoadActionHost();
        codeGen.Writer.Call(_meth);

        stRet = typeof(ActionRunner);
        return true;
    }

    private static ActionRunner Exec(TimeSpan timeRun, TimeSpan timeAbort, ActionHost host)
    {
        Validation.AssertValue(host);
        return new Runner(timeRun, timeAbort);
    }

    private sealed class Runner : SyncActionRunner
    {
        private readonly ManualResetEventSlim _evt;
        private readonly TimeSpan _timeRun;
        private readonly TimeSpan _timeAbort;

        private volatile bool _abort;

        public Runner(TimeSpan timeRun, TimeSpan timeAbort)
        {
            _evt = new ManualResetEventSlim();
            _timeRun = timeRun;
            _timeAbort = timeAbort;
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            throw Validation.BugExcept();
        }

        protected override void RunCore()
        {
            if (_timeRun.Ticks > 0)
                _evt.Wait(_timeRun);
            if (_abort)
                throw new OperationCanceledException("Aborted!!!");
        }

        protected override void AbortCore()
        {
            if (_timeAbort.Ticks > 0)
                Thread.Sleep(_timeAbort);
            _abort = true;
            _evt.Set();
        }

        protected override bool ShouldRethrow(Exception ex)
        {
            return !(ex is OperationCanceledException);
        }
    }
}

public sealed partial class ThreadProcGen : RexlOperationGenerator<ThreadProc>
{
    public static readonly ThreadProcGen Instance = new ThreadProcGen();

    private readonly MethodInfo _meth;

    private ThreadProcGen()
    {
        _meth = new Func<TimeSpan, long, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);
        Validation.Assert(sts[0] == typeof(TimeSpan));

        codeGen.GenLoadActionHost();
        codeGen.Writer.Call(_meth);

        stRet = typeof(ActionRunner);
        return true;
    }

    private static ActionRunner Exec(TimeSpan time, long count, ActionHost host)
    {
        Validation.AssertValue(host);
        return new Runner(time, count);
    }

    private sealed class Runner : ThreadActionRunner
    {
        private readonly TimeSpan _time;
        private readonly long _lim;

        /// <summary>
        /// Number of times we've yielded.
        /// </summary>
        private long _count;

        public Runner(TimeSpan time, long lim)
        {
            _time = time;
            _lim = lim;

            Validation.Verify(AddStableResult("Limit", DType.I8Req).Index == 0);
            Validation.Verify(AddResult("Count", DType.I8Req, isPrimary: true).Index == 1);
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            case 0:
                return _lim;
            default:
                Validation.Assert(info.Index == 1);
                return Interlocked.Read(ref _count);
            }
        }

        protected override Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);
            while (_count < _lim)
            {
                Yield();
                Interlocked.Increment(ref _count);
                // REVIEW: Should we change this to use task sleep?
                if (_time.Ticks > 0)
                    Thread.Sleep(_time);
            }
            return Task.CompletedTask;
        }
    }
}

public sealed partial class PipeProcGen : RexlOperationGenerator<PipeProc>
{
    public static readonly PipeProcGen Instance = new PipeProcGen();

    private readonly MethodInfo _methPipe;
    private readonly MethodInfo _methStep;

    private PipeProcGen()
    {
        _methPipe = new Func<IEnumerable<object>, IEnumerable<object>, Tuple<DType>, ActionHost, ActionRunner>(Exec<object>)
            .Method.GetGenericMethodDefinition();
        _methStep = new Func<IEnumerable<object>, long, Tuple<DType>, ActionHost, ActionRunner>(ExecStep<object>)
            .Method.GetGenericMethodDefinition();
    }


    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var proc = GetOper(call);

        var typeItem = call.Args[0].Type.ItemTypeOrThis;
        var stItem = codeGen.GetSystemType(typeItem);
        Validation.Assert(typeof(IEnumerable<>).MakeGenericType(stItem).IsAssignableFrom(sts[0]));

        var ilw = codeGen.Writer;
        MethodInfo meth;
        if (proc.IsStep)
        {
            if (sts.Length == 1)
                ilw.Ldc_I8(long.MaxValue);
            meth = _methStep;
        }
        else
        {
            if (sts.Length == 1)
                ilw.Ldnull();
            meth = _methPipe;
        }

        codeGen.GenLoadConst(new Tuple<DType>(typeItem.ToSequence()), typeof(Tuple<DType>));
        codeGen.GenLoadActionHost();
        ilw.Call(meth.MakeGenericMethod(stItem));

        stRet = typeof(ActionRunner);
        return true;
    }

    private static ActionRunner ExecStep<T>(IEnumerable<T> seq, long max, Tuple<DType> type, ActionHost host)
    {
        Validation.AssertValue(type);
        Validation.AssertValue(host);
        return new Runner<T>(type.Item1, host, seq, null, max, step: true);
    }

    private static ActionRunner Exec<T>(IEnumerable<T> seq0, IEnumerable<T> seq1, Tuple<DType> type, ActionHost host)
    {
        Validation.AssertValue(type);
        Validation.AssertValue(host);
        return new Runner<T>(type.Item1, host, seq0, seq1, 0, step: false);
    }

    private sealed class Runner<T> : ThreadActionRunner
    {
        /// <summary>
        /// The type of the input/output sequence.
        /// </summary>
        private readonly DType _type;

        /// <summary>
        /// The first input sequence.
        /// </summary>
        private readonly IEnumerable<T> _input0;

        /// <summary>
        /// The second input sequence.
        /// </summary>
        private readonly IEnumerable<T> _input1;

        /// <summary>
        /// Signals that it is ok to post another value to the output.
        /// </summary>
        private readonly AutoResetEvent _evt;

        /// <summary>
        /// The number of pokes before all remaining items can be processed.
        /// </summary>
        private readonly long _max;

        /// <summary>
        /// The number of times we've been poked.
        /// </summary>
        private long _pokeCount;

        /// <summary>
        /// The number of items posted to the output stream.
        /// </summary>
        private long _count;

        /// <summary>
        /// The result sequence builder.
        /// </summary>
        private BuildableSequence<T>.Builder _bldr;

        /// <summary>
        /// The result sequence.
        /// </summary>
        private BuildableSequence<T> _output;

        public Runner(DType type, ActionHost host, IEnumerable<T> seq0, IEnumerable<T> seq1, long max, bool step)
            : base()
        {
            Validation.Assert(type.IsSequence);
            Validation.AssertValue(host);
            // Currently only non-stepped supports two sequences.
            Validation.Assert(!step || seq1 == null);

            _type = type;
            _input0 = seq0 ?? Array.Empty<T>();
            _input1 = seq1;
            _evt = step ? new AutoResetEvent(false) : null;
            _max = max;

            Validation.Verify(AddResult("Count", DType.I8Req).Index == 0);
        }

        protected override void PokeCore()
        {
            Interlocked.Increment(ref _pokeCount);
            if (_evt != null)
                _evt.Set();
        }

        protected override object GetResultValueCore(ResultInfo info)
        {
            Validation.AssertValue(info);
            switch (info.Index)
            {
            case 0:
                return Interlocked.Read(ref _count);
            case 1:
                return _output;
            default:
                Validation.Assert(info.Index == 2);
                return _output;
            }
        }

        protected override Task PrimeCoreAsync()
        {
            Validation.Assert(!IsPrimed);

            _bldr = BuildableSequence<T>.Builder.Create(-1, out _output);

            Validation.Verify(AddStreamingResult("SData", _type).Index == 1);
            return Task.CompletedTask;
        }

        protected override async Task RunCoreAsync()
        {
            Validation.Assert(IsPrimed);

            var ator0 = _input0.GetEnumerator();
            var ator1 = _input1 != null ? _input1.GetEnumerator() : null;
            try
            {
                if (_evt == null)
                {
                    long chunk = 1;
                    for (; ; )
                    {
                        // REVIEW: Ideally the Yield and MoveNext would be integrated.
                        await YieldAsync().ConfigureAwait(false);
                        if (!ator0.MoveNext())
                        {
                            if (ator1 == null)
                                break;
                            ator0.Dispose();
                            ator0 = null;
                            Util.Swap(ref ator0, ref ator1);
                            if (!ator0.MoveNext())
                                break;
                        }
                        var index = _bldr.Add(ator0.Current);
                        Validation.Assert(index == _count);
                        Interlocked.Exchange(ref _count, index + 1);
                        if (ator1 != null)
                            Util.Swap(ref ator0, ref ator1);

                        if (_count / chunk >= 100)
                            chunk *= 10;
                    }
                }
                else
                {
                    Validation.Assert(ator1 == null);
                    long num = 0;
                    long chunk = 1;
                    for (; ; )
                    {
                        var pokeCount = Interlocked.Read(ref _pokeCount);
                        if (pokeCount <= _count)
                            await YieldAsync().ConfigureAwait(false);

                        // Refresh the pokeCount since Yield could have taken a long time.
                        pokeCount = Interlocked.Read(ref _pokeCount);
                        if (pokeCount > _count || IsWaiting || pokeCount >= _max || ++num >= 5)
                        {
                            if (!ator0.MoveNext())
                                break;
                            var index = _bldr.Add(ator0.Current);
                            Validation.Assert(index == _count);
                            Interlocked.Exchange(ref _count, index + 1);
                            num = 0;

                            if (_count / chunk >= 100)
                                chunk *= 10;
                        }
                        else
                            _evt.WaitOne(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _bldr.Quit(ex);
                Validation.Assert(!_bldr.IsActive);
                throw;
            }
            finally
            {
                ator0?.Dispose();
                ator1?.Dispose();
            }

            _bldr.Done();
            _bldr = null;
            Validation.Verify(AddResult("Data", _type, isPrimary: true).Index == 2);
        }
    }
}

public sealed partial class FailProcGen : RexlOperationGenerator<FailProc>
{
    public static readonly FailProcGen Instance = new FailProcGen();

    private readonly MethodInfo _meth;

    private FailProcGen()
    {
        _meth = new Func<long, ActionHost, ActionRunner>(Exec).Method;
    }

    protected override bool TryGenCodeCore(ICodeGen codeGen, BndCallNode call, ReadOnly.Array<Type> sts, out Type stRet)
    {
        Validation.AssertValue(codeGen);
        Validation.Assert(IsValidCall(call, true));
        Validation.Assert(sts.Length == call.Args.Length);

        var ilw = codeGen.Writer;

        codeGen.GenLoadActionHost();
        ilw.Call(_meth);

        stRet = typeof(ActionRunner);
        return true;
    }

    private static ActionRunner Exec(long n, ActionHost host)
    {
        throw new NotSupportedException("FailProc throws!");
    }
}
