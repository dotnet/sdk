// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Configuration;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class NuGetApiPackageManagerTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public NuGetApiPackageManagerTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task DownloadPackage_Latest()
        {
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var result = await packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0").ConfigureAwait(false);

            result.Author.Should().Be("Microsoft");
            result.FullPath.Should().ContainAll(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0");
            Assert.True(File.Exists(result.FullPath));
            result.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            result.Owners.Should().Be(string.Empty);
            result.Trusted.Should().BeFalse();
            result.PackageVersion.Should().NotBeNullOrEmpty();
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DownloadPackage_LatestFromNugetOrgFeed()
        {
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);

            var result = await packageManager.DownloadPackageAsync(
                installPath,
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                // add the source for getting ownership info
                additionalSources: new[] { "https://api.nuget.org/v3/index.json" }).ConfigureAwait(false);

            result.Author.Should().Be("Microsoft");
            result.FullPath.Should().ContainAll(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0");
            Assert.True(File.Exists(result.FullPath));
            result.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            result.Owners.Should().Be("dotnetframework, Microsoft");
            result.Trusted.Should().BeTrue();
            result.PackageVersion.Should().NotBeNullOrEmpty();
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DownloadPackage_SpecificVersion()
        {
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var result = await packageManager.DownloadPackageAsync(
                installPath,
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                "5.0.0").ConfigureAwait(false);

            result.Author.Should().Be("Microsoft");
            result.FullPath.Should().ContainAll(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");
            Assert.True(File.Exists(result.FullPath));
            result.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            result.PackageVersion.Should().Be("5.0.0");
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DownloadPackage_UnknownPackage()
        {
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var exception = await Assert.ThrowsAsync<PackageNotFoundException>(() => packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.NotCommon.ProjectTemplates.5.0", "5.0.0")).ConfigureAwait(false);

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.NotCommon.ProjectTemplates.5.0");
            exception.PackageVersion.Should().NotBeNull();
            exception.PackageVersion!.ToString().Should().Be("5.0.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DownloadPackage_InvalidPath()
        {
            string installPath = ":/?";
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var exception = await Assert.ThrowsAsync<DownloadException>(() => packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0")).ConfigureAwait(false);

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            exception.PackageVersion.ToString().Should().Be("5.0.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DownloadPackage_CannotOverwritePackage()
        {
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            await packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0").ConfigureAwait(false);
            var exception = await Assert.ThrowsAsync<DownloadException>(() => packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0")).ConfigureAwait(false);

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            exception.PackageVersion.ToString().Should().Be("5.0.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetLatestVersion_Success()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            (string latestVersion, bool isLatestVersion) = await packageManager.GetLatestVersionAsync("Microsoft.DotNet.Common.ProjectTemplates.5.0").ConfigureAwait(false);

            latestVersion.Should().NotBeNullOrEmpty();
            isLatestVersion.Should().BeFalse();
        }

        [Fact]
        public async Task GetLatestVersion_SpecificVersion()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            (string latestVersion, bool isLatestVersion) = await packageManager.GetLatestVersionAsync("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0").ConfigureAwait(false);

            latestVersion.Should().NotBe("5.0.0");
            isLatestVersion.Should().BeFalse();
        }

        [Fact]
        public async Task GetLatestVersion_UnknownPackage()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var exception = await Assert.ThrowsAsync<PackageNotFoundException>(() => packageManager.GetLatestVersionAsync("Microsoft.DotNet.NotCommon.ProjectTemplates.5.0", "5.0.0")).ConfigureAwait(false);

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.NotCommon.ProjectTemplates.5.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void RemoveInsecurePackages_AllInsecure()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            List<PackageSource> allPackages = new List<PackageSource>()
            {
                new PackageSource("http://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"),
                new PackageSource("http://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                new PackageSource("http://pkgs.dev.azure.com/dnceng/public/_packaging/nuget-build/nuget/v3/index.json"),
                new PackageSource("http://insecure-feed.org")
            };
            var securePackages = packageManager.RemoveInsecurePackages(allPackages);

            securePackages.Should().BeEmpty();
        }

        [Fact]
        public void RemoveInsecurePackages_AllSecure()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            List<PackageSource> allPackages = new List<PackageSource>()
            {
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"),
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/nuget-build/nuget/v3/index.json")
            };
            var securePackages = packageManager.RemoveInsecurePackages(allPackages);

            securePackages.Should().NotBeEmpty();
            Assert.Equal(allPackages, securePackages);
        }

        [Fact]
        public void RemoveInsecurePackages_Mixed()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            List<PackageSource> allPackages = new List<PackageSource>()
            {
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"),
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                new PackageSource("http://pkgs.dev.azure.com/dnceng/public/_packaging/nuget-build/nuget/v3/index.json"),
                new PackageSource("http://insecure-feed.org")
            };
            var securePackages = packageManager.RemoveInsecurePackages(allPackages);

            var expectedOutcome = new List<PackageSource>()
            {
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"),
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json")
            };

            securePackages.Should().NotBeEmpty();
            securePackages.Should().Equal(expectedOutcome);
        }
    }
}
