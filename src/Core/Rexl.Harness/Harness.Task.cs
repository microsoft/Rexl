// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Harness;

// This partial contains task related enums and utility classes, together with related
// static functions.
partial class HarnessBase
{
    /// <summary>
    /// Indicates the kind of task execution to perform. This expands on the values in
    /// <see cref="TaskState"/> to include a couple additional values, namely <see cref="None"/>,
    /// meaning that a task is not involved, and <see cref="Primary"/>, meaning that the
    /// task should be played to completion and the primary result should be used as the value.
    /// </summary>
    protected enum TaskExecKind : byte
    {
        /// <summary>
        /// Not a task.
        /// </summary>
        None,

        /// <summary>
        /// Finish and produce the primary result as the value.
        /// </summary>
        Primary,

        /// <summary>
        /// Create the task but don't start it.
        /// </summary>
        Create,

        /// <summary>
        /// Initialize enough to where "streaming" results are available. This should
        /// <b>not</b> require inputs to be unblocked (playing). That is, it should <b>not</b>
        /// call <see cref="IEnumerator.MoveNext"/> on inputs.
        /// </summary>
        Prime,

        /// <summary>
        /// Play asynchronously.
        /// </summary>
        Play,

        /// <summary>
        /// Pause if playing.
        /// </summary>
        Pause,

        /// <summary>
        /// Play to the end synchronously.
        /// </summary>
        Finish,

        /// <summary>
        /// Abort the task, if it is not yet finished.
        /// </summary>
        Abort,

        /// <summary>
        /// Poke the task.
        /// </summary>
        Poke,

        /// <summary>
        /// Test whether the task is done and update its state accordingly.
        /// </summary>
        Poll,
    }

    /// <summary>
    /// Map from <see cref="TokKind"/> to <see cref="TaskExecKind"/>.
    /// </summary>
    protected static TaskExecKind GetTek(TokKind tid)
    {
        Validation.Assert(tid.IsTaskModifier() || tid == TokKind.None);
        switch (tid)
        {
        case TokKind.KtxTask: return TaskExecKind.Create;
        case TokKind.KtxPrime: return TaskExecKind.Prime;
        case TokKind.KtxPlay: return TaskExecKind.Play;
        case TokKind.KtxPause: return TaskExecKind.Pause;
        case TokKind.KtxPoke: return TaskExecKind.Poke;
        case TokKind.KtxPoll: return TaskExecKind.Poll;
        case TokKind.KtxFinish: return TaskExecKind.Finish;
        case TokKind.KtxAbort: return TaskExecKind.Abort;
        }
        Validation.Assert(tid == TokKind.None);
        return TaskExecKind.Primary;
    }

    /// <summary>
    /// The "static" status of a task/runner. Note that these are strictly according to the commands that have
    /// been applied to the task and NOT from querying the task for whether it is done. That is,
    /// <list type="bullet">
    /// <item>"task" creates the task and puts it in state <see cref="Created"/>. This non-command requires
    ///   a definition for the task. That is, we don't support <c>task Name</c>, it must be
    ///   <c>task Name as X(...)</c> or an inline task definition.</item>
    /// <item>"finish" waits for the task to complete and puts it in state <see cref="Finished"/>.</item>
    /// <item>"play" starts the task and puts it in state <see cref="Playing"/>, unless it is already in state
    ///   <see cref="Finished"/>, in which case it does nothing.</item>
    /// <item>"pause" pauses the task and puts it in state <see cref="Paused"/>, unless it is already in state
    ///   <see cref="Finished"/>, in which case it does nothing.</item>
    /// </list>
    /// This is distinct from <see cref="RunnerState"/> because it tracks whether a task is "forced playing"
    /// or "forced paused". When a "consumer" task uses streaming result from a "producer" task, the producer
    /// cannot be paused while the consumer is playing.
    /// </summary>
    protected enum TaskState : byte
    {
        /// <summary>
        /// The task has been created but not started.
        /// </summary>
        Created,

        /// <summary>
        /// The task is playing.
        /// </summary>
        Playing,

        /// <summary>
        /// The task is playing but would prefer to be paused.
        /// </summary>
        PlayingForced,

        /// <summary>
        /// The task is paused.
        /// </summary>
        Paused,

        /// <summary>
        /// The task is paused but would rather be playing.
        /// </summary>
        PausedForced,

        /// <summary>
        /// The task has finished, perhaps unsuccessfully. This is the terminal state.
        /// That is, this state is never changed to another.
        /// </summary>
        Finished,
    }

    /// <summary>
    /// Map <see cref="TaskState"/> to <see cref="RunnerState"/>.
    /// </summary>
    protected static RunnerState ToRunnerState(TaskState state)
    {
        switch (state)
        {
        case TaskState.Created:
            return RunnerState.None;
        case TaskState.Paused:
        case TaskState.PausedForced:
            return RunnerState.Paused;
        case TaskState.Playing:
        case TaskState.PlayingForced:
            return RunnerState.Playing;
        default:
            Validation.Assert(state == TaskState.Finished);
            return RunnerState.Done;
        }
    }

