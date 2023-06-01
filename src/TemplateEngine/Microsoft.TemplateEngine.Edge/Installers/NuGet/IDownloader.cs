// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal interface IDownloader
    {
        Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string? version = null, IEnumerable<string>? additionalSources = null, bool force = false, CancellationToken cancellationToken = default);
    }

    internal class NuGetPackageInfo
    {
        public NuGetPackageInfo(string author, string owners, bool reserved, string fullPath, string? nuGetSource, string packageIdentifier, string packageVersion, IReadOnlyList<VulnerabilityInfo> vulnerabilities)
        {
            Author = author;
            Owners = owners;
            Reserved = reserved;
            FullPath = fullPath;
            NuGetSource = nuGetSource;
            PackageIdentifier = packageIdentifier;
            PackageVersion = packageVersion;
            PackageVulnerabilities = vulnerabilities;
        }

        public string Author { get; }

        public string Owners { get; }

        public bool Reserved { get; }

        public string FullPath { get; }

        public string? NuGetSource { get; }

        public string PackageIdentifier { get; }

        public string PackageVersion { get; }

        public IReadOnlyList<VulnerabilityInfo> PackageVulnerabilities { get; }

        internal NuGetPackageInfo WithFullPath(string newFullPath)
        {
            return new NuGetPackageInfo(
                Author,
                Owners,
                Reserved,
                newFullPath,
                NuGetSource,
                PackageIdentifier,
                PackageVersion,
                PackageVulnerabilities);
        }
    }
}
