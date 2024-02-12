// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class ConfigurationTests : BootstrapperTestBase
    {
        [Fact]
        internal async Task PhysicalConfigurationTest()
        {
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            Assert.NotNull(userProfileDir);
            var hostDir = Path.Combine(userProfileDir!, ".templateengine", nameof(PhysicalConfigurationTest).ToString());
            try
            {
                var builtIns = BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation);
                var host = new DefaultTemplateEngineHost(nameof(PhysicalConfigurationTest).ToString(), "1.0.0", null, builtIns, Array.Empty<string>());

                Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true);
                var result = await bootstrapper.GetTemplatesAsync(cancellationToken: default);
                Assert.True(result.Any());
                bootstrapper.Dispose();
                Assert.True(Directory.Exists(hostDir));
                Assert.True(Directory.Exists(Path.Combine(hostDir, "1.0.0")));
                Assert.True(File.Exists(Path.Combine(hostDir, "1.0.0", "templatecache.json")));
            }
            finally
            {
                Directory.Delete(hostDir, true);
            }
        }

        [Fact]
        internal async Task VirtualConfigurationTest()
        {
            string? userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            Assert.NotNull(userProfileDir);

            string baseDir = Path.Combine(userProfileDir!, ".templateengine");
            var hostDir = Path.Combine(baseDir, nameof(VirtualConfigurationTest).ToString());

            var builtIns = BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation);
            var host = new DefaultTemplateEngineHost(nameof(VirtualConfigurationTest).ToString(), "1.0.0", null, builtIns, Array.Empty<string>());

            Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: true, loadDefaultComponents: true);
            var result = await bootstrapper.GetTemplatesAsync(cancellationToken: default);
            Assert.True(result.Any());

            DateTime? packagesJsonModificationTime = null;
            if (File.Exists(Path.Combine(baseDir, "packages.json")))
            {
                packagesJsonModificationTime = File.GetLastWriteTimeUtc(Path.Combine(baseDir, "packages.json"));
            }

            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Web.ProjectTemplates.5.0", "5.0.0");
            IReadOnlyList<InstallResult> installResult = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(installResult);
            Assert.True(installResult[0].Success);
            bootstrapper.Dispose();

            if (packagesJsonModificationTime == null)
            {
                Assert.False(File.Exists(Path.Combine(baseDir, "packages.json")));
            }
            else
            {
                Assert.Equal(packagesJsonModificationTime, File.GetLastWriteTimeUtc(Path.Combine(baseDir, "packages.json")));
            }

            Assert.False(Directory.Exists(hostDir));
        }

        [Fact]
        internal async Task PhysicalConfigurationTest_WithChangedHostLocation()
        {
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            Assert.NotNull(userProfileDir);
            var unexpectedHostDir = Path.Combine(userProfileDir!, ".templateengine", nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString());
            var expectedHostDir = TestUtils.CreateTemporaryFolder();

            var builtIns = BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation);
            var host = new DefaultTemplateEngineHost(nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString(), "1.0.0", null, builtIns, Array.Empty<string>());

            Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true, hostSettingsLocation: expectedHostDir);
            var result = await bootstrapper.GetTemplatesAsync(cancellationToken: default);
            Assert.True(result.Any());
            bootstrapper.Dispose();
            var hostDir = Path.Combine(expectedHostDir, nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString());
            Assert.True(Directory.Exists(hostDir));
            Assert.True(Directory.Exists(Path.Combine(hostDir, "1.0.0")));
            Assert.True(File.Exists(Path.Combine(hostDir, "1.0.0", "templatecache.json")));

            Assert.False(Directory.Exists(unexpectedHostDir));
        }
    }
}
