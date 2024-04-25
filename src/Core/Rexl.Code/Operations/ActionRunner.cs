// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

/// <summary>
/// The base class for task execution. The code generated for an action
/// should construct and produce one of these.
/// 
/// REVIEW: Must the Play/Pause/Wait/Abort api be thread safe, in the sense
/// that is it necessary for these to support simultaneous contradictory "commands" on
/// multiple controlling threads?
/// </summary>
public abstract class ActionRunner : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Information about a published result.
    /// </summary>
    public sealed class ResultInfo
    {
        /// <summary>
        /// The name of the result.
        /// </summary>
        public DName Name { get; }

        /// <summary>
        /// The index number of the result.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// The type of the result.
        /// </summary>
        public DType Type { get; }

        /// <summary>
        /// Whether the result is the "primary" result.
        /// </summary>
        public bool IsPrimary { get; }

        /// <summary>
        /// Whether the result is "streaming" meaning that it is a sequence and iteration
        /// may be blocked if the runner is paused.
        /// </summary>
        public bool IsStreaming { get; }

        /// <summary>
        /// Whether the result is "stable" meaning that it is available when just primed (before
        /// completion) and is fine to access while playing.
        /// </summary>
        public bool IsStable { get; }

        /// <summary>
        /// This should only be used by <see cref="ActionRunner"/>. Unfortunately, C# doesn't provide
        /// a mechanism to declare that.
        /// </summary>
        internal ResultInfo(DName name, int index, DType type, bool isPrimary, bool isStreaming, bool isStable)
        {
            Validation.BugCheckParam(name.IsValid, nameof(name));
            Validation.BugCheckParam(index >= 0, nameof(index));
            Validation.BugCheckParam(type.IsValid, nameof(type));
            Validation.BugCheckParam(!isStreaming || type.IsSequence, nameof(isStreaming));

            Name = name;
            Index = index;
            Type = type;
            IsPrimary = isPrimary;
            IsStreaming = isStreaming;
            IsStable = isStable;
        }
    }

    /// <summary>
    /// This lock protects the result information.
    /// </summary>
    private readonly object _lock;

    /// <summary>
    /// Whether we've been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// The information about the results.
    /// </summary>
    private Immutable.Array<ResultInfo>.Builder _bldrRes;

    /// <summary>
    /// This is a cache of the immutable array from the builder. If it is "default",
    /// it needs to be regenerated.
    /// </summary>
    private Immutable.Array<ResultInfo> _results;

    /// <summary>
    /// The number of results. This is kept in sync with <c>_bldrRes.Count</c>, but is a
    /// separate field so we don't need to grab the lock to return it.
    /// </summary>
    private volatile int _cres;

    /// <summary>
    /// The primary result or <c>null</c> if there isn't one. This is kept in sync with
    /// the contents of <see cref="_bldrRes"/>. It is legal to read without getting the lock.
    /// </summary>
    private volatile ResultInfo _resPrimary;

    /// <summary>
    /// The current execution state. When a runner is first created, its state is
    /// <see cref="State.None"/>.
    /// </summary>
    public abstract RunnerState State { get; }

    /// <summary>
    /// Whether any (expected) streaming results are available.
    /// </summary>
    public abstract bool IsPrimed { get; }

    /// <summary>
    /// Whether the action has completed execution and it succeeded.
    /// </summary>
    public abstract bool WasSuccessful { get; }

    protected ActionRunner()
    {
        _lock = new object();
        _bldrRes = Immutable.Array<ResultInfo>.CreateBuilder();
        _results = Immutable.Array<ResultInfo>.Empty;
        _resPrimary = null;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in the 'Dispose(bool)' method
        // and/or `DisposeAsyncCore()` method.

        await DisposeAsyncCore().ConfigureAwait(false);

        // Dispose unmanaged resources.
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await AbortAsync().ConfigureAwait(false);
        DisposeCore();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Trigger abort.
            BeginAbort();
            DisposeCore();
        }
    }

    /// <summary>
    /// Shuts down the results information.
    /// </summary>
    private void DisposeCore()
    {
        lock (_lock)
        {
            _bldrRes = null;
            _results = default;
            _resPrimary = null;
            _disposed = true;
        }
    }

    private void CheckNotDisposed()
    {
        Validation.Assert(Monitor.IsEntered(_lock));
        if (_disposed)
            throw new ObjectDisposedException(nameof(ActionRunner));
    }

    /// <summary>
    /// If the action is finished (is in the <see cref="RunnerState.Done"/> state) and it failed
    /// (<see cref="WasSuccessful"/> is false), this returns a related error message. Otherwise, this
    /// returns null.
    /// </summary>
    public abstract string GetErrorMessage();

    /// <summary>
    /// If the action is finished (is in the <see cref="RunnerState.Done"/> state) and it failed
    /// (<see cref="WasSuccessful"/> is false), this returns a related exception. Otherwise, this
    /// returns null.
    /// </summary>
    public abstract Exception GetException();

    /// <summary>
    /// Initialize to a point where any streaming results are active. If there are no streaming
    /// results, and the task is in the initial state, this should do nothing.
    /// </summary>
    public abstract Task PrimeAsync();

    /// <summary>
    /// Start or resume execution.
    /// </summary>
    public abstract void Play();

    /// <summary>
    /// Pause execution. This ensures that <see cref="IsPrimed"/> is true before completing, unless
    /// (possibly) the task finishes with errors. Waits for the pause to happen, or execution to finish,
    /// if the action doesn't support pausing.
    /// </summary>
    public abstract Task PauseAsync();

    /// <summary>
    /// Wait for completion.
    /// </summary>
    public abstract Task WaitAsync();

    /// <summary>
    /// Trigger an abort, if the task is not yet finished, but don't wait for it to complete.
    /// </summary>
    public abstract void BeginAbort();

    /// <summary>
    /// Abort the task if it is not yet finished. Wait for the operation to complete.
    /// </summary>
    public abstract Task AbortAsync();

    /// <summary>
    /// This is a way to "poke" a task that it is time to do something. The semantics are
    /// intentionally vague. This may be used primarily (or exclusively) for testing scenarios.
    /// </summary>
    public virtual void Poke()
    {
    }

    /// <summary>
    /// Adds or replaces a published result.
    /// </summary>
    protected ResultInfo AddResult(string name, DType type, bool isPrimary = false)
    {
        Validation.BugCheckParam(DName.TryWrap(name, out var dn), nameof(name));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return AddResultCore(dn, type, isPrimary);
    }

    /// <summary>
    /// Adds or replaces a streaming published result. These are always stable. If it shouldn't
    /// be stable then it shouldn't be streaming.
    /// </summary>
    protected ResultInfo AddStreamingResult(string name, DType type, bool isPrimary = false)
    {
        Validation.BugCheckParam(DName.TryWrap(name, out var dn), nameof(name));
        Validation.BugCheckParam(type.IsSequence, nameof(type));
        return AddResultCore(dn, type, isPrimary, isStreaming: true, isStable: true);
    }

    /// <summary>
    /// Adds or replaces a stable published result. Stable results are available to rexl scripts
    /// while the state is "playing".
    /// </summary>
    protected ResultInfo AddStableResult(string name, DType type, bool isPrimary = false)
    {
        Validation.BugCheckParam(DName.TryWrap(name, out var dn), nameof(name));
        Validation.BugCheckParam(type.IsValid, nameof(type));
        return AddResultCore(dn, type, isPrimary, isStable: true);
    }

    /// <summary>
    /// Adds a published result. If the name matches an existing result, replaces it.
    /// </summary>
    private ResultInfo AddResultCore(DName name, DType type, bool isPrimary = false,
        bool isStreaming = false, bool isStable = false)
    {
        Validation.Assert(name.IsValid);
        Validation.Assert(type.IsValid);

        lock (_lock)
        {
            CheckNotDisposed();

            ResultInfo info;
            int index = _bldrRes.Count;
            for (int i = 0; i < _bldrRes.Count; i++)
            {
                var cur = _bldrRes[i];
                Validation.Assert(cur.Index == i);
                if (cur.Name == name)
                {
                    index = i;
                    break;
                }
            }
            Validation.AssertIndexInclusive(index, _bldrRes.Count);

            info = new ResultInfo(name, index, type, isPrimary, isStreaming, isStable);
            if (index >= _bldrRes.Count)
            {
                _bldrRes.Add(info);
                _cres = _bldrRes.Count;
            }
            else
                _bldrRes[index] = info;
            _results = default;

            // Update the primary index and status.
            if (isPrimary)
            {
                var cur = _resPrimary;
                if (cur != null && cur.Index != index)
                {
                    Validation.Assert(cur.IsPrimary);
                    Validation.AssertIndex(cur.Index, _bldrRes.Count);
                    Validation.Assert(_bldrRes[cur.Index] == cur);
                    _bldrRes[cur.Index] = new ResultInfo(cur.Name, cur.Index, cur.Type,
                        isPrimary: false, cur.IsStreaming, cur.IsStable);
                }
                _resPrimary = info;
            }
            else if (_resPrimary != null && _resPrimary.Index == index)
                _resPrimary = null;

            return info;
        }
    }

    /// <summary>
    /// The number of "results" (currently) available.
    /// </summary>
    public int ResultCount => _cres;

    /// <summary>
    /// The information for the primary result. Returns null if there is no result designated as primary.
    /// </summary>
    public ResultInfo PrimaryResult => _resPrimary;

    /// <summary>
    /// Returns information about all the results.
    /// </summary>
    public Immutable.Array<ResultInfo> GetResultInfos()
    {
        lock (_lock)
        {
            CheckNotDisposed();

            if (_results.IsDefault)
                _results = _bldrRes.ToImmutableCopy();
            return _results;
        }
    }

    /// <summary>
    /// Gets the value of the indicated result.
    /// </summary>
    public object GetResultValue(ResultInfo info)
    {
        Validation.BugCheckValue(info, nameof(info));
        lock (_lock)
        {
            CheckNotDisposed();

            Validation.Assert(_cres == _bldrRes.Count);
            Validation.BugCheckParam(Validation.IsValidIndex(info.Index, _bldrRes.Count), nameof(info));
            Validation.BugCheckParam(info == _bldrRes[info.Index], nameof(info));
        }
        return GetResultValueCore(info);
    }

    /// <summary>
    /// Subclasses need to override this to provide the value for a result.
    /// </summary>
    protected abstract object GetResultValueCore(ResultInfo info);

    /// <summary>
    /// Lookup a result by name.
    /// </summary>
    public bool TryGetResultFromName(DName name, out ResultInfo info)
    {
        lock (_lock)
        {
            CheckNotDisposed();

            Validation.Assert(_cres == _bldrRes.Count);
            for (int i = 0; i < _cres; i++)
            {
                var cur = _bldrRes[i];
                if (cur.Name == name)
                {
                    info = cur;
                    return true;
                }
            }
        }

        info = null;
        return false;
    }

    /// <summary>
    /// Gets an interesting exception from the given one. By default this maps a
    /// <see cref="TargetInvocationException"/> to its inner exception (if non-null).
    /// Subclasses can further refine the logic.
    /// </summary>
    protected virtual Exception Cleanse(Exception ex)
    {
        if (ex != null)
        {
            while (ex is TargetInvocationException tie && tie.InnerException != null)
                ex = tie.InnerException;
        }
        return ex;
    }
}

