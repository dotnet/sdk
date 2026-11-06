// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Contains the current invocation and filesystem inputs used to plan a file-based application run.
/// </summary>
/// <param name="EntryPointFileFullPath">The fully qualified entry-point path.</param>
/// <param name="ArtifactsPath">The application artifacts directory.</param>
/// <param name="GlobalProperties">The effective MSBuild global properties.</param>
/// <param name="CanCache">Whether the current directive set permits cache persistence.</param>
/// <param name="Directives">The serialized directives recognized by the SDK.</param>
/// <param name="SdkVersion">The current SDK version.</param>
/// <param name="RuntimeVersion">The current runtime version.</param>
/// <param name="NoCache">Whether cache reuse is disabled.</param>
/// <param name="GetCscInputPaths">Lazily provides required direct-compilation inputs.</param>
internal sealed record FileBasedAppRunPlanInputs(
    string EntryPointFileFullPath,
    string ArtifactsPath,
    Dictionary<string, string> GlobalProperties,
    bool CanCache,
    ImmutableArray<string> Directives,
    string SdkVersion,
    string RuntimeVersion,
    bool NoCache,
    Func<IEnumerable<string>> GetCscInputPaths);
