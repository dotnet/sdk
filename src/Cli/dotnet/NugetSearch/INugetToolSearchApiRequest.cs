// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Microsoft.DotNet.NugetSearch
{
    internal interface INugetToolSearchApiRequest
    {
        Task<string> GetResult(NugetSearchApiParameter nugetSearchApiParameter);
    }
}
