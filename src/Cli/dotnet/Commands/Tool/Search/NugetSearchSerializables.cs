// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolPackage;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal class SearchResultPackage(
    PackageId id,
    string latestVersion,
    string? description,
    string? summary,
    IReadOnlyCollection<string> tags,
    IReadOnlyCollection<string> authors,
    long totalDownloads,
    bool verified,
    IReadOnlyCollection<SearchResultPackageVersion> versions)
{
    public PackageId Id { get; } = id;
    public string LatestVersion { get; } = latestVersion ?? throw new ArgumentNullException(nameof(latestVersion));
    public string? Description { get; } = description;
    public string? Summary { get; } = summary;
    public IReadOnlyCollection<string> Tags { get; } = tags ?? throw new ArgumentNullException(nameof(tags));
    public IReadOnlyCollection<string> Authors { get; } = authors ?? throw new ArgumentNullException(nameof(authors));
    public long TotalDownloads { get; } = totalDownloads;
    public bool Verified { get; } = verified;
    public IReadOnlyCollection<SearchResultPackageVersion> Versions { get; } = versions ?? throw new ArgumentNullException(nameof(versions));
}

internal class SearchResultPackageVersion(string version, long downloads)
{
    public string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
    public long Downloads { get; } = downloads;
}
