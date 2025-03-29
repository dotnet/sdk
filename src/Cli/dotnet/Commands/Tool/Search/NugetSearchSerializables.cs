// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolPackage;

namespace Microsoft.DotNet.Tools.Tool.Search;

/// <summary>
/// All fields are possibly null other than Id, Version, Tags, Authors, Versions
/// </summary>
internal class SearchResultPackage(
    PackageId id,
    string latestVersion,
    string description,
    string summary,
    IReadOnlyCollection<string> tags,
    IReadOnlyCollection<string> authors,
    int totalDownloads,
    bool verified,
    IReadOnlyCollection<SearchResultPackageVersion> versions)
{
    public PackageId Id { get; } = id;
    public string LatestVersion { get; } = latestVersion ?? throw new ArgumentNullException(nameof(latestVersion));
    public string Description { get; } = description;
    public string Summary { get; } = summary;
    public IReadOnlyCollection<string> Tags { get; } = tags ?? throw new ArgumentNullException(nameof(tags));
    public IReadOnlyCollection<string> Authors { get; } = authors ?? throw new ArgumentNullException(nameof(authors));
    public int TotalDownloads { get; } = totalDownloads;
    public bool Verified { get; } = verified;
    public IReadOnlyCollection<SearchResultPackageVersion> Versions { get; } = versions ?? throw new ArgumentNullException(nameof(versions));
}

internal class SearchResultPackageVersion(string version, int downloads)
{
    public string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
    public int Downloads { get; } = downloads;
}
