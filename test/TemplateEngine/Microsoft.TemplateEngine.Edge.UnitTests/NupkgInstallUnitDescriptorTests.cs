using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class NupkgInstallUnitDescriptorTests : TestBase
    {
        private static readonly string VersionKey = nameof(NupkgInstallUnitDescriptor.Version);

        [Fact(DisplayName = nameof(NupkgDescriptorConstructorCreatesFromValuesTest))]
        public void NupkgDescriptorConstructorCreatesFromValuesTest()
        {
            Guid descriptorId = Guid.NewGuid();
            Guid mountPointId = new Guid("E0DE1C04-FD83-4BC5-BC4B-A675703F2266");
            string packageName = "TestPackage";
            string version = "1.2.3";
            string author = "TestAuthor";
            bool isOptional = true;

            IInstallUnitDescriptor descriptor = new NupkgInstallUnitDescriptor(descriptorId, mountPointId, packageName, isOptional, version, author);
            Assert.Equal(descriptorId, descriptor.DescriptorId);
            Assert.Equal(packageName, descriptor.Identifier);
            Assert.Equal(NupkgInstallUnitDescriptorFactory.FactoryId, descriptor.FactoryId);
            Assert.Equal(mountPointId, descriptor.MountPointId);
            Assert.Equal(version, descriptor.Details[VersionKey]);
            Assert.Equal(author, descriptor.Details[nameof(NupkgInstallUnitDescriptor.Author)]);
            Assert.Equal(isOptional, descriptor.IsPartOfAnOptionalWorkload);
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryCreatesFromDetailsTest))]
        public void NupkgDescriptorFactoryCreatesFromDetailsTest()
        {
            Guid descriptorId = Guid.NewGuid();
            Guid mountPointId = new Guid("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9");
            string packageName = "TestPackage";
            string version = "1.2.3";
            string author = "Microsoft";
            bool isOptional = true;

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { VersionKey, version },
                { nameof(NupkgInstallUnitDescriptor.Author), author }
            };

            Assert.True(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(descriptorId, packageName, mountPointId,
                isOptional, details, out IInstallUnitDescriptor descriptor));
            Assert.Equal(descriptorId, descriptor.DescriptorId);
            Assert.Equal(packageName, descriptor.Identifier);
            Assert.Equal(NupkgInstallUnitDescriptorFactory.FactoryId, descriptor.FactoryId);
            Assert.Equal(mountPointId, descriptor.MountPointId);

            Assert.Equal(version, descriptor.Details[VersionKey]);
            Assert.Equal(author, descriptor.Details[nameof(NupkgInstallUnitDescriptor.Author)]);
        }

        [Fact(DisplayName = nameof(InstallUnitDescriptorFactoryDispatchesToNupkgDescriptorFactoryAndCreatesDescriptorTest))]
        public void InstallUnitDescriptorFactoryDispatchesToNupkgDescriptorFactoryAndCreatesDescriptorTest()
        {
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(NupkgInstallUnitDescriptorFactory));

            string serializedDescriptor = @"
{
    ""3A06B18C-224E-46E3-95EB-8E411DB61B0B"":
    {
        ""FactoryId"": """ + NupkgInstallUnitDescriptorFactory.FactoryId.ToString() + @""",
        ""MountPointId"": ""D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9"",
        ""Identifier"": ""TestPackage"",
        ""Details"": {
            ""Version"": ""1.2.3"",
            ""Author"": ""Microsoft"",
        }
    }
}";
            JObject descriptorJObject = JObject.Parse(serializedDescriptor);
            JProperty descriptorProperty = descriptorJObject.Properties().First();

            Assert.True(InstallUnitDescriptorFactory.TryParse(EngineEnvironmentSettings, descriptorProperty, out IInstallUnitDescriptor parsedDescriptor));

            NupkgInstallUnitDescriptor nupkgDescriptor = parsedDescriptor as NupkgInstallUnitDescriptor;
            Assert.NotNull(nupkgDescriptor);

            Assert.Equal(NupkgInstallUnitDescriptorFactory.FactoryId, nupkgDescriptor.FactoryId);
            Assert.Equal("TestPackage", nupkgDescriptor.Identifier);
            Assert.Equal("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9", nupkgDescriptor.MountPointId.ToString(), ignoreCase: true);
            Assert.Equal("1.2.3", nupkgDescriptor.Version);

            Assert.Equal("1.2.3", nupkgDescriptor.Details["Version"]);
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryFailsOnMissingMountPointTest))]
        public void NupkgDescriptorFactoryFailsOnMissingMountPointTest()
        {
            string packageName = "TestPackage";
            string version = "1.2.3";
            bool isOptional = false;

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { VersionKey, version }
            };

            Assert.False(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(Guid.NewGuid(), packageName, Guid.Empty, isOptional, details, out _));
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryFailsOnMissingPackageNameTest))]
        public void NupkgDescriptorFactoryFailsOnMissingPackageNameTest()
        {
            Guid mountPointId = new Guid("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9");
            string version = "1.2.3";
            bool isOptional = false;

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
                { VersionKey, version }
            };

            Assert.False(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(Guid.Empty, null, mountPointId, isOptional, details, out _));
        }

        [Fact(DisplayName = nameof(NupkgDescriptorFactoryFailsOnMissingVersionTest))]
        public void NupkgDescriptorFactoryFailsOnMissingVersionTest()
        {
            Guid mountPointId = new Guid("D1ADBDAF-0382-4EEA-A43C-8356A8BEFAA9");
            string packageName = "TestPackage";
            bool isOptional = false;

            Dictionary<string, string> details = new Dictionary<string, string>()
            {
            };

            Assert.False(new NupkgInstallUnitDescriptorFactory().TryCreateFromDetails(Guid.NewGuid(), packageName, mountPointId, isOptional, details, out _));
        }
    }
}
