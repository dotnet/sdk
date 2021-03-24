// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Edge.UnitTests.Mocks
{
    internal class MockPackageManager : IDownloader, IUpdateChecker
    {
        internal const string DefaultFeed = "test_feed";
        private PackageManager _packageManager;

        internal MockPackageManager()
        {

        }

        internal MockPackageManager(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        public Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string version = null, IEnumerable<string> additionalSources = null, CancellationToken cancellationToken = default)
        {
            // names of exceptions throw them for test purposes
            switch (identifier)
            {
                case nameof(InvalidNuGetSourceException): throw new InvalidNuGetSourceException("test message");
                case nameof(DownloadException): throw new DownloadException(identifier, version, new[] { DefaultFeed });
                case nameof(PackageNotFoundException): throw new PackageNotFoundException(identifier, new[] { DefaultFeed });
                case nameof(Exception): throw new Exception("Generic error");
            }

            string testPackageLocation = _packageManager.PackTestTemplatesNuGetPackage();
            string targetFileName;
            if (string.IsNullOrWhiteSpace(version))
            {
                targetFileName = Path.GetFileName(testPackageLocation);
            }
            else
            {
                targetFileName = $"{Path.GetFileNameWithoutExtension(testPackageLocation)}.{version}.nupkg";
            }
            File.Copy(testPackageLocation, Path.Combine(downloadPath, targetFileName));
            return Task.FromResult(new NuGetPackageInfo
            {
                Author = "Microsoft",
                FullPath = Path.Combine(downloadPath, targetFileName),
                PackageIdentifier = identifier,
                PackageVersion = version,
                NuGetSource = DefaultFeed
            });
        }

        public Task<(string latestVersion, bool isLatestVersion)> GetLatestVersionAsync(string identifier, string version = null, string additionalNuGetSource = null, CancellationToken cancellationToken = default)
        {
            // names of exceptions throw them for test purposes
            switch (identifier)
            {
                case nameof(InvalidNuGetSourceException): throw new InvalidNuGetSourceException("test message");
                case nameof(DownloadException): throw new DownloadException(identifier, version, new[] { DefaultFeed });
                case nameof(PackageNotFoundException): throw new PackageNotFoundException(identifier, new[] { DefaultFeed });
                case nameof(Exception): throw new Exception("Generic error");
            }

            return Task.FromResult(("1.0.0", version != "1.0.0"));
        }
    }
}
