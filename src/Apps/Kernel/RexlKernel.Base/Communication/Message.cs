// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Represents a jupyter message, both for incoming and outgoing.
/// </summary>
public sealed class Message
{
    private static readonly IReadOnlyList<byte[]> s_emptyBytes = Array.Empty<byte[]>();
    private static readonly IReadOnlyDictionary<string, object> s_emptyMeta = new Dictionary<string, object>();

    public readonly IReadOnlyList<byte[]> Identities;
    public readonly MsgHeader Header;
    public readonly MsgHeader ParentHeader;
    public readonly IReadOnlyDictionary<string, object> MetaData;
    public readonly MessageContent Content;
    public readonly IReadOnlyList<byte[]> Buffers;

    public Message(MsgHeader hdr,
        MessageContent content,
        MsgHeader hdrPar = null,
        IReadOnlyDictionary<string, object> metaData = null,
        IReadOnlyList<byte[]> identities = null,
        IReadOnlyList<byte[]> buffers = null)
    {
        Validation.AssertValue(hdr);
        Validation.AssertValue(content);
        Validation.AssertValueOrNull(hdrPar);

        Header = hdr;
        Content = content;
        ParentHeader = hdrPar;

        Identities = identities ?? s_emptyBytes;
        MetaData = metaData ?? s_emptyMeta;
        Buffers = buffers ?? s_emptyBytes;
    }
}

/// <summary>
/// Header for a jupyter protocol message.
/// See <see href="https://jupyter-client.readthedocs.io/en/stable/messaging.html"/>.
/// </summary>
public sealed class MsgHeader
{
    [JsonPropertyName("msg_id")]
    public string MessageId { get; }

    [JsonPropertyName("session")]
    public string Session { get; }

    [JsonPropertyName("username")]
    public string Username { get; }

    /// <summary>
    /// ISO 8601 timestamp for when the message is created.
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; }

    [JsonPropertyName("msg_type")]
    public string MessageType { get; }

    [JsonPropertyName("version")]
    public string Version { get; }

    public MsgHeader(string messageId, string session, string username, string date, string messageType, string version)
    {
        Validation.BugCheckParam(!string.IsNullOrWhiteSpace(messageType), nameof(messageType));
        Validation.BugCheckParam(!string.IsNullOrWhiteSpace(messageId), nameof(messageId));
        Validation.BugCheckParam(!string.IsNullOrWhiteSpace(version), nameof(version));

        MessageId = messageId;
        Username = username;
        Session = session;
        Date = date;
        MessageType = messageType;
        Version = version;
    }
}

/// <summary>
/// Content of a jupyter protocol message, both request and response.
/// See <see href="https://jupyter-client.readthedocs.io/en/stable/messaging.html"/>
/// for details of the message types.
/// </summary>
public abstract class MessageContent
{
    /// <summary>
    /// Names of the various message types.
    /// </summary>
    public static class Names
    {
        public const string KernelInfoRequest = "kernel_info_request";
        public const string KernInfoReply = "kernel_info_reply";

        public const string ShutdownRequest = "shutdown_request";
        public const string ShutdownReply = "shutdown_reply";

        public const string ExecuteRequest = "execute_request";
        public const string ExecuteReply = "execute_reply";

        public const string CommInfoRequest = "comm_info_request";
        public const string CommInfoReply = "comm_info_reply";

        public const string Status = "status";
        public const string Stream = "stream";
    }

    protected MessageContent()
    {
    }

    /// <summary>
    /// Return the message kind/type. The return value is typically one of the names in
    /// the <see cref="Names"/> static class.
    /// </summary>
    public abstract string GetKind();

    /// <summary>
    /// Deserialize json into a <see cref="MessageContent"/> instance.
    /// The <paramref name="kind"/> value typically comes from the header's
    /// <see cref="MsgHeader.MessageType"/> property.
    /// </summary>
    public static MessageContent FromJson(string kind, string json)
    {
        Validation.CheckParam(!string.IsNullOrWhiteSpace(kind), nameof(kind));
        Validation.CheckParam(!string.IsNullOrWhiteSpace(json), nameof(json));

        switch (kind)
        {
        case Names.KernelInfoRequest:
            return KernelInfoRequest.Instance;
        case Names.ShutdownRequest:
            return JsonSerializer.Deserialize<ShutdownRequest>(json);
        case Names.ExecuteRequest:
            return JsonSerializer.Deserialize<ExecuteRequest>(json);
        case Names.CommInfoRequest:
            return JsonSerializer.Deserialize<CommInfoRequest>(json);
        //case "comm_msg":
        //    return JsonSerializer.Deserialize<CommMsg>(json);
        }

        return new RawContent(kind, json);
    }
}

