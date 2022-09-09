// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class TemplatePackagesTests : BootstrapperTestBase, IClassFixture<PackageManager>
    {
        private readonly PackageManager _packageManager;

        public TemplatePackagesTests(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        [Fact]
        internal async Task CanInstall_LocalNuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0").ConfigureAwait(false);

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(packageLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal("Microsoft.DotNet.Common.ProjectTemplates.5.0", source!.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.DisplayName);
            Assert.Equal("NuGet", source.Installer.Factory.Name);
            Assert.Equal("Microsoft", source.GetDetails()["Author"]);
            source.Version.Should().NotBeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanInstall_RemoteNuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest(
                "Take.Blip.Client.Templates",
                "0.5.135",
                details: new Dictionary<string, string>
                {
                    { InstallerConstants.NuGetSourcesKey, "https://api.nuget.org/v3/index.json" }
                });

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            Assert.True(string.IsNullOrEmpty(result[0].ErrorMessage));
            Assert.Equal(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal("Take.Blip.Client.Templates", source!.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.DisplayName);
            Assert.Equal("NuGet", source.Installer.Factory.Name);
            source.GetDetails()["Author"].Should().NotBeNullOrEmpty();
            Assert.Equal("https://api.nuget.org/v3/index.json", source.GetDetails()["NuGetSource"]);
            Assert.Equal("0.5.135", source.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanInstall_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal(Path.GetFullPath(templateLocation), source!.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.DisplayName);
            Assert.Equal("Folder", source.Installer.Factory.Name);
            source.Version.Should().BeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanCheckForLatestVersion_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source! }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, checkUpdateResults.Count);
            Assert.True(checkUpdateResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, checkUpdateResults[0].Error);
            Assert.True(string.IsNullOrEmpty(checkUpdateResults[0].ErrorMessage));
            Assert.Equal(source, checkUpdateResults[0].TemplatePackage);
            Assert.False(checkUpdateResults[0].IsLatestVersion);
            Assert.NotEqual("5.0.0", checkUpdateResults[0].LatestVersion);
        }

        [Fact]
        internal async Task CanCheckForLatestVersion_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source! }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, checkUpdateResults.Count);
            Assert.True(checkUpdateResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, checkUpdateResults[0].Error);
            Assert.True(string.IsNullOrEmpty(checkUpdateResults[0].ErrorMessage));
            Assert.Equal(source, checkUpdateResults[0].TemplatePackage);
            Assert.True(checkUpdateResults[0].IsLatestVersion);
        }

        [Fact]
        internal async Task CanUpdate_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            UpdateRequest updateRequest = new UpdateRequest(source!, "5.0.1");

            IReadOnlyList<UpdateResult> updateResults = await bootstrapper.UpdateTemplatePackagesAsync(new[] { updateRequest }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, updateResults.Count);
            Assert.True(updateResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, updateResults[0].Error);
            Assert.True(string.IsNullOrEmpty(updateResults[0].ErrorMessage));
            Assert.Equal(updateRequest, updateResults[0].UpdateRequest);

            IManagedTemplatePackage? updatedSource = updateResults[0].TemplatePackage;
            Assert.NotNull(updatedSource);
            Assert.Equal("Global Settings", updatedSource!.Provider.Factory.DisplayName);
            Assert.Equal("NuGet", updatedSource.Installer.Factory.Name);
            Assert.Equal("5.0.1", updatedSource.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages[0].Should().BeEquivalentTo(updatedSource);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, templatePackages.Count);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)updatedSource);
        }

        [Fact]
        internal async Task CanUninstall_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source! }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, uninstallResults.Count);
            Assert.True(uninstallResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, uninstallResults[0].Error);
            uninstallResults[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(source, uninstallResults[0].TemplatePackage);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(0, templatePackages.Count);
        }

        [Fact]
        internal async Task CanUninstall_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal(templateLocation, source!.MountPointUri);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, managedTemplatesPackages.Count);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, uninstallResults.Count);
            Assert.True(uninstallResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, uninstallResults[0].Error);
            uninstallResults[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(source, uninstallResults[0].TemplatePackage);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(0, templatePackages.Count);

            Directory.Exists(templateLocation);
        }

        [Fact]
        internal async Task CanReInstallPackage_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0").ConfigureAwait(false);

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(packageLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest(Path.GetFullPath(packageLocation), force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest(Path.GetFullPath(packageLocation));

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.False(result[0].Success);
            Assert.Equal(InstallerErrorCode.DownloadFailed, result[0].Error);
        }

        [Fact]
        internal async Task CanReInstallRemotePackage_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            Assert.True(string.IsNullOrEmpty(result[0].ErrorMessage));
            Assert.Equal(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0", force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0");

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.False(result[0].Success);
            Assert.Equal(InstallerErrorCode.AlreadyInstalled, result[0].Error);
        }

        [Fact]
        internal async Task CanReInstallFolder_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest(Path.GetFullPath(templateLocation), force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest(Path.GetFullPath(templateLocation));

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, result.Count);
            Assert.False(result[0].Success);
            Assert.Equal(InstallerErrorCode.AlreadyInstalled, result[0].Error);
        }
    }
}
