// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Project can be reprented by project file or by entry point file (for single-file apps).
/// </summary>
internal readonly struct ProjectRepresentation(string projectGraphPath, string? projectPath, string? entryPointFilePath) : IEquatable<ProjectRepresentation>
{
    /// <summary>
    /// Path used in Project Graph (may be virtual).
    /// </summary>
    public readonly string ProjectGraphPath = projectGraphPath;

    /// <summary>
    /// Path to an physical (non-virtual) project, if available.
    /// </summary>
    public readonly string? PhysicalPath = projectPath;

    /// <summary>
    /// Path to an entry point file, if available.
    /// </summary>
    public readonly string? EntryPointFilePath = entryPointFilePath;

    public ProjectRepresentation(string? projectPath, string? entryPointFilePath)
        : this(projectPath ?? VirtualProjectBuilder.GetVirtualProjectPath(entryPointFilePath!), projectPath, entryPointFilePath)
    {
    }

    [MemberNotNullWhen(true, nameof(PhysicalPath))]
    [MemberNotNullWhen(false, nameof(EntryPointFilePath))]
    public bool IsProjectFile
        => PhysicalPath != null;

    public string ProjectOrEntryPointFilePath
        => PhysicalPath ?? EntryPointFilePath!;

    public string GetContainingDirectory()
        => Path.GetDirectoryName(ProjectOrEntryPointFilePath)!;

    public static ProjectRepresentation FromProjectOrEntryPointFilePath(string projectOrEntryPointFilePath)
        => string.Equals(Path.GetExtension(projectOrEntryPointFilePath), ".csproj", StringComparison.OrdinalIgnoreCase)
            ? new(projectPath: projectOrEntryPointFilePath, entryPointFilePath: null)
            : new(projectPath: null, entryPointFilePath: projectOrEntryPointFilePath);

    public ProjectRepresentation WithProjectGraphPath(string projectGraphPath)
        => new(projectGraphPath, PhysicalPath, EntryPointFilePath);

    public bool Equals(ProjectRepresentation other)
        => PathUtilities.OSSpecificPathComparer.Equals(ProjectGraphPath, other.ProjectGraphPath);

    public override bool Equals(object? obj)
        => obj is ProjectRepresentation representation && Equals(representation);

    public override int GetHashCode()
        => PathUtilities.OSSpecificPathComparer.GetHashCode(ProjectGraphPath);

    public static bool operator ==(ProjectRepresentation left, ProjectRepresentation right)
        => left.Equals(right);

    public static bool operator !=(ProjectRepresentation left, ProjectRepresentation right)
        => !(left == right);
}
