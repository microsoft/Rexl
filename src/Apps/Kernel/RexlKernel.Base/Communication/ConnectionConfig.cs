// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// The connection config handed to us from Jupyter.
/// See: https://jupyter-client.readthedocs.io/en/latest/kernels.html#connection-files.
/// </summary>
public class ConnectionConfig
{
    [JsonPropertyName("control_port")]
    public int ControlPort { get; set; }

    [JsonPropertyName("hb_port")]
    public int HeartBeatPort { get; set; }

    [JsonPropertyName("iopub_port")]
    public int IOPubPort { get; set; }

    [JsonPropertyName("shell_port")]
    public int ShellPort { get; set; }

    [JsonPropertyName("stdin_port")]
    public int StdinPort { get; set; }

    [JsonPropertyName("transport")]
    public string Transport { get; set; }

    [JsonPropertyName("ip")]
    public string IP { get; set; }

    [JsonPropertyName("signature_scheme")]
    public string SignatureScheme { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; }
}