/// <summary>
/// The status of an <see cref="ActionRunner"/>.
/// </summary>
public enum RunnerState : byte
{
    /// <summary>
    /// The task has been created but not started.
    /// </summary>
    None,

    /// <summary>
    /// The task is running.
    /// </summary>
    Playing,

    /// <summary>
    /// The task is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// The task has finished running, perhaps unsuccessfully. This is the terminal state.
    /// That is, this state is never changed to another.
    /// </summary>
    Done,
}

/// <summary>
/// A base class for actions that execute synchronously. The abstract method <see cref="RunCore"/>
/// is implemented by the sub class to do the actual work.
/// </summary>
public abstract class SyncActionRunner : ActionRunner
{
    private readonly object _lock;

    // Note: this runner is never in the paused state.
    private volatile int _state;
    private volatile bool _started;
    private volatile Exception _ex;

    public sealed override RunnerState State => (RunnerState)_state;

    public sealed override bool IsPrimed => true;

    public sealed override bool WasSuccessful => _state == (int)RunnerState.Done && _ex == null;

    protected SyncActionRunner()
    {
        _lock = new object();
        _state = (int)RunnerState.None;
    }

    public override Exception GetException()
    {
        return _ex;
    }

    public override string GetErrorMessage()
    {
        var ex = _ex;
        if (ex == null)
            return null;
        var str = ex.Message;
        if (!string.IsNullOrEmpty(str))
            return str;
        return "Failed";
    }

