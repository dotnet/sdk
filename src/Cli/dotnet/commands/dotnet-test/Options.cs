// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    internal record BuildConfigurationOptions(bool HasListTests, string Configuration, string Architecture);

    internal record BuildPathsOptions(string ProjectPath, string SolutionPath, string DirectoryPath, bool HasNoRestore, bool HasNoBuild, string Configuration, string RuntimeIdentifier);
}
