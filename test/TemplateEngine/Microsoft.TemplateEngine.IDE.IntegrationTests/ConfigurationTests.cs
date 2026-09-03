// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [TestClass]
    public class ConfigurationTests : BootstrapperTestBase
    {
        public TestContext TestContext { get; set; } = null!;
        [TestMethod]
        public async Task PhysicalConfigurationTest()
        {
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            Assert.IsNotNull(userProfileDir);
            var hostDir = Path.Combine(userProfileDir!, ".templateengine", nameof(PhysicalConfigurationTest).ToString());
            try
            {
                var builtIns = BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation);
                var host = new DefaultTemplateEngineHost(nameof(PhysicalConfigurationTest).ToString(), "1.0.0", null, builtIns, []);

                Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true);
                var result = await bootstrapper.GetTemplatesAsync(cancellationToken: TestContext.CancellationToken);
                Assert.IsNotEmpty(result);
                bootstrapper.Dispose();
                Assert.IsTrue(Directory.Exists(hostDir));
                Assert.IsTrue(Directory.Exists(Path.Combine(hostDir, "1.0.0")));
                Assert.IsTrue(File.Exists(Path.Combine(hostDir, "1.0.0", "templatecache.json")));
            }
            finally
            {
                Directory.Delete(hostDir, true);
            }
        }

        [TestMethod]
        public async Task VirtualConfigurationTest()
        {
            string? userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            Assert.IsNotNull(userProfileDir);

            string baseDir = Path.Combine(userProfileDir!, ".templateengine");
            var hostDir = Path.Combine(baseDir, nameof(VirtualConfigurationTest).ToString());

            var builtIns = BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation);
            var host = new DefaultTemplateEngineHost(nameof(VirtualConfigurationTest).ToString(), "1.0.0", null, builtIns, []);

            Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: true, loadDefaultComponents: true);
            var result = await bootstrapper.GetTemplatesAsync(cancellationToken: TestContext.CancellationToken);
            Assert.IsNotEmpty(result);

            DateTime? packagesJsonModificationTime = null;
            if (File.Exists(Path.Combine(baseDir, "packages.json")))
            {
                packagesJsonModificationTime = File.GetLastWriteTimeUtc(Path.Combine(baseDir, "packages.json"));
            }

            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Web.ProjectTemplates.5.0", "5.0.0");
            IReadOnlyList<InstallResult> installResult = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, TestContext.CancellationToken);

            Assert.ContainsSingle(installResult);
            Assert.IsTrue(installResult[0].Success);
            bootstrapper.Dispose();

            if (packagesJsonModificationTime == null)
            {
                Assert.IsFalse(File.Exists(Path.Combine(baseDir, "packages.json")));
            }
            else
            {
                Assert.AreEqual(packagesJsonModificationTime, File.GetLastWriteTimeUtc(Path.Combine(baseDir, "packages.json")));
            }

            Assert.IsFalse(Directory.Exists(hostDir));
        }

        [TestMethod]
        public async Task PhysicalConfigurationTest_WithChangedHostLocation()
        {
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            Assert.IsNotNull(userProfileDir);
            var unexpectedHostDir = Path.Combine(userProfileDir!, ".templateengine", nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString());
            var expectedHostDir = TestUtils.CreateTemporaryFolder();

            var builtIns = BuiltInTemplatePackagesProviderFactory.GetComponents(TestTemplatesLocation);
            var host = new DefaultTemplateEngineHost(nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString(), "1.0.0", null, builtIns, []);

            Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true, hostSettingsLocation: expectedHostDir);
            var result = await bootstrapper.GetTemplatesAsync(cancellationToken: TestContext.CancellationToken);
            Assert.IsNotEmpty(result);
            bootstrapper.Dispose();
            var hostDir = Path.Combine(expectedHostDir, nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString());
            Assert.IsTrue(Directory.Exists(hostDir));
            Assert.IsTrue(Directory.Exists(Path.Combine(hostDir, "1.0.0")));
            Assert.IsTrue(File.Exists(Path.Combine(hostDir, "1.0.0", "templatecache.json")));

            Assert.IsFalse(Directory.Exists(unexpectedHostDir));
        }
    }
}