    /// <summary>
    /// Whether the given <paramref name="state"/> is for "playing", whether forced or not.
    /// </summary>
    protected static bool IsPlaying(TaskState state)
    {
        switch (state)
        {
        case TaskState.Playing:
        case TaskState.PlayingForced:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the given <paramref name="state"/> is for "finished" or "playing", whether forced or not.
    /// </summary>
    protected static bool IsPlayingOrDone(TaskState state)
    {
        switch (state)
        {
        case TaskState.Playing:
        case TaskState.PlayingForced:
        case TaskState.Finished:
            return true;
        }
        return false;
    }

    /// <summary>
    /// Stores information for a task state change.
    /// </summary>
    protected sealed class TaskStateChange
    {
        public ActionRunner Task { get; }
        public TaskState Prev { get; }
        public TaskState Cur { get; }

        public TaskStateChange(ActionRunner task, TaskState prev, TaskState cur)
        {
            Validation.AssertValue(task);
            Validation.Assert(prev != TaskState.Finished | cur == TaskState.Finished);
            Validation.Assert(cur != TaskState.Created);
            Task = task;
            Prev = prev;
            Cur = cur;
        }
    }
}

// This partial is for tracking tasks, their states, and their inter-dependencies.
partial class HarnessBase
{
    /// <summary>
    /// The tasks that we are currently tracking, in creation order.
    /// </summary>
    private readonly List<ActionRunner> _tasks;

    /// <summary>
    /// The tasks that we are tracking that have not yet been primed.
    /// </summary>
    private readonly HashSet<ActionRunner> _tasksNeedPrime;

    /// <summary>
    /// The state of each task that we are tracking.
    /// </summary>
    private readonly Dictionary<ActionRunner, TaskState> _taskStates;

    /// <summary>
    /// The dependencies between tasks.
    /// REVIEW: If needed for perf, use a lookup mechanism rather than a flat list.
    /// </summary>
    private readonly List<(ActionRunner producer, ActionRunner consumer)> _taskDeps;

    /// <summary>
    /// The tasks, aka ActionRunners, keyed by name/path. Note that some names may map
    /// to <c>null</c>.
    /// </summary>
    private readonly Dictionary<NPath, ActionRunner> _nameToTask;

    /// <summary>
    /// Accumulates task state change information.
    /// </summary>
    private List<TaskStateChange> _taskStateChanges;

    /// <summary>
    /// Whether the given name is a task (aka action runner).
    /// </summary>
    protected bool HasTask(NPath name) => _nameToTask.ContainsKey(name);

    /// <summary>
    /// Whether the given <see cref="ActionRunner"/> is currently being tracked.
    /// </summary>
    protected bool HasTask(ActionRunner runner) => _tasks.Contains(runner);

    /// <summary>
    /// If there is a task with the given name, sets <paramref name="runner"/> to it and returns true.
    /// NOTE: This may return true but set <paramref name="runner"/> to <c>null</c>, for example
    /// in the <see cref="HandleTaskBad"/> case.
    /// REVIEW: Should this return false when the runner associated with the name is <c>null</c>?
    /// </summary>
    protected bool TryGetTask(NPath name, out ActionRunner runner)
    {
        return _nameToTask.TryGetValue(name, out runner);
    }

    /// <summary>
    /// Gets the current task names and states, sorted by name.
    /// </summary>
    public Immutable.Array<(NPath name, RunnerState state)> GetTaskInfos()
    {
        return _nameToTask
            .Where(kvp => kvp.Value != null && _taskStates.ContainsKey(kvp.Value))
            .OrderBy(kvp => kvp.Key, NPathComparer.Instance)
            .Select(kvp => (kvp.Key, ToRunnerState(_taskStates[kvp.Value])))
            .ToImmutableArray();
    }

    /// <summary>
    /// Whether the <paramref name="runner"/> is statically known to be primed (or done).
    /// </summary>
    protected bool IsPrimed(ActionRunner runner)
    {
        Validation.AssertValue(runner);
        return !_tasksNeedPrime.Contains(runner);
    }

    /// <summary>
    /// Mark that the <paramref name="runner"/> is staticlly known to be primed.
    /// </summary>
    protected void SetPrimed(ActionRunner runner)
    {
        Validation.AssertValue(runner);
        _tasksNeedPrime.Remove(runner);
    }

    /// <summary>
    /// Looks for a task with name <paramref name="task"/> and an item in it with name <paramref name="item"/>.
    /// If found, sets <paramref name="type"/> and <paramref name="isStream"/> accordingly and returns true.
    /// REVIEW: Should support item name being <see cref="NPath"/>.
    /// </summary>
    protected virtual bool TryGetTaskItemType(NPath task, DName item, out DType type, out bool isStream)
    {
        if (_nameToTask.TryGetValue(task, out var runner) && runner != null)
        {
            _taskStates.TryGetValue(runner, out var state).Verify();
            if (runner.TryGetResultFromName(item, out var res) &&
                (!IsPlaying(state) || res.IsStable && IsPrimed(runner)))
            {
                Validation.Assert(res.Type.IsValid);
                type = res.Type;
                isStream = res.IsStreaming && state != TaskState.Finished;
                return true;
            }
        }

        type = default;
        isStream = false;
        return false;
    }

    /// <summary>
    /// Looks for a task with name <paramref name="task"/> and an item in it whose name is a fuzzy match for
    /// name <paramref name="item"/>. If found, sets <paramref name="itemGuess"/>, <paramref name="type"/>,
    /// and <paramref name="isStream"/> accordingly and returns true.
    /// REVIEW: Should support item name being <see cref="NPath"/>.
    /// </summary>
    protected virtual bool TryGetTaskItemTypeFuzzy(NPath task, DName item,
        out DName itemGuess, out DType type, out bool isStream)
    {
        if (_nameToTask.TryGetValue(task, out var runner) && runner != null)
        {
            _taskStates.TryGetValue(runner, out var state).Verify();
            foreach (var res in runner.GetResultInfos())
            {
                Validation.Assert(res.Type.IsValid);
                if (IsFuzzyMatch(res.Name, item) &&
                    (!IsPlaying(state) || res.IsStable && IsPrimed(runner)))
                {
                    itemGuess = res.Name;
                    type = res.Type;
                    isStream = res.IsStreaming && state != TaskState.Finished;
                    return true;
                }
            }
        }

        itemGuess = default;
        type = default;
        isStream = false;
        return false;
    }

    /// <summary>
    /// A task declaration had some error. If there is already a task with the given path,
    /// its value is being set to <c>null</c>, otherwise, it is not being added.
    /// </summary>
    protected virtual void HandleTaskBad(NPath name, bool settingToNull)
    {
        Validation.Assert(settingToNull == HasTask(name));

        if (!IsVerbose)
            return;

        if (!name.IsRoot)
            Sink.WriteLine(settingToNull ? "Error, setting task to null: {0}" : "Error, not creating task: {0}", name);
    }

    /// <summary>
    /// A task declaration was processed. The <paramref name="overwrite"/> is <c>true</c> if
    /// there is already a task with the given <paramref name="name"/> that will be overwritten.
    /// </summary>
    protected virtual void HandleTaskGood(NPath name, bool overwrite)
    {
        Validation.Assert(overwrite == HasTask(name));

        if (!IsVerbose)
            return;

        if (name.IsRoot)
            return;

        if (overwrite)
            Sink.WriteLine("Overwriting task: {0}", name);
        if (HasGlobal(name))
            Sink.WriteLine("Task name same as global: {0}", name);
        if (HasStandardNamespace(name))
            Sink.WriteLine("Task name same as namespace: {0}", name);

        Sink.WriteLine("Task '{0}' added", name);
    }

    /// <summary>
    /// Processes a task definition, including setting the value in the task runner map.
    /// </summary>
    protected virtual void HandleTask(NPath name, ActionRunner runner)
    {
        Validation.AssertValueOrNull(runner);

        bool existing = _nameToTask.ContainsKey(name);
        if (runner == null)
        {
            HandleTaskBad(name, settingToNull: existing);
            if (existing)
            {
                // REVIEW: Would it be better to remove the task? That's a bit tricky given
                // the current data structures.
                Validation.Assert(!name.IsRoot);
                SetTaskName(name, null);
            }
        }
        else
        {
            HandleTaskGood(name, overwrite: existing);
            if (!name.IsRoot)
                SetTaskName(name, runner);
        }
    }

    /// <summary>
    /// The task state has been updated, typically according to a script statement. The <paramref name="stateNew"/>
    /// and <paramref name="stateOld"/> may be the same, indicating that nothing happened since the task was already
    /// in the desired state. When <paramref name="stateNew"/> is <see cref="TaskState.Finished"/>.
    /// </summary>
    protected virtual void PostStateChange(ActionRunner runner, TaskState stateOld, TaskState stateNew)
    {
        Validation.AssertValue(runner);
        Validation.Assert(stateOld != TaskState.Finished | stateNew == TaskState.Finished);
        Validation.Assert(stateNew != TaskState.Created);

        Util.Add(ref _taskStateChanges, new TaskStateChange(runner, stateOld, stateNew));
    }

    protected virtual void ProcessStateChanges()
    {
        if (Util.Size(_taskStateChanges) == 0)
            return;

        foreach (var tsc in _taskStateChanges)
        {
            NPath name = default;
            foreach (var kvp in _nameToTask)
            {
                if (kvp.Value == tsc.Task)
                {
                    name = kvp.Key;
                    break;
                }
            }

            HandleTaskStateUpdate(name, tsc.Prev, tsc.Cur);
        }

        _taskStateChanges.Clear();
    }

    /// <summary>
    /// The task state has been updated, typically according to a script statement. The <paramref name="stateNew"/>
    /// and <paramref name="stateOld"/> may be the same, indicating that nothing happened since the task was already
    /// in the desired state. When <paramref name="stateNew"/> is <see cref="TaskState.Finished"/>.
    /// </summary>
    protected virtual void HandleTaskStateUpdate(NPath name, TaskState stateOld, TaskState stateNew)
    {
        Validation.Assert(name.IsRoot || HasTask(name));
        Validation.Assert(stateOld != TaskState.Finished | stateNew == TaskState.Finished);
        Validation.Assert(stateNew != TaskState.Created);

        if (!IsVerbose)
            return;

        WriteTaskStateUpdateMessage(name, stateOld, stateNew);
    }

    protected virtual void WriteTaskStateUpdateMessage(NPath name, TaskState stateOld, TaskState stateNew)
    {
        Validation.Assert(name.IsRoot || HasTask(name));

        string msg = null;
        switch (stateNew)
        {
        case TaskState.Playing:
            switch (stateOld)
            {
            case TaskState.Playing:
                msg = "already playing";
                break;
            default:
                msg = "now playing";
                break;
            }
            break;
        case TaskState.PlayingForced:
            switch (stateOld)
            {
            case TaskState.Playing:
            case TaskState.PlayingForced:
                break;
            default:
                msg = "forced playing";
                break;
            }
            break;
        case TaskState.Paused:
            switch (stateOld)
            {
            case TaskState.Paused:
                msg = "already paused";
                break;
            default:
                msg = "now paused";
                break;
            }
            break;
        case TaskState.PausedForced:
            switch (stateOld)
            {
            case TaskState.Paused:
            case TaskState.PausedForced:
                break;
            default:
                msg = "forced paused";
                break;
            }
            break;
        case TaskState.Finished:
            if (stateOld != TaskState.Finished)
                msg = "finished";
            else
                msg = "already finished";
            break;
        }

        if (msg is null)
            return;

        if (name.IsRoot)
            Sink.Write($"<Anonymous task> ");
        else
            Sink.Write("Task '{0}' ", name);
        Sink.WriteLine(msg);
    }

    /// <summary>
    /// Associates the given <paramref name="runner"/> with the given <paramref name="name"/>.
    /// Note that this does <e>not</e> call <see cref="HandleTask"/>.
    /// </summary>
    private void SetTaskName(NPath name, ActionRunner runner)
    {
        Validation.Assert(!name.IsRoot);
        Validation.AssertValueOrNull(runner);
        if (_nameToTask.TryGetValue(name, out var cur) && cur != null)
        {
            Validation.Assert(runner != cur);
            _nameToTask[name] = null;
            // REVIEW: What should we do?
            // ForgetTask(cur);
        }
        _nameToTask[name] = runner;
        UpdateNamespaces(name);
    }

    /// <summary>
    /// Process a task command statement.
    /// </summary>
    protected virtual async Task<bool> ProcessTaskCmdAsync(TaskExecKind tek, IdentPath path, NPath name)
    {
        Validation.AssertValue(path);
        Validation.Assert(!name.IsRoot);

        if (!_nameToTask.TryGetValue(name, out var runner))
        {
            StmtError(path.Last.Token, ErrorStrings.ErrTaskUnknown);
            return false;
        }

        if (runner == null)
            return true;

        await ApplyTaskExecKindAsync(runner, tek).ConfigureAwait(false);
        ProcessStateChanges();
        return true;
    }

    private async Task ApplyTaskExecKindAsync(ActionRunner runner, TaskExecKind tek)
    {
        Validation.AssertValue(runner);

        _taskStateChanges?.Clear();
        _taskStates.TryGetValue(runner, out var state).Verify();

        switch (state)
        {
        case TaskState.Finished:
            // Already finished.
            Validation.Assert(IsPrimed(runner));
            PostStateChange(runner, TaskState.Finished, TaskState.Finished);
            return;
        case TaskState.Paused:
        case TaskState.PausedForced:
            Validation.Assert(runner.IsPrimed);
            Validation.Assert(IsPrimed(runner));
            break;
        case TaskState.Playing:
        case TaskState.PlayingForced:
            break;
        case TaskState.Created:
            Validation.Assert(IsPrimed(runner) == runner.IsPrimed);
            break;

        default:
            Validation.Assert(false);
            break;
        }

        switch (tek)
        {
        case TaskExecKind.Create:
            break;
        case TaskExecKind.Prime:
            await PrimeTaskCoreAsync(runner).ConfigureAwait(false);
            break;
        case TaskExecKind.Play:
            PlayTaskCore(runner, forced: false);
            break;
        case TaskExecKind.Pause:
            await PauseTaskCoreAsync(runner, forced: false).ConfigureAwait(false);
            break;
        case TaskExecKind.Primary:
        case TaskExecKind.Finish:
        case TaskExecKind.Abort:
            if (tek != TaskExecKind.Abort)
            {
                PlayTaskCore(runner, forced: false);
                await runner.WaitAsync().ConfigureAwait(false);
                await SetTaskStateFinishedAsync(runner).ConfigureAwait(false);
            }
            else
                await AbortTaskCoreAsync(runner).ConfigureAwait(false);

            // Note that when aborting, we can't/shouldn't assert that the runner failed
            // since it may have finished before we called Abort.
            Validation.Assert(runner.State == RunnerState.Done);
            break;
        case TaskExecKind.Poke:
            runner.Poke();
            break;
        case TaskExecKind.Poll:
            // Note that runner.State is volatile.
            switch (runner.State)
            {
            case RunnerState.Done:
                await SetTaskStateFinishedAsync(runner).ConfigureAwait(false);
                break;
            case RunnerState.Paused:
                Validation.Assert(runner.IsPrimed);
                SetPrimed(runner);
                break;
            case RunnerState.Playing:
                Validation.Assert(IsPlaying(state));
                if (runner.IsPrimed)
                    SetPrimed(runner);
                break;
            }
            break;

        default:
            Validation.Assert(false, "Unhandled task command");
            break;
        }
    }

    private string StateToStr(TaskState state)
    {
        switch (state)
        {
        case TaskState.Created:
            return "Created";
        case TaskState.Playing:
        case TaskState.PlayingForced:
            return "Playing";
        case TaskState.Paused:
        case TaskState.PausedForced:
            return "Paused";
        case TaskState.Finished:
            return "Finished";
        }

        Validation.Assert(false);
        return null;
    }

    private string StateToStr(RunnerState state)
    {
        switch (state)
        {
        case RunnerState.None: return "Created";
        case RunnerState.Playing: return "Playing";
        case RunnerState.Paused: return "Paused";
        case RunnerState.Done: return "Finished";
        }

        Validation.Assert(false);
        return null;
    }

    /// <summary>
    /// Look for a meta-property with the given <paramref name="name"/> on the task with name
    /// <paramref name="path"/>.
    /// </summary>
    protected virtual bool TryGetMetaProp(NPath path, DName name, out BoundNode bnd)
    {
        if (!_nameToTask.TryGetValue(path, out var runner))
        {
            bnd = null;
            return false;
        }

        var state = runner != null ? _taskStates[runner] : TaskState.Finished;

        switch (name.Value)
        {
        case "State":
            bnd = BndStrNode.Create(StateToStr(state));
            return true;
        case "RealTimeState":
            bnd = BndStrNode.Create(runner != null ? StateToStr(runner.State) : null);
            return true;
        case "Finished":
            bnd = BndIntNode.CreateBit(state == TaskState.Finished);
            return true;
        case "Failed":
            bnd = BndIntNode.CreateBit(state == TaskState.Finished && (runner == null || !runner.WasSuccessful));
            return true;
        case "ErrorMessage":
            if (state != TaskState.Finished)
                bnd = BndStrNode.Null;
            else if (runner == null)
                bnd = BndStrNode.Create("Runner creation failed");
            else if (runner.WasSuccessful)
                bnd = BndStrNode.Null;
            else
                bnd = BndStrNode.Create(runner.GetErrorMessage());
            return true;
        case "ResultNames":
            if (runner != null)
            {
                if (!IsPlaying(state))
                {
                    // All results are visible.
                    var infos = runner.GetResultInfos();
                    int count = infos.Length;
                    if (count > 0)
                    {
                        var names = Immutable.Array.CreateBuilder<BoundNode>(count, init: true);
                        for (int i = 0; i < count; i++)
                            names[i] = BndStrNode.Create(infos[i].Name.Value);
                        bnd = BndTupleNode.Create(names.ToImmutable());
                        return true;
                    }
                }
                else if (IsPrimed(runner))
                {
                    // Only stable results are visible.
                    var infos = runner.GetResultInfos();
                    int count = infos.Length;
                    if (count > 0)
                    {
                        var names = Immutable.Array.CreateBuilder<BoundNode>(count);
                        foreach (var info in infos)
                        {
                            if (info.IsStable)
                                names.Add(BndStrNode.Create(info.Name.Value));
                        }
                        bnd = BndTupleNode.Create(names.ToImmutable());
                        return true;
                    }
                }
            }
            bnd = BndTupleNode.Create(Immutable.Array<BoundNode>.Empty);
            return true;

        default:
            bnd = null;
            return false;
        }
    }

    /// <summary>
    /// Track a task's static state and dependencies.
    /// </summary>
    private void TrackTask(ActionRunner runner)
    {
        Validation.AssertValue(runner);
        Validation.Assert(!_tasks.Contains(runner));
        Validation.Assert(!_tasksNeedPrime.Contains(runner));
        Validation.Assert(!_taskStates.ContainsKey(runner));
        _tasks.Add(runner);
        if (!runner.IsPrimed)
            _tasksNeedPrime.Add(runner);
        _taskStates[runner] = TaskState.Created;
    }

    private void ClearTaskDeps(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        int ivDst = 0;
        for (int ivSrc = 0; ivSrc < _taskDeps.Count; ivSrc++)
        {
            Validation.AssertIndexInclusive(ivDst, ivSrc);
            var pair = _taskDeps[ivSrc];
            if (pair.producer != runner && pair.consumer != runner)
            {
                if (ivDst < ivSrc)
                    _taskDeps[ivDst] = pair;
                ivDst++;
            }
        }

        if (ivDst < _taskDeps.Count)
            _taskDeps.RemoveRange(ivDst, _taskDeps.Count - ivDst);
    }

    /// <summary>
    /// Marks the task as <see cref="TaskState.Playing"/> or <see cref="TaskState.PlayingForced"/>.
    /// Also calls <see cref="PostStateChange(ActionRunner, TaskState, TaskState)"/>.
    /// Asserts that no up-stream tasks are paused.
    /// </summary>
    private void SetTaskStatePlaying(ActionRunner runner, bool forced)
    {
        Validation.AssertValue(runner);
        Validation.Assert(_taskStates.ContainsKey(runner));

        var stateCur = _taskStates[runner];
        Validation.Assert(stateCur != TaskState.Finished);

#if DEBUG
        // Verify the invariant that if a consumer is playing the producer must be playing or done.
        // Any producers must be playing or done.
        foreach (var pair in _taskDeps)
        {
            if (pair.consumer == runner)
            {
                _taskStates.TryGetValue(pair.producer, out var st).Verify();
                Validation.Assert(IsPlayingOrDone(st));
            }
        }
#endif

        if (forced && stateCur == TaskState.PausedForced)
            forced = false;

        var stateNew = forced ? TaskState.PlayingForced : TaskState.Playing;
        _taskStates[runner] = stateNew;
        PostStateChange(runner, stateCur, stateNew);
    }

    /// <summary>
    /// Markes the task as <see cref="TaskState.Paused"/> or <see cref="TaskState.PausedForced"/>.
    /// Also calls <see cref="PostStateChange(ActionRunner, TaskState, TaskState)"/>.
    /// Asserts that no down-stream tasks are playing.
    /// </summary>
    private void SetTaskStatePaused(ActionRunner runner, bool forced)
    {
        Validation.AssertValue(runner);
        Validation.Assert(_taskStates.ContainsKey(runner));

        var stateCur = _taskStates[runner];
        Validation.Assert(stateCur != TaskState.Finished);

#if DEBUG
        // Verify the invariant that if a consumer is playing the producer must be playing or done.
        // Any consumers must not be playing.
        foreach (var pair in _taskDeps)
        {
            if (pair.producer == runner)
            {
                _taskStates.TryGetValue(pair.consumer, out var st).Verify();
                Validation.Assert(!IsPlaying(st));
            }
        }
#endif

        Validation.Assert(runner.IsPrimed);
        SetPrimed(runner);

        if (forced && stateCur == TaskState.PlayingForced)
            forced = false;

        var stateNew = forced ? TaskState.PausedForced : TaskState.Paused;
        _taskStates[runner] = stateNew;
        PostStateChange(runner, stateCur, stateNew);
    }

    /// <summary>
    /// Marks the task a finished. Also allows up-stream tasks to pause and down-stream task to play and
    /// removes dependencies that are no longer needed. This is async to allow up-stream tasks to pause.
    /// </summary>
    private async Task SetTaskStateFinishedAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);
        Validation.Assert(_taskStates.ContainsKey(runner));

        var stateCur = _taskStates[runner];

        // Finished things are considered primed.
        SetPrimed(runner);

        _taskStates[runner] = TaskState.Finished;
        PostStateChange(runner, stateCur, TaskState.Finished);

        await AllowPauseUpTasksAsync(runner).ConfigureAwait(false);
        AllowPlayDownTasks(runner);

        // Remove instances in the dependencies.
        ClearTaskDeps(runner);
    }

