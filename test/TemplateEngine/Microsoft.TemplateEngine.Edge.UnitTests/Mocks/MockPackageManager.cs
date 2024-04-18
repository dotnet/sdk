// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Protocol.Core.Types;
using static Microsoft.TemplateEngine.Edge.Installers.NuGet.NuGetApiPackageManager;

namespace Microsoft.TemplateEngine.Edge.UnitTests.Mocks
{
    internal class MockPackageManager : IDownloader, IUpdateChecker
    {
        internal const string DefaultFeed = "test_feed";
        private readonly PackageManager? _packageManager;
        private readonly string? _packageToPack;

        internal MockPackageManager()
        {
        }

        internal MockPackageManager(PackageManager packageManager, string packageToPack)
        {
            _packageManager = packageManager;
            _packageToPack = packageToPack;
        }

        public Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string? version = null, IEnumerable<string>? additionalSources = null, bool force = false, CancellationToken cancellationToken = default)
        {
            // names of exceptions throw them for test purposes
            switch (identifier)
            {
                case nameof(InvalidNuGetSourceException): throw new InvalidNuGetSourceException("test message");
                case nameof(DownloadException): throw new DownloadException(identifier, version ?? string.Empty, new[] { DefaultFeed });
                case nameof(PackageNotFoundException): throw new PackageNotFoundException(identifier, new[] { DefaultFeed });
                case nameof(VulnerablePackageException) when version == "12.0.3":
                    throw new VulnerablePackageException("Test Message", identifier, version, GetMockVulnerabilities());
                case nameof(Exception): throw new Exception("Generic error");
            }

            if (_packageManager == null)
            {
                throw new InvalidOperationException($"{nameof(_packageManager)} was not initialized");
            }
            if (_packageToPack == null)
            {
                throw new InvalidOperationException($"{nameof(_packageToPack)} was not initialized");
            }

            string testPackageLocation = _packageManager.PackNuGetPackage(_packageToPack);
            string targetFileName = string.IsNullOrWhiteSpace(version)
                ? Path.GetFileName(testPackageLocation)
                : $"{Path.GetFileNameWithoutExtension(testPackageLocation)}.{version}.nupkg";
            File.Copy(testPackageLocation, Path.Combine(downloadPath, targetFileName));
            return Task.FromResult(
                new NuGetPackageInfo(
                    "Microsoft",
                    "Microsoft",
                    true,
                    Path.Combine(downloadPath, targetFileName),
                    DefaultFeed,
                    identifier,
                    version ?? string.Empty,
                    Array.Empty<VulnerabilityInfo>()));
        }

        public Task<(string LatestVersion, bool IsLatestVersion, IReadOnlyList<VulnerabilityInfo> Vulnerabilities)> GetLatestVersionAsync(string identifier, string? version = null)
        {
            // names of exceptions throw them for test purposes
            return identifier switch
            {
                nameof(InvalidNuGetSourceException) => throw new InvalidNuGetSourceException("test message"),
                nameof(DownloadException) => throw new DownloadException(identifier, version ?? string.Empty, new[] { DefaultFeed }),
                nameof(PackageNotFoundException) => throw new PackageNotFoundException(identifier, new[] { DefaultFeed }),
                nameof(VulnerablePackageException) when version == "12.0.0" => throw new VulnerablePackageException("Test Message", identifier, version, GetMockVulnerabilities()),
                nameof(Exception) => throw new Exception("Generic error"),
                _ => Task.FromResult(("1.0.0", version != "1.0.0", (IReadOnlyList<VulnerabilityInfo>)new List<VulnerabilityInfo>())),
            };
        }

        Task<(string LatestVersion, bool IsLatestVersion, NugetPackageMetadata PackageMetadata)> IUpdateChecker.GetLatestVersionAsync(string identifier, string? version, string? additionalNuGetSource, CancellationToken cancellationToken)
        {
            // names of exceptions throw them for test purposes
            return identifier switch
            {
                nameof(InvalidNuGetSourceException) => throw new InvalidNuGetSourceException("test message"),
                nameof(DownloadException) => throw new DownloadException(identifier, version ?? string.Empty, new[] { DefaultFeed }),
                nameof(PackageNotFoundException) => throw new PackageNotFoundException(identifier, new[] { DefaultFeed }),
                nameof(VulnerablePackageException) when version == "12.0.0" => throw new VulnerablePackageException("Test Message", identifier, version, GetMockVulnerabilities()),
                nameof(Exception) => throw new Exception("Generic error"),
                _ => Task.FromResult(("1.0.0", version != "1.0.0", new NugetPackageMetadata(A.Fake<IPackageSearchMetadata>(), "owners", false))),
            };
        }

        private IReadOnlyList<VulnerabilityInfo> GetMockVulnerabilities() => new List<VulnerabilityInfo>()
        {
            new VulnerabilityInfo(1, new List<string>() { "https://testUrl1.com" }),
            new VulnerabilityInfo(2, new List<string>() { "https://testUrl2.com" }),
            new VulnerabilityInfo(3, new List<string>() { "https://testUrl3.com" })
        };
    }
}
