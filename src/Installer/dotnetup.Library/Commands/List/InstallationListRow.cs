// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

/// <summary>
/// A text-only relationship between a spec and its selected installation.
/// Kept separate from the list command's JSON contract.
/// </summary>
internal sealed class InstallationListRow
{
    public required InstallComponent Component { get; init; }
    public InstallSpecInfo? Spec { get; init; }
    public InstallationInfo? Installation { get; init; }
    public string SpecDisplay { get; init; } = "-";
    public string SourceDisplay { get; init; } = "-";
    public string? Error { get; init; }
}
