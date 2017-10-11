using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class NupkgInstallUnitDescriptorTests : TestBase
    {
        private static readonly string MountPointIdKey = nameof(NupkgInstallUnitDescriptor.MountPointId);
        private static readonly string PackageNameKey = nameof(NupkgInstallUnitDescriptor.PackageName);
        private static readonly string VersionKey = nameof(NupkgInstallUnitDescriptor.Version);

        [Fact(DisplayName = nameof(NupkgDescriptorConstructorCreatesFromValuesTest))]
        public void NupkgDescriptorConstructorCreatesFromValuesTest()
        {
            Guid mountPointId = new Guid("E0DE1C04-FD83-4BC5-BC4B-A675703F2266");
            string packageName = "TestPackage";
            string version = "1.2.3";

            IInstallUnitDescriptor descriptor = new NupkgInstallUnitDescriptor(mountPointId, packageName, version);
            Assert.Equal(packageName, descriptor.Identifier);
            Assert.Equal(NupkgInstallUnitDescriptorFactory.FactoryId, descriptor.FactoryId);
            Assert.Equal(mountPointId, descriptor.MountPointId);

            Assert.True(Guid.TryParse(descriptor.Details[MountPointIdKey], out Guid detailsMountPointId));
            Assert.Equal(mountPointId, detailsMountPointId);

            Assert.Equal(packageName, descriptor.Details[PackageNameKey]);
            Assert.Equal(version, descriptor.Details[VersionKey]);
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryCreatesFromDetailsTest))]
        public void NupkgDescriptorFactoryCreatesFromDetailsTest()
        {
            Guid mountPointId = new Guid("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9");
            string packageName = "TestPackage";
            string version = "1.2.3";

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { MountPointIdKey, mountPointId.ToString() },
                { PackageNameKey, packageName },
                { VersionKey, version }
            };

            Assert.True(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(details, out IInstallUnitDescriptor descriptor));
            Assert.Equal(packageName, descriptor.Identifier);
            Assert.Equal(NupkgInstallUnitDescriptorFactory.FactoryId, descriptor.FactoryId);
            Assert.Equal(mountPointId, descriptor.MountPointId);

            Assert.True(Guid.TryParse(descriptor.Details[MountPointIdKey], out Guid detailsMountPointId));
            Assert.Equal(mountPointId, detailsMountPointId);

            Assert.Equal(packageName, descriptor.Details[PackageNameKey]);
            Assert.Equal(version, descriptor.Details[VersionKey]);
        }

        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryDispatchesToNupkgDescriptorFactoryAndCreatesDescriptorTest))]
        public void InstallUnitDescriptorFactoryDispatchesToNupkgDescriptorFactoryAndCreatesDescriptorTest()
        {
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(NupkgInstallUnitDescriptorFactory));

            string serializedDescriptor = @"
{
    ""FactoryId"": """  + NupkgInstallUnitDescriptorFactory.FactoryId.ToString() + @""",
    ""Details"": {
        ""MountPointId"": ""D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9"",
        ""PackageName"": ""TestPackage"",
        ""Version"": ""1.2.3""
    }
}";
            JObject descriptorJObject = JObject.Parse(serializedDescriptor);
            Assert.True(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, descriptorJObject, out IInstallUnitDescriptor parsedDescriptor));

            NupkgInstallUnitDescriptor nupkgDescriptor = parsedDescriptor as NupkgInstallUnitDescriptor;
            Assert.NotNull(nupkgDescriptor);

            Assert.Equal(NupkgInstallUnitDescriptorFactory.FactoryId, nupkgDescriptor.FactoryId);
            Assert.Equal("TestPackage", nupkgDescriptor.Identifier);
            Assert.Equal("TestPackage", nupkgDescriptor.PackageName);
            Assert.Equal("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9", nupkgDescriptor.MountPointId.ToString(), ignoreCase: true);
            Assert.Equal("1.2.3", nupkgDescriptor.Version);

            Assert.Equal("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9", nupkgDescriptor.Details["MountPointId"], ignoreCase: true);
            Assert.Equal("TestPackage", nupkgDescriptor.Details["PackageName"]);
            Assert.Equal("1.2.3", nupkgDescriptor.Details["Version"]);
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryFailsOnMissingMountPointTest))]
        public void NupkgDescriptorFactoryFailsOnMissingMountPointTest()
        {
            string packageName = "TestPackage";
            string version = "1.2.3";

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { PackageNameKey, packageName },
                { VersionKey, version }
            };

            Assert.False(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(details, out IInstallUnitDescriptor descriptor));
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryFailsOnMissingPackageNameTest))]
        public void NupkgDescriptorFactoryFailsOnMissingPackageNameTest()
        {
            Guid mountPointId = new Guid("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9");
            string version = "1.2.3";

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { MountPointIdKey, mountPointId.ToString() },
                { VersionKey, version }
            };

            Assert.False(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(details, out IInstallUnitDescriptor descriptor));
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryFailsOnMissingVersionTest))]
        public void NupkgDescriptorFactoryFailsOnMissingVersionTest()
        {
            Guid mountPointId = new Guid("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9");
            string packageName = "TestPackage";

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { MountPointIdKey, mountPointId.ToString() },
                { PackageNameKey, packageName },
            };

            Assert.False(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(details, out IInstallUnitDescriptor descriptor));
        }
    }
}
