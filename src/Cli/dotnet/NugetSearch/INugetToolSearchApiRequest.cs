// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Tool.Search;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.NugetSearch;

internal interface INugetToolSearchApiRequest
{
    /// <summary>
    /// Queries the given source using NuGet's package search resource.
    /// </summary>
    Task<IReadOnlyCollection<SearchResultPackage>> GetResult(
        NugetSearchApiParameter nugetSearchApiParameter,
        PackageSource source);
}
