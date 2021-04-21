// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class NuGetApiPackageManagerTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;

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
            result.PackageVersion.Should().NotBeNullOrEmpty();
            result.NuGetSource.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DownloadPackage_SpecificVersion()
        {
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(engineEnvironmentSettings);
            var result = await packageManager.DownloadPackageAsync(installPath, "Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0").ConfigureAwait(false);

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
            exception.PackageVersion.ToString().Should().Be("5.0.0");
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
    }
}
