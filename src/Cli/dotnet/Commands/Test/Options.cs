﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal record TestOptions(string Architecture, bool HasFilterMode, bool IsHelp);

internal record PathOptions(string ProjectPath, string SolutionPath, string DirectoryPath);

internal record BuildOptions(
    PathOptions PathOptions,
    bool HasNoRestore,
    bool HasNoBuild,
    VerbosityOptions? Verbosity,
    bool NoLaunchProfile,
    bool NoLaunchProfileArguments,
    int DegreeOfParallelism, List<string> UnmatchedTokens,
    IEnumerable<string> MSBuildArgs);
