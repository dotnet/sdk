// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.NugetSearch;

internal interface INugetToolSearchApiRequest
{
    /// <summary>
    /// Queries the search API for the given source's service index (e.g. a NuGet.Config
    /// package source's Source URL) with the given search parameters.
    /// </summary>
    Task<string> GetResult(NugetSearchApiParameter nugetSearchApiParameter, string sourceUrl);
}
