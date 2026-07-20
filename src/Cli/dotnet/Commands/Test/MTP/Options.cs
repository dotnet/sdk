// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal enum TestListFormat
{
    /// <summary>
    /// Human-readable discovery output (the default when '--list-tests' is passed without a value).
    /// </summary>
    Text,

    /// <summary>
    /// Machine-readable JSON discovery output ('--list-tests json').
    /// </summary>
    Json,
}

internal record TestOptions(bool IsHelp, bool IsDiscovery, TestListFormat ListTestsFormat);

internal record PathOptions(string? ProjectOrSolutionPath, string? SolutionPath, string? TestModules, string? ResultsDirectoryPath, string? ConfigFilePath, string? DiagnosticOutputDirectoryPath);

internal record BuildOptions(
    PathOptions PathOptions,
    bool HasNoRestore,
    bool HasNoBuild,
    Utils.VerbosityOptions? Verbosity,
    bool NoLaunchProfile,
    bool NoLaunchProfileArguments,
    ImmutableArray<string> TestApplicationArguments,
    IEnumerable<string> MSBuildArgs,
    string? Device,
    bool ListDevices,
    IReadOnlyDictionary<string, string> EnvironmentVariables);
