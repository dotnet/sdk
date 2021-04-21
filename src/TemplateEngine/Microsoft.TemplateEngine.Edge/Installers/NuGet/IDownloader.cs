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

    internal class NuGetPackageInfo
    {
        public NuGetPackageInfo(string author, string fullPath, string nuGetSource, string packageIdentifier, string packageVersion)
        {
            Author = author;
            FullPath = fullPath;
            NuGetSource = nuGetSource;
            PackageIdentifier = packageIdentifier;
            PackageVersion = packageVersion;
        }

        public string Author { get; }

        public string FullPath { get; }

        public string NuGetSource { get; }

        public string PackageIdentifier { get; }

        public string PackageVersion { get; }

        internal NuGetPackageInfo WithFullPath(string newFullPath)
        {
            return new NuGetPackageInfo(
                Author,
                newFullPath,
                NuGetSource,
                PackageIdentifier,
                PackageVersion);
        }
    }
}
