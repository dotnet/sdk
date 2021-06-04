// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class ConfigurationTests
    {
        [Fact]
        internal async Task PhysicalConfigurationTest()
        {
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            var hostDir = Path.Combine(userProfileDir, ".templateengine", nameof(PhysicalConfigurationTest).ToString());
            try
            {
                var builtIns = new List<(Type, IIdentifiedComponent)>();
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory()));
                var host = new DefaultTemplateEngineHost(nameof(PhysicalConfigurationTest).ToString(), "1.0.0", null, builtIns, Array.Empty<string>());

                Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true);
                var result = await bootstrapper.GetTemplatesAsync(cancellationToken: default).ConfigureAwait(false);
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
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            var hostDir = Path.Combine(userProfileDir, ".templateengine", nameof(VirtualConfigurationTest).ToString());

            var builtIns = new List<(Type, IIdentifiedComponent)>();
            builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory()));
            var host = new DefaultTemplateEngineHost(nameof(VirtualConfigurationTest).ToString(), "1.0.0", null, builtIns, Array.Empty<string>());

            Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: true, loadDefaultComponents: true);
            var result = await bootstrapper.GetTemplatesAsync(cancellationToken: default).ConfigureAwait(false);
            Assert.True(result.Any());

            DateTime? packagesJsonModificationTime = null;
            if (File.Exists(Path.Combine(userProfileDir, ".templateengine", "packages.json")))
            {
                packagesJsonModificationTime = File.GetLastWriteTimeUtc(Path.Combine(userProfileDir, ".templateengine", "packages.json"));
            }

            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Web.ProjectTemplates.5.0", "5.0.0");
            IReadOnlyList<InstallResult> installResult = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(1, installResult.Count);
            Assert.True(installResult[0].Success);
            bootstrapper.Dispose();

            if (packagesJsonModificationTime == null)
            {
                Assert.False(File.Exists(Path.Combine(userProfileDir, ".templateengine", "packages.json")));
            }
            else
            {
                Assert.Equal(packagesJsonModificationTime, File.GetLastWriteTimeUtc(Path.Combine(userProfileDir, ".templateengine", "packages.json")));
            }

            Assert.False(Directory.Exists(hostDir));
        }

        [Fact]
        internal async Task PhysicalConfigurationTest_WithChangedHostLocation()
        {
            var userProfileDir = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME");
            var unexpectedHostDir = Path.Combine(userProfileDir, ".templateengine", nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString());
            var expectedHostDir = TestUtils.CreateTemporaryFolder();

            var builtIns = new List<(Type, IIdentifiedComponent)>();
            builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory()));
            var host = new DefaultTemplateEngineHost(nameof(PhysicalConfigurationTest_WithChangedHostLocation).ToString(), "1.0.0", null, builtIns, Array.Empty<string>());

            Bootstrapper bootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true, hostSettingsLocation: expectedHostDir);
            var result = await bootstrapper.GetTemplatesAsync(cancellationToken: default).ConfigureAwait(false);
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
