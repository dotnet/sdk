// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class PackageLocation(
    FilePath? nugetConfig = null,
    DirectoryPath? rootConfigDirectory = null,
    string[] additionalFeeds = null,
    string[] sourceFeedOverrides = null)
{
    public FilePath? NugetConfig { get; } = nugetConfig;
    public DirectoryPath? RootConfigDirectory { get; } = rootConfigDirectory;
    public string[] AdditionalFeeds { get; } = additionalFeeds ?? Array.Empty<string>();
    public string[] SourceFeedOverrides { get; } = sourceFeedOverrides ?? Array.Empty<string>();
}
