// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal interface IUpdateChecker
    {
        Task<(string LatestVersion, bool IsLatestVersion)> GetLatestVersionAsync(string identifier, string version = null, string additionalNuGetSource = null, CancellationToken cancellationToken = default);
    }
}
