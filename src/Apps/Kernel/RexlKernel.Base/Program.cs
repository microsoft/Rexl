// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// The main kernel logic.
/// </summary>
public abstract partial class Program : ChannelHost
{
    private readonly string _session;
    protected readonly Logger _logger;

    private readonly ConnectionConfig _config;
    private readonly SignatureHandler _sig;
    private readonly CancellationTokenSource _cts;
    private readonly BlockingCollection<Message> _exeQueue;

    private readonly Pub _pub;
    private readonly Shell _control;
    private readonly Shell _shell;
    private readonly HeartBeat _heartBeat;

    // The worker thread for consuming and executing message from _exeQueue.
    private readonly Task _worker;

    protected readonly Publisher _publisher;

    public sealed override string Session => _session;
    public sealed override Logger Logger => _logger;
    public sealed override SignatureHandler Sig => _sig;

    protected Program(ReadOnlySpan<string> args, LogLevel lvl = LogLevel.All)
        : base()
    {
        _session = Guid.NewGuid().ToString();
        _logger = new Logger(_session, lvl);

        _logger.LogLow("New session");

        LogArgs(args);
        if (args.Length != 1)
        {
            _logger.LogTop("Expected one command line arg - path to connection file, got {0} args.", args.Length);
            throw new InvalidOperationException();
        }

        _config = ParseConfig(args[0]);

        _sig = new SignatureHandler(_config.Key, _config.SignatureScheme);
        _pub = new Pub(this, GetAddr(_config.IOPubPort));
        _publisher = new PubImpl(_pub);

        _pub.SendStarting();

        _cts = new CancellationTokenSource();
        _exeQueue = new BlockingCollection<Message>(new ConcurrentQueue<Message>());

        var handlersControl = new Dictionary<string, Action<Shell, Message>>
        {
            { MessageContent.Names.KernelInfoRequest, HandleInfoRequest },
            { MessageContent.Names.ShutdownRequest, HandleShutdownRequest },
            { MessageContent.Names.CommInfoRequest, HandleCommInfoRequest },
        };
        _control = new Shell(this, "Cntrl", GetAddr(_config.ControlPort), handlersControl);

        var handlersShell = new Dictionary<string, Action<Shell, Message>>
        {
            { MessageContent.Names.KernelInfoRequest, HandleInfoRequest },
            { MessageContent.Names.ShutdownRequest, HandleShutdownRequest },
            { MessageContent.Names.CommInfoRequest, HandleCommInfoRequest },
            { MessageContent.Names.ExecuteRequest, AddExecuteRequest },
        };
        _shell = new Shell(this, "Shell", GetAddr(_config.ShellPort), handlersShell);

        _heartBeat = new HeartBeat(this, GetAddr(_config.HeartBeatPort));

        _worker = Task.Run(HandleExecuteRequests);
    }

    private void LogArgs(ReadOnlySpan<string> args)
    {
        _logger.LogLow("Command line args ({0}):", args.Length);
        for (int i = 0; i < args.Length; i++)
            _logger.LogLow("  {0}) {1}", i, args[i]);
    }

    private ConnectionConfig ParseConfig(string path)
    {
        _logger.LogMid("Opening connection file: '{0}'", path);
        string text = File.ReadAllText(path);
        _logger.LogLow("Connection file contents:");
        _logger.LogLow(text);

        return JsonSerializer.Deserialize<ConnectionConfig>(text);
    }

    private string GetAddr(int port)
    {
        return string.Format("{0}://{1}:{2}", _config.Transport, _config.IP, port);
    }

    protected void Run()
    {
        _logger.LogTop("In run");

        _control.Start();
        _shell.Start();
        _heartBeat.Start();

        _control.Thread.Join();
        _shell.Thread.Join();
        _heartBeat.Thread.Join();

        _logger.LogTop("Shut down gracefully");
    }

    private void HandleInfoRequest(Shell shell, Message msg)
    {
        using (_pub.Busy(shell.Name, msg))
        {
            _logger.LogTop("{0}: Handling " + MessageContent.Names.KernelInfoRequest, shell.Name);
            shell.SendMain(msg, GetKernelInfo());
        }
    }

    protected abstract KernelInfoReply GetKernelInfo();

    private void AddExecuteRequest(Shell shell, Message msg)
    {
        _exeQueue.Add(msg);
    }

