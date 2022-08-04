// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Edge.UnitTests.Mocks
{
    internal class MockPackageManager : IDownloader, IUpdateChecker
    {
        internal const string DefaultFeed = "test_feed";
        private PackageManager? _packageManager;

        internal MockPackageManager()
        {
        }

        internal MockPackageManager(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        public Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string? version = null, IEnumerable<string>? additionalSources = null, bool force = false, CancellationToken cancellationToken = default)
        {
            // names of exceptions throw them for test purposes
            switch (identifier)
            {
                case nameof(InvalidNuGetSourceException): throw new InvalidNuGetSourceException("test message");
                case nameof(DownloadException): throw new DownloadException(identifier, version ?? string.Empty, new[] { DefaultFeed });
                case nameof(PackageNotFoundException): throw new PackageNotFoundException(identifier, new[] { DefaultFeed });
                case nameof(Exception): throw new Exception("Generic error");
            }

            string testPackageLocation = _packageManager?.PackTestTemplatesNuGetPackage() ?? throw new Exception("Package Manager was not initialized");
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
            return Task.FromResult(new NuGetPackageInfo("Microsoft", Path.Combine(downloadPath, targetFileName), DefaultFeed, identifier, version ?? string.Empty));
        }

        public Task<(string LatestVersion, bool IsLatestVersion)> GetLatestVersionAsync(string identifier, string? version = null, string? additionalNuGetSource = null, CancellationToken cancellationToken = default)
        {
            // names of exceptions throw them for test purposes
            switch (identifier)
            {
                case nameof(InvalidNuGetSourceException): throw new InvalidNuGetSourceException("test message");
                case nameof(DownloadException): throw new DownloadException(identifier, version ?? string.Empty, new[] { DefaultFeed });
                case nameof(PackageNotFoundException): throw new PackageNotFoundException(identifier, new[] { DefaultFeed });
                case nameof(Exception): throw new Exception("Generic error");
            }

            return Task.FromResult(("1.0.0", version != "1.0.0"));
        }
    }
}
