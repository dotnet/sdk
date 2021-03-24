// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class TemplatePackagesTests : IClassFixture<PackageManager>
    {
        private PackageManager _packageManager;
        public TemplatePackagesTests(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        [Fact]
        internal async Task CanInstall_LocalNuGetPackage()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackProjectTemplatesNuGetPackage("microsoft.dotnet.common.projecttemplates.5.0");

            InstallRequest installRequest = new InstallRequest
            {
                Identifier = Path.GetFullPath(packageLocation)
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            Assert.Equal(InstallerErrorCode.Success, result.First().Error);
            result.First().ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result.First().InstallRequest);

            IManagedTemplatePackage source = result.First().Source;
            Assert.Equal("Microsoft.DotNet.Common.ProjectTemplates.5.0", source.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.Name);
            Assert.Equal("NuGet", source.Installer.Name);
            Assert.Equal("Microsoft", source.GetDisplayDetails()["Author"]);
            source.Version.Should().NotBeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages.First().Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages.First());
            templatePackages.First().Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanInstall_RemoteNuGetPackage()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            InstallRequest installRequest = new InstallRequest
            {
                Identifier = "Take.Blip.Client.Templates",
                Version = "0.5.135",
                Details = new Dictionary<string, string>
                {
                    { InstallerConstants.NuGetSourcesKey, "https://api.nuget.org/v3/index.json" }
                }
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            Assert.Equal(InstallerErrorCode.Success, result.First().Error);
            Assert.True(string.IsNullOrEmpty(result.First().ErrorMessage));
            Assert.Equal(installRequest, result.First().InstallRequest);

            IManagedTemplatePackage source = result.First().Source;
            Assert.Equal("Take.Blip.Client.Templates", source.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.Name);
            Assert.Equal("NuGet", source.Installer.Name);
            source.GetDisplayDetails()["Author"].Should().NotBeNullOrEmpty();
            Assert.Equal("https://api.nuget.org/v3/index.json", source.GetDisplayDetails()["NuGetSource"]);
            Assert.Equal("0.5.135", source.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages.First().Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages.First());
            templatePackages.First().Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanInstall_Folder()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest
            {
                Identifier = Path.GetFullPath(templateLocation)
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            Assert.Equal(InstallerErrorCode.Success, result.First().Error);
            result.First().ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result.First().InstallRequest);

            IManagedTemplatePackage source = result.First().Source;
            Assert.Equal(Path.GetFullPath(templateLocation), source.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.Name);
            Assert.Equal("Folder", source.Installer.Name);
            source.Version.Should().BeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages.First().Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages.First());
            templatePackages.First().Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanCheckForLatestVersion_NuGetPackage()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            InstallRequest installRequest = new InstallRequest
            {
                Identifier = "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                Version = "5.0.0",
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            IManagedTemplatePackage source = result.First().Source;

            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, checkUpdateResults.Count);
            Assert.True(checkUpdateResults.First().Success);
            Assert.Equal(InstallerErrorCode.Success, checkUpdateResults.First().Error);
            Assert.True(string.IsNullOrEmpty(checkUpdateResults.First().ErrorMessage));
            Assert.Equal(source, checkUpdateResults.First().Source);
            Assert.False(checkUpdateResults.First().IsLatestVersion);
            Assert.NotEqual("5.0.0", checkUpdateResults.First().LatestVersion);
        }

        [Fact]
        internal async Task CanCheckForLatestVersion_Folder()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest
            {
                Identifier = Path.GetFullPath(templateLocation)
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            IManagedTemplatePackage source = result.First().Source;

            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, checkUpdateResults.Count);
            Assert.True(checkUpdateResults.First().Success);
            Assert.Equal(InstallerErrorCode.Success, checkUpdateResults.First().Error);
            Assert.True(string.IsNullOrEmpty(checkUpdateResults.First().ErrorMessage));
            Assert.Equal(source, checkUpdateResults.First().Source);
            Assert.True(checkUpdateResults.First().IsLatestVersion);
        }

        [Fact]
        internal async Task CanUpdate_NuGetPackage()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            InstallRequest installRequest = new InstallRequest
            {
                Identifier = "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                Version = "5.0.0",
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            IManagedTemplatePackage source = result.First().Source;

            UpdateRequest updateRequest = new UpdateRequest()
            {
                Source = source,
                Version = "5.0.1"
            };

            IReadOnlyList<UpdateResult> updateResults = await bootstrapper.UpdateTemplatePackagesAsync(new[] { updateRequest }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, updateResults.Count);
            Assert.True(updateResults.First().Success);
            Assert.Equal(InstallerErrorCode.Success, updateResults.First().Error);
            Assert.True(string.IsNullOrEmpty(updateResults.First().ErrorMessage));
            Assert.Equal(updateRequest, updateResults.First().UpdateRequest);

            IManagedTemplatePackage updatedSource = updateResults.First().Source;
            Assert.Equal("Global Settings", updatedSource.Provider.Factory.Name);
            Assert.Equal("NuGet", updatedSource.Installer.Name);
            Assert.Equal("5.0.1", updatedSource.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages.First().Should().BeEquivalentTo(updatedSource);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages.First());
            templatePackages.First().Should().BeEquivalentTo((ITemplatePackage)updatedSource);
        }

        [Fact]
        internal async Task CanUninstall_NuGetPackage()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            InstallRequest installRequest = new InstallRequest
            {
                Identifier = "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                Version = "5.0.0",
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            IManagedTemplatePackage source = result.First().Source;

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages.First().Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, uninstallResults.Count);
            Assert.True(uninstallResults.First().Success);
            Assert.Equal(InstallerErrorCode.Success, uninstallResults.First().Error);
            uninstallResults.First().ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(source, uninstallResults.First().Source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(0, templatePackages.Count);
        }

        [Fact]
        internal async Task CanUninstall_Folder()
        {
            Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest
            {
                Identifier = Path.GetFullPath(templateLocation)
            };

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result.First().Success);
            IManagedTemplatePackage source = result.First().Source;
            Assert.Equal(templateLocation, source.MountPointUri);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages.First().Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, uninstallResults.Count);
            Assert.True(uninstallResults.First().Success);
            Assert.Equal(InstallerErrorCode.Success, uninstallResults.First().Error);
            uninstallResults.First().ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(source, uninstallResults.First().Source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackages(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(0, templatePackages.Count);

            Directory.Exists(templateLocation);
        }
    }
}