    /// <summary>
    /// Handle the execute requests as they are added to the queue.
    /// </summary>
    private async Task HandleExecuteRequests()
    {
        long counter = 0;

        var name = _shell.Name;
        _logger.LogTop("Starting worker task");

        var ct = _cts.Token;
        var handling = name + ": Handling " + MessageContent.Names.ExecuteRequest + ": '{0}'";
        bool graceful = false;
        try
        {
            try
            {
                foreach (var raw in _exeQueue.GetConsumingEnumerable(ct))
                {
                    if (raw.Content is ExecuteRequest exec)
                    {
                        using (_pub.Busy(name, raw))
                        {
                            _logger.LogTop(handling, exec.Code);
                            var msg = new ExecuteMessage(raw, exec, ++counter, ct);
                            await DoOne(msg);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            graceful = true;
        }
        finally
        {
            // Cleanup.
            _logger.LogTop(graceful ? "Cleaning up worker task" : "Abrupt shut down of worker task");
            await CleanupAsync().ConfigureAwait(false);
            _logger.LogTop("Leaving worker task");
        }
    }

    /// <summary>
    /// Handle one execute message.
    /// </summary>
    private async Task DoOne(ExecuteMessage msg)
    {
        Validation.AssertValue(msg);

        // Handle the execute message and completion.
        ExecuteReply reply;
        try
        {
            await ExecAsync(msg).ConfigureAwait(false);
            await msg.CompleteAsync().ConfigureAwait(false);
            reply = new ExecuteReplyOk(msg.Counter);
        }
        catch (Exception ex)
        {
            // REVIEW: Improve this? How does the client use the reply?
            var traceBack = new List<string>();
            traceBack.Add(ex.Message);
            traceBack.AddRange(ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            var err = new ExecuteReplyError(msg.Counter, "Exception", ex.Message, traceBack);
            reply = err;

            _publisher.PublishError(msg, ex.ToString());
        }

        // Complete the request.
        _shell.SendMain(msg.Msg, reply);
    }

    protected abstract Handler GetHandler();

    protected virtual Task ExecAsync(ExecuteMessage msg)
    {
        var handler = GetHandler();
        return handler.ExecAsync(msg);
    }

    protected virtual Task CleanupAsync()
    {
        var handler = GetHandler();
        return handler.CleanupAsync();
    }

    private void HandleShutdownRequest(Shell shell, Message msg)
    {
        // REVIEW: Handle restart? We currently ignore it.
        var name = shell.Name;
        using (_pub.Busy(name, msg))
        {
            _logger.LogTop("{0}: Handling " + MessageContent.Names.ShutdownRequest, name);

            // Conclude the message queue.
            // REVIEW: Tell the worker to abort?
            _exeQueue.CompleteAdding();

            shell.SendMain(msg, new ShutdownReply());
        }

        // Stop the channels.
        _control.Stop();
        _shell.Stop();
        _heartBeat.Stop();

        _cts.Cancel();
    }

    private void HandleCommInfoRequest(Shell shell, Message msg)
    {
        var name = shell.Name;
        using (_pub.Busy(name, msg))
        {
            _logger.LogTop("{0}: Handling " + MessageContent.Names.CommInfoRequest, name);
            shell.SendMain(msg, CommInfoReply.Instance);
        }
    }

    private sealed class PubImpl : Publisher
    {
        private readonly Pub _pub;

        public PubImpl(Pub pub)
        {
            Validation.AssertValue(pub);
            _pub = pub;
        }

        public override void PublishError(ExecuteMessage msg, string plain, string html)
        {
            Validation.AssertValue(msg);

            if (!string.IsNullOrWhiteSpace(plain))
            {
                if (!plain.EndsWith("\n"))
                    plain += "\n";
                _pub.SendMsg(msg.Msg, StreamContent.StdErr(plain));
            }

            if (!string.IsNullOrEmpty(html))
                _pub.SendMsg(msg.Msg, DisplayDataContent.CreateHtml(html));
        }

        public override void PublishData(ExecuteMessage msg, string? text, string? html = null)
        {
            Validation.AssertValue(msg);
            _pub.SendMsg(msg.Msg, DisplayDataContent.Create(text, html));
        }
    }
}

/// <summary>
/// Wraps an <see cref="ExecuteRequest"/> and related information.
/// </summary>
public sealed class ExecuteMessage
{
    // REVIEW: Support async?
    // REVIEW: Do we really need concurrent? Is there something cheaper?
    private volatile ConcurrentQueue<Action> _onCompleteActions;

    /// <summary>
    /// Cancellation token to use.
    /// </summary>
    public CancellationToken Ct { get; }

    /// <summary>
    /// The raw message.
    /// </summary>
    public Message Msg { get; }

    /// <summary>
    /// The execute request.
    /// </summary>
    public ExecuteRequest Request { get; }

    /// <summary>
    /// The counter value.
    /// </summary>
    public long Counter { get; }

    public ExecuteMessage(Message msg, ExecuteRequest request, long counter, CancellationToken ct = default)
    {
        Validation.AssertValue(msg);
        Validation.AssertValue(request);

        Ct = ct;
        Msg = msg;
        Request = request;
        Counter = counter;
    }

    /// <summary>
    /// Add a completion task.
    /// </summary>
    public void OnComplete(Action onComplete)
    {
        if (onComplete is not null)
            (_onCompleteActions ??= new()).Enqueue(onComplete);
    }

    /// <summary>
    /// Handle completion tasks.
    /// </summary>
    public Task CompleteAsync()
    {
        var acts = Interlocked.Exchange(ref _onCompleteActions, null);
        if (acts is not null)
        {
            foreach (var act in acts)
                act();
        }
        return Task.CompletedTask;
    }
}
