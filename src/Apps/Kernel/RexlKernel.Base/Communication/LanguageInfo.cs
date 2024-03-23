// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Language information, as defined by the Jupyter protocol.
/// </summary>
public sealed class LanguageInfo
{
    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("version")]
    public string Version { get; }

    [JsonPropertyName("mimetype")]
    public string MimeType { get; }

    [JsonPropertyName("file_extension")]
    public string FileExtension { get; }

    [JsonPropertyName("pygments_lexer")]
    public string PygmentsLexer { get; }

    [JsonPropertyName("codemirror_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object CodeMirrorMode { get; }

    [JsonPropertyName("nbconvert_exporter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string NbConvertExporter { get; }

    public LanguageInfo(
        string name, string version, string mimeType, string fileExtension,
        string pygmentsLexer = null, object codeMirrorMode = null, string nbConvertExporter = null)
    {
        Validation.AssertNonEmpty(name);
        Validation.AssertNonEmpty(version);
        Validation.AssertNonEmpty(mimeType);
        Validation.AssertNonEmpty(fileExtension);

        Name = name;
        Version = version;
        MimeType = mimeType;
        FileExtension = fileExtension;
        PygmentsLexer = pygmentsLexer;
        CodeMirrorMode = codeMirrorMode;
        NbConvertExporter = nbConvertExporter;
    }
}

/// <summary>
/// Link information, as defined by the Jupyter protocol.
/// </summary>
public class Link
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}
