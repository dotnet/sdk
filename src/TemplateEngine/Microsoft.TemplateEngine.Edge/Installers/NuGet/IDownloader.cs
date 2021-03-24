// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal interface IDownloader
    {
        Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string version = null, IEnumerable<string> additionalSources = null, CancellationToken cancellationToken = default);
    }

    internal struct NuGetPackageInfo
    {
        internal string Author;
        internal string FullPath;
        internal string NuGetSource;
        internal string PackageIdentifier;
        internal string PackageVersion;
    }
}