/// <summary>
/// The content of an unrecognized incoming message type is typically materialized
/// as one of these.
/// REVIEW: Make this usable for outgoing messages for when the json is
/// convenient to construct.
/// </summary>
public sealed class RawContent : MessageContent
{
    private readonly string _kind;

    /// <summary>
    /// The json representation of the content.
    /// </summary>
    public string Json { get; }

    public RawContent(string kind, string json)
        : base()
    {
        Validation.AssertNonEmpty(kind);

        _kind = kind;
        Json = json;
    }

    public override string GetKind() => _kind;
}

/// <summary>
/// The content for an outgoing status message. There are three
/// instances: "starting", "busy", and "idle".
/// </summary>
public sealed class StatusContent : MessageContent
{
    public static readonly StatusContent Starting = new StatusContent("starting");
    public static readonly StatusContent Busy = new StatusContent("busy");
    public static readonly StatusContent Idle = new StatusContent("idle");

    [JsonPropertyName("execution_state")]
    public string ExecutionState { get; }

    private StatusContent(string state)
    {
        Validation.AssertNonEmpty(state);
        ExecutionState = state;
    }

    public override string GetKind() => Names.Status;
}

/// <summary>
/// Content for a request of some sort.
/// </summary>
public abstract class RequestContent : MessageContent
{
    protected RequestContent()
        : base()
    {
    }
}

/// <summary>
/// Content for a reply of some sort.
/// </summary>
public abstract class ReplyContent : MessageContent
{
    protected ReplyContent()
        : base()
    {
    }
}

/// <summary>
/// Content for a shutdown request.
/// </summary>
public sealed class ShutdownRequest : RequestContent
{
    [JsonPropertyName("restart")]
    public bool Restart { get; }

    public ShutdownRequest(bool restart = false)
    {
        Restart = restart;
    }

    public override string GetKind() => Names.ShutdownRequest;
}

/// <summary>
/// Content for a shutdown reply.
/// </summary>
public class ShutdownReply : ReplyContent
{
    [JsonPropertyName("restart")]
    public bool Restart { get; }

    public ShutdownReply(bool restart = false)
    {
        Restart = restart;
    }

    public override string GetKind() => Names.ShutdownReply;
}

/// <summary>
/// Content for a kernel information request.
/// </summary>
public sealed class KernelInfoRequest : RequestContent
{
    public static readonly KernelInfoRequest Instance = new();

    private KernelInfoRequest()
        : base()
    {
    }

    public override string GetKind() => Names.KernelInfoRequest;
}

/// <summary>
/// Content for a kernel information reply.
/// </summary>
public class KernelInfoReply : ReplyContent
{
    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("protocol_version")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("implementation")]
    public string Implementation { get; }

    [JsonPropertyName("implementation_version")]
    public string ImplementationVersion { get; }

    [JsonPropertyName("language_info")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LanguageInfo LanguageInfo { get; }

    [JsonPropertyName("banner")]
    public string Banner { get; }

    [JsonPropertyName("debugger")]
    public bool Debugger { get; }

    [JsonPropertyName("help_links")]
    public IReadOnlyList<Link> HelpLinks { get; }

    public KernelInfoReply(
        string protocolVersion, string implementation, string implementationVersion, LanguageInfo languageInfo,
        string banner = null, IReadOnlyList<Link> helpLinks = null)
    {
        Validation.BugCheckNonEmpty(protocolVersion, nameof(protocolVersion));
        Validation.BugCheckNonEmpty(implementation, nameof(implementation));
        Validation.BugCheckValue(languageInfo, nameof(languageInfo));

        Status = "ok";
        ProtocolVersion = protocolVersion;
        Implementation = implementation;
        ImplementationVersion = implementationVersion;
        LanguageInfo = languageInfo;
        Banner = banner;
        Debugger = false;
        HelpLinks = helpLinks ?? new List<Link>();
    }

    public override string GetKind() => Names.KernInfoReply;
}