    public sealed override Task PrimeAsync()
    {
        // This runner is always primed.
        return Task.CompletedTask;
    }

    public sealed override void Play()
    {
        if (Interlocked.CompareExchange(ref _state, (int)RunnerState.Playing, (int)RunnerState.None) == (int)RunnerState.None)
        {
            // This thread is the nominated runner. Note however that if there are other threads that
            // are simultaneously doing a Finish or Abort, we're in a race with them to see which thread actually
            // does the execution. It doesn't really matter though. Clients can't determine which thread actually
            // does the execution unless the winning thread is doing an abort. But then it just looks like we
            // support abort.
            RunSync();
        }
    }

    public sealed override Task PauseAsync()
    {
        // REVIEW: Make this truly async? Or remove this runner altogether?
        switch ((RunnerState)_state)
        {
        case RunnerState.None:
        case RunnerState.Done:
            return Task.CompletedTask;

        case RunnerState.Paused:
            // This doesn't support pausing.
            Validation.Assert(false);
            return Task.CompletedTask;

        case RunnerState.Playing:
            // Need to synchronize. RunSync does that for us.
            RunSync();
            return Task.CompletedTask;

        default:
            Validation.Assert(false);
            return Task.CompletedTask;
        }
    }

    public sealed override Task WaitAsync()
    {
        // REVIEW: Make this truly async? Or remove this runner altogether?
        Play();
        if (_state != (int)RunnerState.Done)
        {
            // We call RunSync to ensure that we've waited. See the comment in RunSync.
            RunSync();
        }
        return Task.CompletedTask;
    }

    public sealed override void BeginAbort()
    {
        Abort(wait: false);
    }

    public sealed override Task AbortAsync()
    {
        // REVIEW: Make this async?
        Abort(wait: true);
        return Task.CompletedTask;
    }

    private void Abort(bool wait)
    {
        // Make sure we're not in the None state (in Playing or Done).
        bool nominated = Interlocked.CompareExchange(ref _state, (int)RunnerState.Playing, (int)RunnerState.None) == (int)RunnerState.None;

        // Signal any execution thread that we're aborting.
        AbortCore();

        if (!wait)
        {
            if (!nominated)
                return;
            if (_started)
            {
                // To see code coverage of the  the `if (_state != ...)` case below,
                // comment out this return. Otherwise, that case is very rare unless wait
                // is true.
                return;
            }
        }

        // Whether this thread is the nominated runner or not, we grab the lock to synchronize.
        // If we happen to get the lock first, set the exception and set the state to done. That is,
        // in this case the "abort" wins over "play" or "finish".
        // REVIEW: If wait is false, nominated is true and _started is false, there's a small
        // chance that someone else got the lock first, so waiting for this lock is less than ideal.
        // Should we use a different synchronization technique?
        lock (_lock)
        {
            if (_state != (int)RunnerState.Playing)
                Validation.Assert(_state == (int)RunnerState.Done);
            else
            {
                // The running thread hasn't yet started (we got the lock first), so set the exception
                // and set the state to done.
                Validation.Assert(_ex == null);
                _ex = new OperationCanceledException("Cancelled by call to Abort");
                _state = (int)RunnerState.Done;
            }
        }
    }