    private bool ForcePlayUpTasks(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        bool any = false;
        foreach (var pair in _taskDeps)
        {
            if (pair.consumer == runner)
            {
                ForcePlayTask(pair.producer);
                any = true;
            }
        }
        return any;
    }

    private bool AllowPlayDownTasks(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        bool any = false;
        foreach (var pair in _taskDeps)
        {
            if (pair.producer == runner)
                any |= AllowPlayTask(pair.consumer);
        }
        return any;
    }

    private async Task<bool> ForcePauseDownTasksAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        // Iterate in reverse order, for slight efficiency.
        bool any = false;
        for (int i = _taskDeps.Count; --i >= 0;)
        {
            var pair = _taskDeps[i];
            if (pair.producer == runner)
                any |= await ForcePauseTaskAsync(pair.consumer).ConfigureAwait(false);
        }
        return any;
    }

    private async Task<bool> AllowPauseUpTasksAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        // Iterate in reverse order, for slight efficiency.
        bool any = false;
        for (int i = _taskDeps.Count; --i >= 0;)
        {
            var pair = _taskDeps[i];
            if (pair.consumer == runner)
                any |= await AllowPauseTaskAsync(pair.producer).ConfigureAwait(false);
        }
        return any;
    }

    /// <summary>
    /// Force the given task to be either playing or done.
    /// </summary>
    private bool ForcePlayTask(ActionRunner runner)
    {
        Validation.AssertValue(runner);
        _taskStates.TryGetValue(runner, out var state).Verify();

        switch (state)
        {
        case TaskState.Created:
        case TaskState.Paused:
            PlayTaskCore(runner, forced: true);
            return true;

        case TaskState.PausedForced:
            PlayTaskCore(runner, forced: false);
            return true;

        case TaskState.Playing:
        case TaskState.PlayingForced:
            break;

        default:
            Validation.Assert(state == TaskState.Finished);
            break;
        }

        return false;
    }

    /// <summary>
    /// If the given task is force-paused, and it can play, play it.
    /// </summary>
    private bool AllowPlayTask(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        _taskStates.TryGetValue(runner, out var state).Verify();
        if (state != TaskState.PausedForced)
            return false;

        // If there are no producers that are paused, this can play.
        foreach (var pair in _taskDeps)
        {
            if (pair.consumer != runner)
                continue;
            _taskStates.TryGetValue(pair.producer, out var st).Verify();
            if (!IsPlayingOrDone(st))
                return false;
        }

        PlayTaskCore(runner, false);
        return true;
    }

    /// <summary>
    /// Force the given task to be either paused or done.
    /// </summary>
    private async Task<bool> ForcePauseTaskAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);
        _taskStates.TryGetValue(runner, out var state).Verify();

        switch (state)
        {
        case TaskState.Created:
            // Created is also ok when forcing to paused.
            return false;

        case TaskState.PlayingForced:
            await PauseTaskCoreAsync(runner, forced: false).ConfigureAwait(false);
            return true;

        case TaskState.Playing:
            await PauseTaskCoreAsync(runner, forced: true).ConfigureAwait(false);
            return true;

        case TaskState.Paused:
        case TaskState.PausedForced:
            break;

        default:
            Validation.Assert(state == TaskState.Finished);
            break;
        }

        return false;
    }

    /// <summary>
    /// If the given task is force-playing, and it can pause, pause it.
    /// </summary>
    private async Task<bool> AllowPauseTaskAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        _taskStates.TryGetValue(runner, out var state).Verify();
        if (state != TaskState.PlayingForced)
            return false;

        // If there are no consumers that are playing, this can pause.
        foreach (var pair in _taskDeps)
        {
            if (pair.producer != runner)
                continue;
            _taskStates.TryGetValue(pair.consumer, out var st).Verify();
            if (IsPlaying(st))
                return false;
        }

        await PauseTaskCoreAsync(runner, forced: false).ConfigureAwait(false);
        return true;
    }

    private void PlayTaskCore(ActionRunner runner, bool forced)
    {
        Validation.AssertValue(runner);

        var state0 = _taskStates[runner];

        // Force play inputs.
        var state1 = state0;
        if (ForcePlayUpTasks(runner))
            state1 = _taskStates[runner];

        TaskState stateNew = forced ? TaskState.PlayingForced : TaskState.Playing;
        if (state1 != stateNew || state0 == state1)
        {
            runner.Play();
            SetTaskStatePlaying(runner, forced);
        }

        // Allow consumers to play.
        AllowPlayDownTasks(runner);
    }

    private async Task PrimeTaskCoreAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        var state = _taskStates[runner];
        switch (state)
        {
        case TaskState.Created:
            {
                Validation.Assert(runner.State == RunnerState.None);

                // Must force upstream tasks to play while priming.
                if (ForcePlayUpTasks(runner))
                    Validation.Assert(_taskStates[runner] == TaskState.Created);

                var t = runner.PrimeAsync();
                SetTaskStatePlaying(runner, forced: false);

                await t.ConfigureAwait(false);
                SetPrimed(runner);
            }
            break;

        case TaskState.PlayingForced:
        case TaskState.Playing:
            if (runner.IsPrimed)
                return;
            await runner.PrimeAsync().ConfigureAwait(false);
            SetPrimed(runner);
            break;

        case TaskState.Paused:
        case TaskState.PausedForced:
            Validation.Assert(runner.IsPrimed);
            return;

        default:
            Validation.Assert(state == TaskState.Finished);
            return;
        }

        // Note that runner.State is volatile.
        switch (runner.State)
        {
        case RunnerState.None:
            Validation.Assert(false, "ActionRunner in unexpected None state");
            break;
        case RunnerState.Playing:
            Validation.Assert(state != TaskState.Created, "ActionRunner playing after initial priming");
            break;
        case RunnerState.Paused:
            Validation.Assert(state == TaskState.Created, "ActionRunner unexpectedly paused after priming");
            SetTaskStatePaused(runner, forced: false);
            // Allow inputs to pause.
            await AllowPauseUpTasksAsync(runner).ConfigureAwait(false);
            break;
        case RunnerState.Done:
            await SetTaskStateFinishedAsync(runner).ConfigureAwait(false);
            break;

        default:
            Validation.Assert(false, "Invalid runner state");
            break;
        }
    }

    private async Task AbortTaskCoreAsync(ActionRunner runner)
    {
        Validation.AssertValue(runner);

        await runner.AbortAsync().ConfigureAwait(false);
        await SetTaskStateFinishedAsync(runner).ConfigureAwait(false);
    }

    private async Task PauseTaskCoreAsync(ActionRunner runner, bool forced)
    {
        Validation.AssertValue(runner);

        var state0 = _taskStates[runner];

        // Force pause consumers.
        var state1 = state0;
        if (await ForcePauseDownTasksAsync(runner).ConfigureAwait(false))
            state1 = _taskStates[runner];

        var stateNew = forced ? TaskState.PausedForced : TaskState.Paused;
        if (state1 != stateNew || state0 == state1)
        {
            await runner.PauseAsync().ConfigureAwait(false);
            SetTaskStatePaused(runner, forced);
        }

        // Allow inputs to pause.
        await AllowPauseUpTasksAsync(runner).ConfigureAwait(false);
    }

    /// <summary>
    /// Call back from the action host to get the current date time offset information.
    /// </summary>
    protected virtual DateTimeOffset Now()
    {
        return DateTimeOffset.Now;
    }

    /// <summary>
    /// Call back from action host to load an existing stream.
    /// </summary>
    protected abstract Task<(Link full, Stream stream)> LoadStreamForTaskAsync(SourceContext sctx, Link link);

    /// <summary>
    /// Call back from action host to create a new stream.
    /// </summary>
    protected abstract Task<(Link full, Stream stream)> CreateStreamForTaskAsync(SourceContext sctx, Link link,
        StreamOptions options = default);

    /// <summary>
    /// Call back from action host to get "files" in a "directory".
    /// </summary>
    protected abstract IEnumerable<Link> GetFilesForTask(SourceContext sctx, Link linkDir, out Link full);

    /// <summary>
    /// Create an action runner for an invocation of a user defined proc. The argument values and types are
    /// passed in <paramref name="with"/> and <paramref name="typeWith"/>.
    /// </summary>
    protected abstract ActionRunner CreateUserProcRunner(UserProc proc, DType typeWith, RecordBase with);

    /// <summary>
    /// The action host implementation for tasks. This mostly delegates to the <see cref="HarnessBase"/>.
    /// </summary>
    public abstract class HarnessActionHost : ActionHost
    {
        private readonly HarnessBase _parent;
        private readonly SourceContext _sctx;

        protected HarnessActionHost(HarnessBase parent, SourceContext sctx)
        {
            Validation.AssertValue(parent);
            Validation.AssertValue(sctx);
            _parent = parent;
            _sctx = sctx;
        }

        public sealed override TypeManager TypeManager => _parent.TypeManager;

        public sealed override Task<(Link full, Stream stream)> LoadStreamAsync(Link link)
        {
            return _parent.LoadStreamForTaskAsync(_sctx, link);
        }

        public sealed override Task<(Link full, Stream stream)> CreateStreamAsync(Link link,
            StreamOptions options = default)
        {
            return _parent.CreateStreamForTaskAsync(_sctx, link, options);
        }

        public sealed override IEnumerable<Link> GetFiles(Link linkDir, out Link full)
        {
            return _parent.GetFilesForTask(_sctx, linkDir, out full);
        }

        public sealed override DateTimeOffset Now()
        {
            return _parent.Now();
        }

        public override ActionRunner CreateUserProcRunner(UserProc proc, DType typeWith, RecordBase with)
        {
            return _parent.CreateUserProcRunner(proc, typeWith, with);
        }
    }

    private sealed class ActionHostImpl : HarnessActionHost
    {
        public ActionHostImpl(HarnessBase parent, SourceContext sctx)
            : base(parent, sctx)
        {
        }
    }
}