/// <summary>
/// Content for a communication information request.
/// </summary>
public sealed class CommInfoRequest : RequestContent
{
    [JsonPropertyName("target_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string TargetName { get; }

    public CommInfoRequest(string targetName)
    {
        TargetName = targetName;
    }

    public override string GetKind() => Names.CommInfoRequest;
}

/// <summary>
/// Content for a communication information reply.
/// </summary>
public sealed class CommInfoReply : ReplyContent
{
    // REXL kernel does not actively open comms, so this is empty.
    // REVIEW: Need to fully support this and delegate to sub-class of Program.
    public static readonly CommInfoReply Instance = new();

    private CommInfoReply()
        : base()
    {
    }

    public override string GetKind() => Names.CommInfoReply;
}

/// <summary>
/// Content for an execute request.
/// </summary>
public sealed class ExecuteRequest : RequestContent
{
    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("silent")]
    public bool Silent { get; }

    [JsonPropertyName("store_history")]
    public bool StoreHistory { get; }

    [JsonPropertyName("user_expressions")]
    public IReadOnlyDictionary<string, string> UserExpressions { get; }

    [JsonPropertyName("allow_stdin")]
    public bool AllowStdin { get; }

    [JsonPropertyName("stop_on_error")]
    public bool StopOnError { get; }

    public ExecuteRequest(
        string code, bool silent = false, bool storeHistory = false, bool allowStdin = true, bool stopOnError = false,
        IReadOnlyDictionary<string, string> userExpressions = null)
    {
        Silent = silent;
        StoreHistory = storeHistory;
        AllowStdin = allowStdin;
        StopOnError = stopOnError;
        UserExpressions = userExpressions ?? new Dictionary<string, string>();
        Code = code ?? string.Empty;
    }

    public override string GetKind() => Names.ExecuteRequest;
}

public abstract class ExecuteReply : ReplyContent
{
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Status { get; }

    [JsonPropertyName("execution_count")]
    public long ExecutionCount { get; }

    protected ExecuteReply(string status, long executionCount)
    {
        Status = status;
        ExecutionCount = executionCount;
    }

    public sealed override string GetKind() => Names.ExecuteReply;
}

public class ExecuteReplyOk : ExecuteReply
{
    public ExecuteReplyOk(
            long executionCount,
            IReadOnlyList<IReadOnlyDictionary<string, string>> payload = null,
            IReadOnlyDictionary<string, string> userExpressions = null)
        : base("ok", executionCount)
    {
        UserExpressions = userExpressions ?? new Dictionary<string, string>();
        Payload = payload ?? new List<IReadOnlyDictionary<string, string>>();
    }

    [JsonPropertyName("payload")]
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Payload { get; }

    [JsonPropertyName("user_expressions")]
    public IReadOnlyDictionary<string, string> UserExpressions { get; }
}

public class ExecuteReplyError : ExecuteReply
{
    [JsonPropertyName("ename")]
    public string EName { get; }

    [JsonPropertyName("evalue")]
    public string EValue { get; }

    [JsonPropertyName("traceback")]
    public IReadOnlyList<string> Traceback { get; }

    [JsonConstructor]
    public ExecuteReplyError(long executionCount, string eName, string eValue, IReadOnlyList<string> traceback = null)
        : base("error", executionCount)
    {
        EName = eName;
        EValue = eValue;
        Traceback = traceback ?? new List<string>();
    }
}

public abstract class PubSubContent : MessageContent
{
    protected PubSubContent()
        : base()
    {
    }
}

public sealed class StreamContent : PubSubContent
{
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("text")]
    public string Text { get; }

    private StreamContent(string name, string text)
        : base()
    {
        Validation.AssertNonEmpty(name);

        Name = name;
        Text = text;
    }

    public static StreamContent StdErr(string text)
    {
        return new StreamContent("stderr", text);
    }

    public static StreamContent StdOut(string text)
    {
        return new StreamContent("stdout", text);
    }

    public override string GetKind() => Names.Stream;
}

public class DisplayDataContent : PubSubContent
{
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Source { get; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object> Data { get; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object> MetaData { get; }

    [JsonPropertyName("transient")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object> Transient { get; }

    public DisplayDataContent(string source = null,
        IReadOnlyDictionary<string, object> data = null,
        IReadOnlyDictionary<string, object> metaData = null,
        IReadOnlyDictionary<string, object> transient = null)
    {
        Source = source;
        Data = data ?? new Dictionary<string, object>();
        Transient = transient ?? new Dictionary<string, object>();
        MetaData = metaData ?? new Dictionary<string, object>();
    }

    public static DisplayDataContent Create(string? plain, string? html)
    {
        var data = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(plain))
            data.Add("text/plain", plain);
        if (!string.IsNullOrWhiteSpace(html))
            data.Add("text/html", html);
        return new DisplayDataContent(data: data);
    }

    public static DisplayDataContent CreateHtml(string html)
    {
        return new DisplayDataContent(data: new Dictionary<string, object> { { "text/html", html } });
    }

    public static DisplayDataContent CreatePlain(string plain)
    {
        return new DisplayDataContent(data: new Dictionary<string, object> { { "text/plain", plain } });
    }

    public override string GetKind() => "display_data";
}