    private void RunSync()
    {
        // A "Wait" may beat us to the lock. That's ok. The callers can't tell which
        // thead actually ends up doing the work.
        lock (_lock)
        {
            if (_state != (int)RunnerState.Playing)
            {
                Validation.Assert(_state == (int)RunnerState.Done);
                return;
            }

            try
            {
                _started = true;
                RunCore();
            }
            catch (Exception ex)
            {
                Validation.Assert(_ex == null);
                _ex = Cleanse(ex);
                Validation.Assert(_ex != null);
                if (ShouldRethrow(_ex))
                    throw;
            }
            finally
            {
                _state = (int)RunnerState.Done;
            }
        }
    }

    /// <summary>
    /// Implemented by the sub-class and called by the base class to perform the execution.
    /// </summary>
    protected abstract void RunCore();

    /// <summary>
    /// Implemented by the sub-class and called by the base class to abort execution if
    /// possible. Note that this may be called before <see cref="RunCore"/> is called
    /// and will typically be called on a separate thread.
    /// </summary>
    protected abstract void AbortCore();

    /// <summary>
    /// When <see cref="RunCore"/> throws an exception, the exception is recorded and
    /// this method is called to determine whether the exception should be re-thrown.
    /// In either case, the task is also transitioned to the <see cref="RunnerState.Done"/>
    /// state.
    /// </summary>
    protected virtual bool ShouldRethrow(Exception ex) => true;
}

/// <summary>
/// <para>A base class for an action that executes in its own worker task. The abstract method
/// <see cref="RunCoreAsync"/> is implemented by the sub class and called in that dedicated
/// worker task. Note that this is NOT intended to be universally used for everything. In particular,
/// it is not suitable distributed compute scenarios. It is intended for in-process actions whose
/// functionality fits the pattern of executing in process and periodically calling a yield method
/// to support pausing and aborting.</para>
/// 
/// <para>The coordination between the worker task and controlling tasks (any others that may call
/// <see cref="Play"/>, <see cref="PauseAsync"/>, <see cref="WaitAsync"/>, etc) is tricky and
/// intricate.</para>
/// 
/// <para>Here's how it works:
/// <list type="bullet">
/// <item>The implementation of <see cref="RunCoreAsync"/> (executed in the worker task) should
///   periodically call <see cref="YieldAsync"/> and/or <see cref="Yield"/> to allow for pausing
///   and aborting.</item>
/// <item>There is a "control pseudo-lock, implemented using interlocked compare exchange on a control
///   flag field. The control methods such as <see cref="Play"/> each grab this lock before doing anything.
///   If the control lock is already held when a control method requests it, an exception is thrown.
///   If clients want to make this wait instead, they must implement their own control serialization
///   logic, eg, with a 0/1 semaphore.</item>
/// <item>There is a "state" lock. Anything that changes the internal state or sets or resets any of
///   the synchronization signals grabs this lock. This lock should be released asap.</item>
/// <item>There is a <see cref="TaskCompletionSource"/>, <see cref="_tcsPrime"/>, that is set when priming
///   is completed.</item>
/// <item>To transition from created to playing, the worker task is created and <see cref="_tcsPrime"/>
///   is created.</item>
/// <item>To transition from playing to "pausing" (but not yet paused), a <see cref="TaskCompletionSource"/>
///   is allocated and assigned to the <see cref="_tcsPause"/> field.</item>
/// <item>To transition from pausing to paused, the <see cref="_state"/> is set to "paused", the
///   <see cref="_tcsPause"/> is completed successfully and the field is cleared, and a new
///   <see cref="TaskCompletionSource"/> is created and assigned to <see cref="_tcsPlay"/>. The worker task
///   then waits on <see cref="_tcsPlay"/>.</item>
/// <item>To transition from paused to playing, <see cref="_tcsPlay"/> is completed successfully, and the
///   field is cleared.</item>
/// <item>To transition to "aborting" from playing, <see cref="_abort"/> is set to true.</item>
/// <item>To transition to "aborting" from paused, <see cref="_abort"/> is set to true and we transition to
///   playing so "yield" is unblocked and can throw.</item>
/// </list>
/// </para>
/// 
/// REVIEW: Rename this appropriately and move to its own file.
/// </summary>
public abstract class ThreadActionRunner : ActionRunner
{
    /// <summary>
    /// We use this with interlocked compare exchange to detect invalid control access. It is exclusively
    /// for control threads.
    /// </summary>
    private volatile int _control;

    /// <summary>
    /// This synchronizes <see cref="_state"/> assignment and triggering/signaling/completing activity.
    /// This lock is grabbed by both control methods (after getting the <see cref="_control"/>) and
    /// the worker task.
    /// </summary>
    private readonly object _lockState;

    /// <summary>
    /// The worker task. Created when first needed, which is also where the signals are
    /// created. Note that a runner can get to the <see cref="RunnerState.Done"/> state with
    /// these not created (if it is aborted before ever playing).
    /// </summary>
    private Task _worker;

    /// <summary>
    /// Waited on by controlling threads after they've ensured the state is <see cref="RunnerState.Playing"/>.
    /// Completed successfull once <see cref="PrimeCoreAsync"/> is done. Once allocated, it is never set back
    /// to <c>null</c>. It should be non-null iff <see cref="_worker"/> is non-null.
    /// </summary>
    private TaskCompletionSource _tcsPrime;

