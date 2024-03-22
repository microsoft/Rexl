// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public class ActionRunnerTests
{
    private readonly EnumerableTypeManager _typeManager;
    private readonly EnumerableCodeGeneratorBase _codeGen;
    private readonly BindHost _bindHost;
    private readonly ActionHost _actionHost;

    public ActionRunnerTests()
    {
        // Setup TypeManager and code generators.
        _typeManager = new TestEnumTypeManager();
        _codeGen = new EnumerableCodeGenerator(_typeManager, TestGenerators.Instance);
        _bindHost = new TestOperBindHostImpl();
        _actionHost = new NoopActionHost(_typeManager);
    }

    private Func<ActionRunner> GetMaker(string src)
    {
        var fma = RexlFormula.Create(SourceContext.Create(src));
        Assert.IsFalse(fma.HasDiagnostics);
        var bfma = BoundFormula.Create(fma, _bindHost, BindOptions.AllowImpure);
        Assert.IsFalse(bfma.HasDiagnostics);
        Assert.IsTrue(bfma.BoundTree.IsProcCall);

        var cgr = _codeGen.Run(bfma.BoundTree);
        Assert.AreEqual(0, cgr.Globals.Length);

        var fn = cgr.CreateRunnerFunc;
        Assert.IsNotNull(fn);
        return () =>
        {
            var runner = fn(Array.Empty<object>(), _actionHost);
            Assert.IsNotNull(runner);
            Assert.AreEqual(RunnerState.None, runner.State);
            return runner;
        };
    }

    /// <summary>
    /// Run a <see cref="SyncActionRunner"/> with Wait on a separate thread.
    /// </summary>
    [TestMethod]
    public async Task SyncProcPlay()
    {
        // Wait at most one tick.
        var maker = GetMaker("SyncProc(CastTime(1), Time(0))");

        // Play immediately.
        {
            var runner = maker();

            var t = Task.Run(runner.Play);

            await runner.WaitAsync();
            Assert.AreEqual(RunnerState.Done, runner.State);

            // Await the task as well, although we don't really need to.
            await t;

            Assert.AreEqual(RunnerState.Done, runner.State);
            Assert.IsTrue(runner.WasSuccessful);
            Assert.IsNull(runner.GetException());
            Assert.IsNull(runner.GetErrorMessage());
        }

        // Delay play, so the main thread may win.
        {
            var runner = maker();

            var t = Task.Run(() =>
            {
                Thread.Sleep(10);
                runner.Play();
            });

            await runner.WaitAsync();
            Assert.AreEqual(RunnerState.Done, runner.State);

            // Await the task as well, although we don't really need to.
            await t;

            Assert.AreEqual(RunnerState.Done, runner.State);
            Assert.IsTrue(runner.WasSuccessful);
            Assert.IsNull(runner.GetException());
            Assert.IsNull(runner.GetErrorMessage());
        }
    }

    /// <summary>
    /// "Pause" a <see cref="SyncActionRunner"/> from various states. Even though the runner does not
    /// support the paused state, the pause method needs coverage.
    /// </summary>
    [TestMethod]
    public async Task SyncProcPause()
    {
        // Run for 100 ms.
        var maker = GetMaker("SyncProc(Time(0, 0, 0, 0, 10), Time(0))");

        // Pause before ever playing the runner.
        {
            var runner = maker();

            await runner.PauseAsync();

            Assert.AreEqual(RunnerState.None, runner.State);
            Assert.IsFalse(runner.WasSuccessful);
            Assert.IsNull(runner.GetException());
            Assert.IsNull(runner.GetErrorMessage());
        }

        // Pause when the runner is playing.
        {
            var runner = maker();

            var t = Task.Run(runner.Play);

            // Once the state is no longer "None", signal pause. Sleep for one millisecond
            // each time we wait.
            while (runner.State == RunnerState.None)
                Thread.Sleep(1);

            // We're no longer in the "None" state so signal pause. This waits so the runner
            // should be "Done" when this returns.
            await runner.PauseAsync();

            Assert.AreEqual(RunnerState.Done, runner.State);
            Assert.IsTrue(runner.WasSuccessful);
            Assert.IsNull(runner.GetException());
            Assert.IsNull(runner.GetErrorMessage());
        }

        // Delay before playing, and pause immediately.
        {
            var runner = maker();

            var t = Task.Run(() =>
            {
                Thread.Sleep(10);
                runner.Play();
            });

            // This most likely happens before the runner starts playing. Either way, it has no
            // effect on the final result.
            await runner.PauseAsync();

            await t;

            Assert.AreEqual(RunnerState.Done, runner.State);
            Assert.IsTrue(runner.WasSuccessful);
            Assert.IsNull(runner.GetException());
            Assert.IsNull(runner.GetErrorMessage());
        }
    }

    /// <summary>
    /// Abort a <see cref="SyncActionRunner"/> from various states.
    /// </summary>
    [TestMethod]
    public async Task SyncProcAbort()
    {
        // Run at most 10 seconds. It shouldn't take any where near that long before
        // we signal to abort.
        var maker = GetMaker("SyncProc(Time(0, 0, 0, 10), Time(0))");

        // Abort before ever playing the runner.
        {
            var runner = maker();

            runner.BeginAbort();

            Assert.AreEqual(RunnerState.Done, runner.State);
            Assert.IsFalse(runner.WasSuccessful);
            Assert.IsNotNull(runner.GetException());
            Assert.IsNotNull(runner.GetErrorMessage());
        }

        // Abort when the runner is playing.
        {
            var runner = maker();

            var t = Task.Run(runner.Play);

            // Once the state is no longer "None", signal abort. Sleep for one millisecond
            // each time we wait.
            while (runner.State == RunnerState.None)
                Thread.Sleep(1);

            // We're no longer in the "None" state so signal abort. Almost certainly, the test
            // should still be running so this should cause the runner to "fail".
            runner.BeginAbort();

            // Wait until the runner completes.
            await t;

            Assert.AreEqual(RunnerState.Done, runner.State);

            // REVIEW: Technically there is no guarantee that this fails but the only way
            // it will succeed is if the main thread is suspended for 10 seconds.
            Assert.IsFalse(runner.WasSuccessful);
            Assert.IsNotNull(runner.GetException());
            Assert.IsNotNull(runner.GetErrorMessage());
        }

        // Abort and wait when the runner is playing.
        {
            var runner = maker();

            var t = Task.Run(runner.Play);

            // Once the state is no longer "None", signal abort. Sleep for one millisecond
            // each time we wait.
            while (runner.State == RunnerState.None)
                Thread.Sleep(1);

            // We're no longer in the "None" state so signal abort. Almost certainly, the test
            // should still be running so this should cause the runner to "fail".
            await runner.AbortAsync();

            Assert.AreEqual(RunnerState.Done, runner.State);

            // REVIEW: Technically there is no guarantee that this fails but the only way
            // it will succeed is if the main thread is suspended for 10 seconds.
            Assert.IsFalse(runner.WasSuccessful);
            Assert.IsNotNull(runner.GetException());
            Assert.IsNotNull(runner.GetErrorMessage());
        }

        // Delay before playing, and abort immediately.
        {
            var runner = maker();

            var t = Task.Run(() =>
            {
                Thread.Sleep(10);
                runner.Play();
            });

            runner.BeginAbort();
            await t;

            Assert.AreEqual(RunnerState.Done, runner.State);
            Assert.IsFalse(runner.WasSuccessful);
            Assert.IsNotNull(runner.GetException());
            Assert.IsNotNull(runner.GetErrorMessage());
        }
    }

    /// <summary>
    /// AbortCore should be "nominated" but its delay allows RunCore to grab the lock first
    /// and do the work.
    /// </summary>
    [TestMethod]
    public async Task SyncProcAbortInterleaved()
    {
        // Abort cases with abort delay.

        // RunCore has no delay. AbortCore has 100 ms delay. The main thread (AbortCore) should be the "nominated" thread,
        // but the the task thread (RunCore) should sneak in and grab the lock and actually do the "playing".
        var maker = GetMaker("SyncProc(Time(0), Time(0, 0, 0, 0, 100))");

        var runner = maker();

        var t = Task.Run(() =>
        {
            Thread.Sleep(1);
            runner.WaitAsync();
        });

        runner.BeginAbort();
        await t;

        Assert.AreEqual(RunnerState.Done, runner.State);
        Assert.IsTrue(runner.WasSuccessful);
        Assert.IsNull(runner.GetException());
        Assert.IsNull(runner.GetErrorMessage());
    }

    void VerifyThreadRunner(ActionRunner runner, long lim, RunnerState? state = null, bool good = true)
    {
        Assert.IsInstanceOfType(runner, typeof(ThreadActionRunner));
        Assert.AreEqual(2, runner.ResultCount);
        var infos = runner.GetResultInfos();
        Assert.AreEqual(2, infos.Length);

        var info = infos[0];
        Assert.AreEqual(info.Name, new DName("Limit"));
        Assert.AreEqual(info.Type, DType.I8Req);
        Assert.IsFalse(info.IsPrimary);
        Assert.AreEqual(lim, runner.GetResultValue(info));

        info = infos[1];
        Assert.AreEqual(info.Name, new DName("Count"));
        Assert.AreEqual(info.Type, DType.I8Req);
        Assert.IsTrue(info.IsPrimary);
        var count = (long)runner.GetResultValue(info);
        Assert.IsTrue(0L <= count);
        Assert.IsTrue(count <= lim);

        if (state != null)
        {
            switch (state)
            {
            case RunnerState.None:
                Assert.IsFalse(runner.WasSuccessful);
                Assert.AreEqual(0L, count);
                break;
            case RunnerState.Done:
                Assert.AreEqual(good, runner.WasSuccessful);
                if (good)
                {
                    Assert.AreEqual(lim, count);
                    Assert.IsNull(runner.GetException());
                    Assert.IsNull(runner.GetErrorMessage());
                }
                else
                {
                    Assert.IsNotNull(runner.GetException());
                    Assert.IsNotNull(runner.GetErrorMessage());
                }
                break;
            case RunnerState.Paused:
                Assert.IsFalse(runner.WasSuccessful);
                break;
            case RunnerState.Playing:
                // This is a race condition, so not appropriate.
                // Assert.IsTrue(!runner.WasSuccessful);
                break;
            }
        }
    }

    /// <summary>
    /// Run a <see cref="ThreadActionRunner"/> with Wait on a separate thread.
    /// </summary>
    [TestMethod]
    public async Task ThreadProcPlay()
    {
        // Wait one tick per yield.
        long lim = 10;
        var maker = GetMaker("ThreadProc(CastTime(1), 10)");

        // Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            await runner.WaitAsync();

            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }

        // Play, Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }

        // Delay Play on one thread and Wait on another so the Wait may win.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            var t = Task.Run(() =>
            {
                Thread.Sleep(10);
                runner.Play();
                VerifyThreadRunner(runner, lim);
            });

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);

            // Await the task as well, although we don't really need to.
            await t;

            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }
    }

    [TestMethod]
    public async Task ThreadProcPrime()
    {
        // Wait one tick per yield.
        long lim = 10;
        var maker = GetMaker("ThreadProc(CastTime(1), 10)");

        // Prime.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            await runner.PrimeAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }

        // Play, Prime, Play, Prime.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PrimeAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);

            await runner.PrimeAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }
    }

    /// <summary>
    /// "Pause" a <see cref="ThreadActionRunner"/> from various states.
    /// This also does "Abort" on each to release the worker thread.
    /// </summary>
    [TestMethod]
    public async Task ThreadProcPause()
    {
        // Wait 10 ms per yield.
        long lim = 20;
        var maker = GetMaker("ThreadProc(Time(0, 0, 0, 0, 10), 20)");

        // Just Pause does nothing.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, AbortAndWait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, Pause, AbortAndWait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, BeginAbort, Pause, Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            runner.BeginAbort();

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, BeginAbort, BeginAbort, Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            runner.BeginAbort();
            runner.BeginAbort();

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, Play, Pause, AbortAndWait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, Wait, Pause.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }
    }

    /// <summary>
    /// "Abort" a <see cref="ThreadActionRunner"/> from various states.
    /// </summary>
    [TestMethod]
    public async Task ThreadProcAbort()
    {
        // Wait 10 ms per yield.
        long lim = 20;
        var maker = GetMaker("ThreadProc(Time(0, 0, 0, 0, 10), 20)");

        // AbortAndWait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // BeginAbort, Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.BeginAbort();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, AbortAndWait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, BeginAbort, Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            runner.BeginAbort();
            VerifyThreadRunner(runner, lim);

            await runner.WaitAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }

        // Play, Pause, BeginAbort, Wait.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            runner.Play();
            VerifyThreadRunner(runner, lim, RunnerState.Playing);

            await runner.PauseAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.AbortAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }
    }

    [TestMethod]
    public async Task ThreadProcMultiWait()
    {
        // Wait one tick per yield.
        long lim = 10;
        var maker = GetMaker("ThreadProc(CastTime(1), 10)");

        // Wait twice.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            var t1 = runner.WaitAsync();
            var t2 = runner.WaitAsync();
            Assert.AreSame(t1, t2);

            await t2;

            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }
    }

    [TestMethod]
    public async Task ThreadProcWaitPause()
    {
        // Wait 100 ms per yield.
        long lim = 3;
        var maker = GetMaker("ThreadProc(Time(0, 0, 0, 0, 100), 3)");

        // Wait and Pause.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            var t1 = runner.WaitAsync();
            var t2 = runner.PauseAsync();
            var t3 = runner.PauseAsync();
            Assert.AreNotSame(t1, t2);
            Assert.AreSame(t2, t3);

            await t2;
            VerifyThreadRunner(runner, lim, RunnerState.Paused);
            var t4 = runner.PauseAsync();
            Assert.IsTrue(t4.IsCompletedSuccessfully);

            runner.Play();

            await t1;
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }
    }

    [TestMethod]
    public async Task ThreadPausePlay()
    {
        // Wait 100 ms per yield.
        long lim = 3;
        var maker = GetMaker("ThreadProc(Time(0, 0, 0, 0, 100), 3)");

        // Play while pausing should throw.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            Assert.IsTrue(runner.TryGetResultFromName(new DName("Count"), out var info));
            var count = (long)runner.GetResultValue(info);
            Assert.AreEqual(0L, count);

            runner.Play();
            while ((long)runner.GetResultValue(info) == count)
            {
            }

            // Start pausing.
            var t1 = runner.PauseAsync();
            // Trying to play should throw (until the pause is complete).
            Assert.ThrowsException<InvalidOperationException>(runner.Play);

            await t1;
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            await runner.PrimeAsync();
            VerifyThreadRunner(runner, lim, RunnerState.Paused);

            var t = runner.WaitAsync();
            await runner.PrimeAsync();

            await t;
            VerifyThreadRunner(runner, lim, RunnerState.Done, true);
        }
    }

    [TestMethod]
    public async Task ThreadPauseAbort()
    {
        // Wait 100 ms per yield.
        long lim = 3;
        var maker = GetMaker("ThreadProc(Time(0, 0, 0, 0, 100), 3)");

        // Play while pausing should throw.
        {
            var runner = maker();
            VerifyThreadRunner(runner, lim, RunnerState.None);

            Assert.IsTrue(runner.TryGetResultFromName(new DName("Count"), out var info));
            var count = (long)runner.GetResultValue(info);
            Assert.AreEqual(0L, count);

            runner.Play();
            while ((long)runner.GetResultValue(info) == count)
            {
                await Task.Delay(0);
            }

            // Start pausing.
            var t1 = runner.PauseAsync();
            // Signal abort.
            runner.BeginAbort();

            await t1;
            VerifyThreadRunner(runner, lim, RunnerState.Done, false);
        }
    }

    /// <summary>
    /// This bind host knows about the test functions and procs, but nothing else.
    /// </summary>
    protected sealed class TestOperBindHostImpl : MinBindHost
    {
        public TestOperBindHostImpl()
            : base()
        {
        }

        public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            Validation.Assert(!name.IsRoot);

            if (user | fuzzy)
            {
                info = null;
                return false;
            }

            info = TestOperations.Instance.GetInfo(name);
            Validation.Assert(info is null || info.Oper is not null);
            return info != null;
        }
    }
}

/// <summary>
/// An action host that doesn't support streams.
/// </summary>
internal sealed class NoopActionHost : ActionHost
{
    public override TypeManager TypeManager { get; }

    public NoopActionHost(TypeManager tm)
    {
        TypeManager = tm;
    }

    public override Task<(Link full, Stream stream)> LoadStreamAsync(Link link)
    {
        throw new NotImplementedException();
    }

    public override Task<(Link full, Stream stream)> CreateStreamAsync(Link link, StreamOptions options = default)
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<Link> GetFiles(Link linkDir, out Link full)
    {
        throw new NotImplementedException();
    }

    public override DateTimeOffset Now()
    {
        throw new NotImplementedException();
    }

    public override ActionRunner CreateUserProcRunner(UserProc proc, DType typeWith, RecordBase with)
    {
        throw new NotImplementedException();
    }
}
