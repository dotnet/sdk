// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Contains a validated command for launching a file-based application.
/// </summary>
/// <param name="Command">The executable command.</param>
/// <param name="ArtifactsPath">The associated artifacts directory.</param>
/// <param name="RunProperties">The cached run properties, or <see langword="null"/> for a synthetic launch.</param>
internal sealed record FileBasedAppLaunchInfo(
    string Command,
    string ArtifactsPath,
    RunProperties? RunProperties = null);
