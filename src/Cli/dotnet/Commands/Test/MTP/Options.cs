// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal record TestOptions(bool HasFilterMode, bool IsHelp, bool IsDiscovery);

internal record PathOptions(string? ProjectPath, string? SolutionPath, string? ResultsDirectoryPath, string? ConfigFilePath, string? DiagnosticOutputDirectoryPath);

internal record BuildOptions(
    PathOptions PathOptions,
    bool HasNoRestore,
    bool HasNoBuild,
    Utils.VerbosityOptions? Verbosity,
    bool NoLaunchProfile,
    bool NoLaunchProfileArguments,
    int DegreeOfParallelism,
    List<string> UnmatchedTokens,
    IEnumerable<string> MSBuildArgs);
