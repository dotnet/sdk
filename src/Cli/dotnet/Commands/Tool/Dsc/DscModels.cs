// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal record DscToolsState
{
    [JsonPropertyName("tools")]
    public List<DscToolState> Tools { get; set; } = new List<DscToolState>();
}

internal record DscToolState
{
    [JsonPropertyName("packageId")]
    public string? PackageId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("commands")]
    public List<string>? Commands { get; set; }

    [JsonPropertyName("scope")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DscToolScope? Scope { get; set; }

    [JsonPropertyName("toolPath")]
    public string? ToolPath { get; set; }

    [JsonPropertyName("manifestPath")]
    public string? ManifestPath { get; set; }

    [JsonPropertyName("_exist")]
    public bool? Exist { get; set; }
}

internal enum DscToolScope
{
    Global,
    Local,
    ToolPath
}

internal record DscErrorMessage
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

internal record DscDebugMessage
{
    [JsonPropertyName("debug")]
    public string Debug { get; set; } = string.Empty;
}

internal record DscTraceMessage
{
    [JsonPropertyName("trace")]
    public string Trace { get; set; } = string.Empty;
}
