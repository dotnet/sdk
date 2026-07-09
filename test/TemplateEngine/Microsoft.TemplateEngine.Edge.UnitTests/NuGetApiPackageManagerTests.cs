// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Configuration;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class NuGetApiPackageManagerTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private readonly IList<string> _additionalSources = new[] { "https://api.nuget.org/v3/index.json" };

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        public async Task DownloadPackage_Latest()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            // use a different source for checking specific nuget metadata
            var result = await packageManager.DownloadPackageAsync(
                installPath,
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                additionalSources: new[] { "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" },
                cancellationToken: TestContext.CancellationToken);

            result.Author.Should().Be("Microsoft");
            result.FullPath.Should().ContainAll(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0");
            Assert.IsTrue(File.Exists(result.FullPath));
            result.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            result.Owners.Should().Be(string.Empty);
            result.Reserved.Should().BeFalse();
            result.PackageVersion.Should().NotBeNullOrEmpty();
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_LatestFromNugetOrgFeed()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);

            // add the source for getting ownership info
            var result = await packageManager.DownloadPackageAsync(
                installPath,
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                additionalSources: _additionalSources,
                cancellationToken: TestContext.CancellationToken);

            result.Author.Should().Be("Microsoft");
            result.FullPath.Should().ContainAll(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0");
            Assert.IsTrue(File.Exists(result.FullPath));
            result.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            result.Owners.Should().Be("dotnetframework, Microsoft");
            result.Reserved.Should().BeTrue();
            result.PackageVersion.Should().NotBeNullOrEmpty();
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_SpecificVersion()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var result = await packageManager.DownloadPackageAsync(
                installPath,
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                "5.0.0",
                additionalSources: _additionalSources, cancellationToken: TestContext.CancellationToken);

            result.Author.Should().Be("Microsoft");
            result.FullPath.Should().ContainAll(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");
            Assert.IsTrue(File.Exists(result.FullPath));
            result.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            result.PackageVersion.Should().Be("5.0.0");
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_UnknownPackage()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var exception = await Assert.ThrowsExactlyAsync<PackageNotFoundException>(() => packageManager.DownloadPackageAsync(
                installPath, "Microsoft.DotNet.NotCommon.ProjectTemplates.5.0", "5.0.0", additionalSources: _additionalSources, cancellationToken: TestContext.CancellationToken));

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.NotCommon.ProjectTemplates.5.0");
            exception.PackageVersion.Should().NotBeNull();
            exception.PackageVersion!.ToString().Should().Be("5.0.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_InvalidPath()
        {
            string installPath = ":/?";
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var exception = await Assert.ThrowsExactlyAsync<DownloadException>(() => packageManager.DownloadPackageAsync(
                installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0", additionalSources: _additionalSources, cancellationToken: TestContext.CancellationToken));

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            exception.PackageVersion.ToString().Should().Be("5.0.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_CannotOverwritePackage()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            await packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0", additionalSources: _additionalSources, cancellationToken: TestContext.CancellationToken);
            var exception = await Assert.ThrowsExactlyAsync<DownloadException>(() => packageManager.DownloadPackageAsync(
                installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0", additionalSources: _additionalSources, cancellationToken: TestContext.CancellationToken));

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            exception.PackageVersion.ToString().Should().Be("5.0.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_HasVulnerabilities()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            // Getting this version of the package as it has known vulnerabilities
            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);

            // add the source for getting vulnerability info
            var exception = await Assert.ThrowsExactlyAsync<VulnerablePackageException>(() => packageManager.DownloadPackageAsync(
                installPath,
                "System.Text.Json",
                "8.0.4",
                additionalSources: _additionalSources,
                cancellationToken: TestContext.CancellationToken));

            exception.PackageIdentifier.Should().Be("System.Text.Json");
            exception.PackageVersion.Should().Be("8.0.4");
            exception.Vulnerabilities.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task DownloadPackage_HasVulnerabilitiesForce()
        {
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            // Getting this version of the package as it has known vulnerabilities
            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);

            // add the source for getting vulnerability info
            var result = await packageManager.DownloadPackageAsync(
                installPath,
                "System.Text.Json",
                "8.0.4",
                additionalSources: _additionalSources,
                force: true,
                cancellationToken: TestContext.CancellationToken);

            result.PackageIdentifier.Should().Be("System.Text.Json");
            result.Author.Should().Be("Microsoft");
            result.PackageVersion.Should().Be("8.0.4");
            Assert.IsTrue(File.Exists(result.FullPath));
            result.PackageVulnerabilities.Should().NotBeNullOrEmpty();
            result.NuGetSource.Should().Be(_additionalSources[0]);
        }

        [TestMethod]
        public async Task GetLatestVersion_Success()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            (string latestVersion, bool isLatestVersion, _) = await packageManager.GetLatestVersionAsync("Microsoft.DotNet.Common.ProjectTemplates.5.0", additionalSource: _additionalSources.FirstOrDefault(), cancellationToken: TestContext.CancellationToken);

            latestVersion.Should().NotBeNullOrEmpty();
            isLatestVersion.Should().BeFalse();
        }

        [TestMethod]
        public async Task GetLatestVersion_SpecificVersion()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            (string latestVersion, bool isLatestVersion, _) = await packageManager.GetLatestVersionAsync(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0", additionalSource: _additionalSources.First(), cancellationToken: TestContext.CancellationToken);

            latestVersion.Should().NotBe("5.0.0");
            isLatestVersion.Should().BeFalse();
        }

        [TestMethod]
        public async Task GetLatestVersion_UnknownPackage()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var exception = await Assert.ThrowsExactlyAsync<PackageNotFoundException>(() => packageManager.GetLatestVersionAsync(
                "Microsoft.DotNet.NotCommon.ProjectTemplates.5.0", "5.0.0", additionalSource: _additionalSources.FirstOrDefault(), cancellationToken: TestContext.CancellationToken));

            exception.PackageIdentifier.Should().Be("Microsoft.DotNet.NotCommon.ProjectTemplates.5.0");
            exception.Message.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public void RemoveInsecurePackages_AllInsecure()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

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

        [TestMethod]
        public void RemoveInsecurePackages_AllSecure()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            List<PackageSource> allPackages = new List<PackageSource>()
            {
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"),
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                new PackageSource("https://pkgs.dev.azure.com/dnceng/public/_packaging/nuget-build/nuget/v3/index.json")
            };
            var securePackages = packageManager.RemoveInsecurePackages(allPackages);

            securePackages.Should().NotBeEmpty();
            Assert.AreSequenceEqual(allPackages, securePackages);
        }

        [TestMethod]
        public void RemoveInsecurePackages_Mixed()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

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
