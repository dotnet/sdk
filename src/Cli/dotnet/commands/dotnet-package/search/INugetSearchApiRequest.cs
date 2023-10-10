// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.commands.package.search
{
    internal interface INugetSearchApiRequest
    {
        IList<PackageSource> GetEndpointsAsync();
        Task ExecuteCommandAsync();
    }
    
}