    /// <summary>
    /// Waited on by the worker thread. Controlling threads complete this when <see cref="Play"/> is called.
    /// The worker thread creates a new instance when moving to the <see cref="RunnerState.Paused"/> state.
    /// It should never be the case that this and <see cref="_tcsPause"/> are both non-null and completed.
    /// </summary>
    private TaskCompletionSource _tcsPlay;

    /// <summary>
    /// Created and waited on by controlling threads they set the state to <see cref="RunnerState.Pausing"/>.
    /// Set to null by controlling threads when they set the state to <see cref="RunnerState.Playing"/>
    /// or <see cref="RunnerState.Aborting"/>. Completed by the worker thread when it enters a paused state.
    /// This is also set when transitioning to the done state in order to release any controlling threads
    /// that are waiting on it.
    /// </summary>
    private TaskCompletionSource _tcsPause;

    /// <summary>
    /// Whether <see cref="PrimeCoreAsync"/> has completed. This signals "yield" that we can enter the "primed"
    /// substate. This is only accessed on the execution thread so doesn't need to be volatile.
    /// </summary>
    private bool _primeCoreCalled;

    /// <summary>
    /// Whether we are primed. This somewhat matches whether <see cref="_tcsPrime"/> is completed. If there
    /// is an exception while priming, the tcs can end up completed while the bool is still false. That is,
    /// we can get to the done state with this still false.
    /// </summary>
    private volatile bool _primed;

    /// <summary>
    /// Whether the worker task has been handed out, so someone may be waiting on completion.
    /// Only transitions from false to true, never back to false.
    /// Currently used primarily for tests.
    /// REVIEW: Is this really needed?
    /// </summary>
    private volatile bool _waiting;

    /// <summary>
    /// The "fine" state. Protected by <see cref="_lockState"/>.
    /// </summary>
    private volatile RunnerState _state;

    /// <summary>
    /// Whether the task should abort. Only transitions from <c>false</c> to <c>true</c>, never back
    /// to <c>false</c>.
    /// </summary>
    private volatile bool _abort;

    /// <summary>
    /// A recorded exception for the failure case.
    /// </summary>
    private volatile Exception _ex;

    /// <summary>
    /// The number of times that <see cref="Yield"/> or <see cref="YieldAsync"/> has been called. This is
    /// useful for debugging. Also exposed as a property.
    /// </summary>
    private long _yieldCount;

    /// <summary>
    /// Used with interlocked compare exchange to ensure that yield invocations don't overlap.
    /// </summary>
    private int _yieldControl;

    /// <summary>
    /// The number of times this runner has yielded.
    /// </summary>
    public long YieldCount => Interlocked.Read(ref _yieldCount);

    public sealed override RunnerState State => _state;

    public override bool IsPrimed => _primed;

    public sealed override bool WasSuccessful
    {
        get { return _state == RunnerState.Done && _ex == null; }
    }

    /// <summary>
    /// Whether the "done" task has been handed out, indicating that someone may be waiting on completion.
    /// </summary>
    protected bool IsWaiting => _waiting;

    protected ThreadActionRunner()
    {
        _lockState = new object();
        _state = RunnerState.None;
    }

    public override Exception GetException()
    {
        return _ex;
    }

    public override string GetErrorMessage()
    {
        Exception ex = _ex;
        if (ex == null)
            return null;
        var str = ex.Message;
        if (!string.IsNullOrEmpty(str))
            return str;
        return "Failed";
    }

    /// <summary>
    /// Assert consistency between <see cref="_state"/> and other fields.
    /// This assumes/asserts <see cref="_lockState"/> is held.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void AssertState()
    {
#if DEBUG
        Validation.Assert(Monitor.IsEntered(_lockState));
        switch (_state)
        {
        case RunnerState.None:
            Validation.Assert(!HasInited());
            Validation.Assert(!_primed);
            return;
        case RunnerState.Playing:
            Validation.Assert(HasInited());
            Validation.Assert(_tcsPlay == null);
            Validation.Assert(_tcsPause == null || !_tcsPause.Task.IsCompleted);
            Validation.Assert(!_worker.IsCompleted);
            return;
        case RunnerState.Paused:
            Validation.Assert(HasInited());
            Validation.Assert(_tcsPlay != null);
            Validation.Assert(!_tcsPlay.Task.IsCompleted);
            Validation.Assert(_tcsPause == null);
            // Paused implies primed.
            Validation.Assert(_primed);
            Validation.Assert(_tcsPrime == null);
            // The abort flag shouldn't be set when paused.
            Validation.Assert(!_abort);
            return;
        default:
            Validation.Assert(IsDone());
            return;
        }
#endif
    }

    /// <summary>
    /// Private method to test for whether we've initiated execution. Also asserts
    /// consistency. This is primarily used for asserts.
    /// This assumes/asserts <see cref="_lockState"/> is held.
    /// </summary>
    private bool HasInited()
    {
        Validation.Assert(Monitor.IsEntered(_lockState));

        if (_worker == null)
        {
            Validation.Assert(_tcsPrime == null);
            Validation.Assert(_tcsPlay == null);
            Validation.Assert(_tcsPause == null);
            Validation.Assert(_state == RunnerState.None || _state == RunnerState.Done);
            return false;
        }

        Validation.Assert(_state != RunnerState.None);
        return true;
    }

