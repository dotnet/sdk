// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Describes a file-based application launch committed to the Native AOT path.
/// </summary>
/// <param name="Command">The executable command.</param>
/// <param name="CommandArguments">The escaped command arguments.</param>
/// <param name="EnvironmentVariables">Environment variables to apply to the launched process.</param>
/// <param name="WorkingDirectory">The process working directory.</param>
/// <param name="ArtifactsPath">The artifacts directory to mark as used, or <see langword="null"/> when no artifacts are used.</param>
internal sealed record AotRunInvocation(
    string Command,
    string CommandArguments,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    string WorkingDirectory,
    string? ArtifactsPath);
#endif
