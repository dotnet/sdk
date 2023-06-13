// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using static Microsoft.TemplateEngine.Edge.Installers.NuGet.NuGetApiPackageManager;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal interface IUpdateChecker
    {
        Task<(string LatestVersion, bool IsLatestVersion, NugetPackageMetadata PackageMetadata)> GetLatestVersionAsync(string identifier, string? version = null, string? additionalNuGetSource = null, CancellationToken cancellationToken = default);
    }
}
