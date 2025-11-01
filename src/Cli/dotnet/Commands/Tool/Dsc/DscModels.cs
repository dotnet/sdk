// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using NuGet.Versioning;

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

    /// <summary>
    /// Parses packageId and version from the PackageId property.
    /// Supports format: "packageId" or "packageId@version"
    /// </summary>
    public (string PackageId, VersionRange? VersionRange) ParsePackageIdentity()
    {
        if (string.IsNullOrEmpty(PackageId))
        {
            return (string.Empty, null);
        }

        string[] parts = PackageId.Split('@');
        string packageId = parts[0];

        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
        {
            // packageId@version format
            if (VersionRange.TryParse(parts[1], out var versionRange))
            {
                return (packageId, versionRange);
            }
        }
        else if (!string.IsNullOrEmpty(Version))
        {
            // Use separate Version property if available
            if (VersionRange.TryParse(Version, out var versionRange))
            {
                return (packageId, versionRange);
            }
        }

        return (packageId, null);
    }
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
