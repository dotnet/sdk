// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.NugetSearch;

internal sealed class NugetSearchApiParameter(
    string? searchTerm = null,
    int? skip = null,
    int? take = null,
    bool prerelease = false)
{
    public string? SearchTerm { get; } = searchTerm;
    public int? Skip { get; } = skip;
    public int? Take { get; } = take;
    public bool Prerelease { get; } = prerelease;
}
