// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Tool.List;

internal sealed class VersionedDataContract<TContract>
{
        /// <summary>
        /// The version of the JSON format for dotnet tool list.
        /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;
    
    [JsonPropertyName("data")]
    public required TContract Data { get; init; }
}

internal class ToolListJsonContract
{
    [JsonPropertyName("packageId")]
    public required string PackageId { get; init; }
    
    [JsonPropertyName("version")]
    public required string Version { get; init; }
    
    [JsonPropertyName("commands")]
    public required string[] Commands { get; init; }
}

internal sealed class LocalToolListJsonContract : ToolListJsonContract
{
    [JsonPropertyName("manifest")]
    public required string Manifest { get; init; }
}

internal enum ToolListOutputFormat
{
    table = 0,
    json = 1
}

internal static class JsonHelper
{
    public static readonly JsonSerializerOptions NoEscapeSerializerOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}