    /// <summary>
    /// Private method to test for whether we're done executing. Also asserts
    /// consistency. This is primarily used for asserts.
    /// This assumes/asserts <see cref="_lockState"/> is held.
    /// </summary>
    private bool IsDone()
    {
        Validation.Assert(Monitor.IsEntered(_lockState));

        // This does consistency asserts.
        bool inited = HasInited();

        if (_state != RunnerState.Done)
            return false;

        Validation.Assert(_tcsPlay == null);
        if (inited)
        {
            // No controlling threads should be blocked. Note that _primed is not necessarily true,
            // eg, when PrimeCoreAsync throws an exception.
            // REVIEW: Can't really assert this - doing so introduces a race condition.
            // Perhaps we should use a separate tcs for done?
            // Validation.Assert(_worker.IsCompleted);
            Validation.Assert(_tcsPrime == null);
            Validation.Assert(_tcsPause == null);
        }
        return true;
    }

    /// <summary>
    /// Create a <see cref="TaskCompletionSource"/> used to signal a transition.
    /// </summary>
    private static TaskCompletionSource CreateTcs()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Complete a <see cref="TaskCompletionSource"/> and clear it. Typically, <paramref name="tcs"/>
    /// is a field of the action runner.
    /// </summary>
    private static void CompleteTcs(ref TaskCompletionSource tcs)
    {
        Validation.AssertValue(tcs);
        tcs.TrySetResult().Verify();
        tcs = null;
    }

    private void Init(bool pausing)
    {
        Validation.Assert(_state == RunnerState.None);
        AssertState();

        // Start playing. Create the signals and thread.
        _primed = false;

        Validation.Assert(_tcsPlay == null);
        if (pausing)
            _tcsPause = CreateTcs();
        _state = RunnerState.Playing;
        _worker = Task.Run(WorkAsync);

        AssertState();
    }

    /// <summary>
    /// Enters a "control" block. Must be balanced with a call to <see cref="LeaveControl"/>.
    /// Control is not re-entrant. That is, this throws if control is already "checked out".
    /// This mechanism acts as a sort of throwing 0/1 semaphore. If a client doesn't want this,
    /// it must institute its own serialization.
    /// </summary>
    private void EnterControl()
    {
        if (Interlocked.CompareExchange(ref _control, 1, 0) != 0)
            throw Validation.BugExcept("Invalid attempt to enter control");
    }

    /// <summary>
    /// Must balance a call to <see cref="EnterControl"/>.
    /// </summary>
    private void LeaveControl()
    {
        if (Interlocked.CompareExchange(ref _control, 0, 1) != 1)
            throw Validation.BugExcept("Invalid attempt to leave control");
    }

    public sealed override Task PrimeAsync()
    {
        EnterControl();
        try
        {
            lock (_lockState)
            {
                AssertState();
                switch (_state)
                {
                case RunnerState.None:
                    Init(pausing: true);
                    break;
                case RunnerState.Playing:
                    break;
                case RunnerState.Paused:
                    Validation.Assert(_primed);
                    return Task.CompletedTask;
                default:
                    Validation.Assert(IsDone());
                    return Task.CompletedTask;
                }
                Validation.Assert(HasInited());

                if (_primed)
                    return Task.CompletedTask;
                if (_tcsPrime == null)
                    _tcsPrime = CreateTcs();
                Validation.Assert(!_tcsPrime.Task.IsCompleted);
                return _tcsPrime.Task;
            }
        }
        finally
        {
            LeaveControl();
        }
    }

    public sealed override void Play()
    {
        EnterControl();
        try
        {
            lock (_lockState)
            {
                AssertState();
                switch (_state)
                {
                case RunnerState.None:
                    Init(pausing: false);
                    return;

                case RunnerState.Playing:
                    if (_tcsPause != null && !_abort)
                        throw Validation.BugExcept("Invalid Play while pausing");
                    return;

                case RunnerState.Paused:
                    Validation.Assert(_tcsPause == null);
                    Validation.Assert(_tcsPlay != null);
                    Validation.Assert(!_tcsPlay.Task.IsCompleted);

                    _state = RunnerState.Playing;
                    CompleteTcs(ref _tcsPlay);

                    AssertState();
                    return;

                default:
                    Validation.Assert(IsDone());
                    return;
                }
            }
        }
        finally
        {
            LeaveControl();
        }
    }

    public sealed override Task PauseAsync()
    {
        EnterControl();
        try
        {
            lock (_lockState)
            {
                AssertState();

                switch (_state)
                {
                case RunnerState.None:
                    // Start it up to get to a proper paused state.
                    Init(pausing: true);
                    Validation.Assert(_state == RunnerState.Playing);
                    Validation.Assert(_tcsPause != null);
                    Validation.Assert(!_tcsPause.Task.IsCompleted);
                    return _tcsPause.Task;

                case RunnerState.Playing:
                    if (_abort)
                    {
                        // Wait on the abort so the external state isn't "Playing".
                        return _worker;
                    }
                    // Create a new tcs only if there isn't already one.
                    if (_tcsPause == null)
                        _tcsPause = CreateTcs();
                    Validation.Assert(!_tcsPause.Task.IsCompleted);
                    return _tcsPause.Task;

                case RunnerState.Paused:
                    Validation.Assert(_tcsPause == null);
                    return Task.CompletedTask;

                default:
                    Validation.Assert(IsDone());
                    return Task.CompletedTask;
                }
            }
        }
        finally
        {
            LeaveControl();
        }
    }

