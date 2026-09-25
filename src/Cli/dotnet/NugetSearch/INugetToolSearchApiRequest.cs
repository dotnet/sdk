// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Search;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.NugetSearch;

internal interface INugetToolSearchApiRequest
{
    Task<IReadOnlyCollection<SearchResultPackage>> GetResult(
        NugetSearchApiParameter nugetSearchApiParameter,
        PackageSource source,
        CancellationToken cancellationToken);
}
