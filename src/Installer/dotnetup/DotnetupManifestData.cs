// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Top-level manifest data structure for dotnetup installation tracking.
/// </summary>
internal class DotnetupManifestData
{
    public string SchemaVersion { get; set; } = "1";
    public List<DotnetRootEntry> DotnetRoots { get; set; } = [];
}

/// <summary>
/// Represents a dotnet installation root directory with its install specs and installations.
/// </summary>
internal class DotnetRootEntry
{
    public string Path { get; set; } = string.Empty;
    public InstallArchitecture Architecture { get; set; }
    public List<InstallSpec> InstallSpecs { get; set; } = [];
    public List<Installation> Installations { get; set; } = [];
}

/// <summary>
/// Represents what the user requested to be installed (the "install spec").
/// </summary>
internal class InstallSpec
{
    public InstallComponent Component { get; set; }
    public string VersionOrChannel { get; set; } = string.Empty;
    public InstallSource InstallSource { get; set; }
    public string? GlobalJsonPath { get; set; }
}

/// <summary>
/// Represents an actual installed component with its exact version and subcomponents.
/// </summary>
internal class Installation
{
    public InstallComponent Component { get; set; }
    public string Version { get; set; } = string.Empty;
    public List<string> Subcomponents { get; set; } = [];
}

/// <summary>
/// The source of an install spec.
/// </summary>
internal enum InstallSource
{
    Explicit,
    GlobalJson,
    Previous
}