    /// <summary>
    /// Ensures that the action runner is playing and returns a system task that completes when this
    /// action runner finished or is aborted.
    /// </summary>
    public sealed override Task WaitAsync()
    {
        Play();
        _waiting = true;
        Validation.Assert(_worker != null || _state == RunnerState.Done);
        return _worker ?? Task.CompletedTask;
    }

    public sealed override void BeginAbort()
    {
        BeginAbortCore();
    }

    /// <summary>
    /// This initiates aborting.
    /// </summary>
    private void BeginAbortCore()
    {
        EnterControl();
        try
        {
            lock (_lockState)
            {
                AssertState();
                switch (_state)
                {
                case RunnerState.None:
                    _ex = new OperationCanceledException("Aborted");
                    _state = RunnerState.Done;
                    break;

                case RunnerState.Playing:
                    Validation.Assert(_tcsPlay == null);
                    _abort = true;
                    break;

                case RunnerState.Paused:
                    // Set to aborting and unblock the worker thread. Note that the wrapper won't
                    // allow the thread to do any additional work, so this odd state is short lived.
                    Validation.Assert(_tcsPlay != null);
                    Validation.Assert(!_tcsPlay.Task.IsCompleted);

                    _tcsPause = null;
                    _abort = true;

                    _state = RunnerState.Playing;
                    CompleteTcs(ref _tcsPlay);

                    break;

                default:
                    Validation.Assert(IsDone());
                    break;
                }
                AssertState();
            }
        }
        finally
        {
            LeaveControl();
        }
    }

    public sealed override Task AbortAsync()
    {
        BeginAbortCore();
        _waiting = true;
        return _worker ?? Task.CompletedTask;
    }

    public sealed override void Poke()
    {
        // REVIEW: Should this enter control or grab the state lock?
        PokeCore();
    }

    protected virtual void PokeCore()
    {
    }

    /// <summary>
    /// This is the worker. It is carefully crafted to be consistent with the
    /// control methods.
    /// </summary>
    private async Task WorkAsync()
    {
        try
        {
            // Allow aborting before priming.
            await YieldAsync().ConfigureAwait(false);

            await PrimeCoreAsync().ConfigureAwait(false);

            Validation.Assert(!_primeCoreCalled);
            Validation.Assert(!_primed);
            _primeCoreCalled = true;

            // Allow pausing and handle priming.
            await YieldAsync().ConfigureAwait(false);

            // Do the main work.
            await RunCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _ex = Cleanse(ex);
        }
        finally
        {
            // Do cleanup.
            try
            {
                await CleanupCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                lock (_lockState)
                {
                    // Mark as done and unblock control threads.
                    _state = RunnerState.Done;
                    if (_tcsPause != null)
                        CompleteTcs(ref _tcsPause);
                    if (_tcsPrime != null)
                        CompleteTcs(ref _tcsPrime);
                }
            }
        }
    }

    /// <summary>
    /// This is the core logic for state transitions for yielding. It is used by <see cref="Yield"/>,
    /// <see cref="YieldAsync"/>, and <see cref="YieldCoreAsync"/>. This returns <c>null</c> if the paused
    /// state was <i>not</i> entered. Otherwise, it returns a <see cref="TaskCompletionSource"/> that will
    /// be completed when playing should resume (when the paused state is ended). The tcs is also
    /// stored in the <see cref="_tcsPlay"/> field. A controlling thread can then complete this and set the
    /// field back to <c>null</c>. This returns a <see cref="TaskCompletionSource"/> rather than a
    /// <see cref="Task"/> mostly to avoid confusing this as a standard async method. It is not. Standard
    /// async methods don't return <c>null</c>. Note that caller logic is different when this returns <c>null</c>
    /// vs when it returns a tcs that has already been completed. In the former case, the yield is "done".
    /// In the latter, the caller loops and calls this again.
    /// </summary>
    private TaskCompletionSource YieldStateLogic()
    {
        lock (_lockState)
        {
            AssertState();

            // Note: we need to update the _primed flag and complete the primed tcs in the same
            // state lock as moving to paused to avoid subtle race conditions. Also, if we're
            // "priming", we need to complete the tcs _after_ we've updated the state. Hence, these
            // are done in a finally.
            bool setPrimed = !_primed && _primeCoreCalled;
            try
            {
                switch (_state)
                {
                case RunnerState.None:
                    Validation.Assert(false);
                    return null;

                case RunnerState.Playing:
                    Validation.Assert(_tcsPlay == null);
                    if (_abort)
                        throw new OperationCanceledException("Aborted");
                    if (_tcsPause == null)
                    {
                        // Nothing to do.
                        return null;
                    }
                    Validation.Assert(!_tcsPause.Task.IsCompleted);

                    // Don't pause before we are primed.
                    if (!(_primed | setPrimed))
                        return null;
                    break;

                case RunnerState.Paused:
                    // This is the only code that sets the state to paused (below), so we can't be paused.
                    throw Validation.BugExcept("Already paused in yield!");

                default:
                    // The done state is only set in the finally for the thread proc, so
                    // this should never happen.
                    Validation.Assert(false);
                    return null;
                }

                // This needs to happen before the paused tcs is completed.
                // When we're transitioning to "primed", this needs to happen before the
                // primed flag and tcs are set.
                _state = RunnerState.Paused;
            }
            finally
            {
                if (setPrimed)
                {
                    _primed = true;
                    if (_tcsPrime != null)
                        CompleteTcs(ref _tcsPrime);
                }
            }

            // Create a new play tcs and complete the pause tcs, which release controlling threads that are
            // waiting on pause. Controlling threads can then complete the play tcs, but only after grabbing
            // the state lock, so it is safest to keep this in this order and inside the state lock.
            // NOTE: we return _tcsPlay inside the state lock, since once we leave the lock _tcsPlay
            // may be set to null by a control thread.
            _tcsPlay = CreateTcs();
            CompleteTcs(ref _tcsPause);
            return _tcsPlay;
        }
    }

    /// <summary>
    /// Enters a yield block. Must be balanced with a call to <see cref="LeaveYield"/>.
    /// Yield is not re-entrant. That is, this throws if yield is already "checked out".
    /// </summary>
    private void EnterYield()
    {
        if (Interlocked.CompareExchange(ref _yieldControl, 1, 0) != 0)
            throw Validation.BugExcept("Invalid attempt to enter yield");
    }

    /// <summary>
    /// Must balance a call to <see cref="EnterYield"/>.
    /// </summary>
    private void LeaveYield()
    {
        if (Interlocked.CompareExchange(ref _yieldControl, 0, 1) != 1)
            throw Validation.BugExcept("Invalid attempt to leave yield");
    }

    /// <summary>
    /// Typically used in non-async code called by the <see cref="RunCoreAsync"/> method to support
    /// pausing and aborting. For example, when a callback from deeply nested code allows yielding.
    /// For async code, use <see cref="YieldAsync"/>.
    /// REVIEW: When/if all sub-classes are fully async, remove this and supporting functionality.
    /// </summary>
    protected void Yield()
    {
        EnterYield();
        try
        {
            Interlocked.Increment(ref _yieldCount);

            for (; ; )
            {
                TaskCompletionSource tcsPlay = YieldStateLogic();
                if (tcsPlay == null)
                    return;

                // Wait until we're playing again.
                tcsPlay.Task.Wait();

                // Whoever completed the play tcs should have cleared the paused tcs. Since this is the only
                // code that completes the paused tcs and sets the state to paused, and this code isn't re-entrant,
                // we can assert here.
                Validation.Assert(_state != RunnerState.Paused);
                Validation.Assert(_tcsPause == null || !_tcsPause.Task.IsCompleted);
            }
        }
        finally
        {
            LeaveYield();
        }
    }

    /// <summary>
    /// Called by the <see cref="RunCoreAsync"/> method to support pausing and aborting.
    /// </summary>
    protected Task YieldAsync()
    {
        EnterYield();
        TaskCompletionSource tcsPlay;
        bool leave = true;

        try
        {
            Interlocked.Increment(ref _yieldCount);
            tcsPlay = YieldStateLogic();
            if (tcsPlay == null)
                return Task.CompletedTask;
            leave = false;
        }
        finally
        {
            if (leave)
                LeaveYield();
        }

        return YieldCoreAsync(tcsPlay);
    }

    /// <summary>
    /// This is the "core" for <see cref="YieldAsync"/>. It is not used bye <see cref="Yield"/>. This uses
    /// <c>async</c> while <see cref="YieldAsync"/> does not. That is intentional. This is similar to how
    /// a method returning <c>IEnumerable</c> may handle some cases directly before forwarding to a core
    /// method implemented using <c>yield return</c>.
    /// 
    /// Note that <see cref="PrimeAsync"/> and <see cref="PrimeCoreAsync"/> do NOT have a similar relationship.
    /// IMO this method should more correctly be named <c>YieldAsyncCore</c> but that violates the <c>Async</c>
    /// suffix convention.
    /// </summary>
    private async Task YieldCoreAsync(TaskCompletionSource tcsPlay)
    {
        Validation.Assert(_yieldControl == 1);
        try
        {
            Validation.AssertValue(tcsPlay);

            for (; ; )
            {
                // Wait until we're playing again.
                await tcsPlay.Task.ConfigureAwait(false);

                // Whoever completed the play tcs should have cleared the paused tcs. Since this is the only
                // code that completes the paused tcs and sets the state to paused, and yield isn't re-entrant,
                // we can assert here.
                Validation.Assert(_state != RunnerState.Paused);
                Validation.Assert(_tcsPause == null || !_tcsPause.Task.IsCompleted);

                tcsPlay = YieldStateLogic();
                if (tcsPlay == null)
                    return;
            }
        }
        finally
        {
            LeaveYield();
        }
    }

    /// <summary>
    /// Implemented by the sub-class and called by the base class to prime (initialize) the runner.
    /// The purpose of priming is to publish initial "streaming" results.
    /// </summary>
    protected virtual Task PrimeCoreAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Implemented by the sub-class and called by the base class to perform the execution. This should
    /// call <see cref="YieldAsync"/> and/or <see cref="Yield "/>regularly to support pausing and aborting.
    /// </summary>
    protected abstract Task RunCoreAsync();

    /// <summary>
    /// Implemented by the sub-class and called by the base class to clean up or release
    /// resources. This is called whether the task succeeds or fails.
    /// </summary>
    protected virtual Task CleanupCoreAsync()
    {
        return Task.CompletedTask;
    }
}
