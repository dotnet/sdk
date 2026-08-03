// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Microsoft.NET.Build.Tasks.UnitTests.Mocks;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAPackageOverrideResolver
    {
        [TestMethod]
        public void ItMergesPackageOverridesUsingHighestVersion()
        {
            ITaskItem[] packageOverrides = new[]
            {
                new MockTaskItem("Platform", new Dictionary<string, string>
                {
                    { MetadataKeys.OverriddenPackages, "System.Ben|4.2.0;System.Immo|4.2.0;System.Livar|4.3.0;System.Dave|4.2.0" }
                }),
                new MockTaskItem("Platform", new Dictionary<string, string>
                {
                    { MetadataKeys.OverriddenPackages, "System.Ben|4.2.0;System.Immo|4.3.0;System.Livar|4.2.0;System.Nick|4.2.0" }
                })
            };

            var resolver = new PackageOverrideResolver<MockConflictItem>(packageOverrides);

            Assert.ContainsSingle(resolver.PackageOverrides);

            PackageOverride packageOverride = resolver.PackageOverrides["Platform"];
            Assert.HasCount(5, packageOverride.OverriddenPackages);
            Assert.AreEqual(new NuGetVersion(4, 2, 0), packageOverride.OverriddenPackages["System.Ben"]);
            Assert.AreEqual(new NuGetVersion(4, 3, 0), packageOverride.OverriddenPackages["System.Immo"]);
            Assert.AreEqual(new NuGetVersion(4, 3, 0), packageOverride.OverriddenPackages["System.Livar"]);
            Assert.AreEqual(new NuGetVersion(4, 2, 0), packageOverride.OverriddenPackages["System.Dave"]);
            Assert.AreEqual(new NuGetVersion(4, 2, 0), packageOverride.OverriddenPackages["System.Nick"]);
        }

        [TestMethod]
        public void ItHandlesNullITaskItemArray()
        {
            var resolver = new PackageOverrideResolver<MockConflictItem>(null);

            Assert.IsNull(resolver.PackageOverrides);
            Assert.IsNull(resolver.Resolve(new MockConflictItem(), new MockConflictItem()));
        }

        [TestMethod]
        public void ItHandlesNullPackageIds()
        {
            ITaskItem[] packageOverrides = new[]
            {
                new MockTaskItem("FakePlatform", new Dictionary<string, string>
                {
                    { MetadataKeys.OverriddenPackages, "System.Ben|4.2.0;System.Jobst-Immo|4.2.0;System.Livar|4.3.0;System.Dave|4.2.0" }
                })
            };

            var resolver = new PackageOverrideResolver<MockConflictItem>(packageOverrides);

            Assert.IsNotNull(resolver.PackageOverrides);

            var packageItem = new MockConflictItem("System.Eric")
            {
                PackageId = "System.Eric",
                PackageVersion = new NuGetVersion(4, 0, 0),
                AssemblyVersion = new Version(4, 0, 0, 0),
                ItemType = ConflictItemType.Reference
            };

            var platformItem = new MockConflictItem("System.Eric")
            {
                PackageId = null,
                AssemblyVersion = new Version(4, 1, 0, 0),
                ItemType = ConflictItemType.Platform
            };

            Assert.IsNull(resolver.Resolve(packageItem, platformItem));
            Assert.IsNull(resolver.Resolve(platformItem, packageItem));

            var packageItem2 = new MockConflictItem("System.Eric")
            {
                PackageId = "FakePlatform",
                PackageVersion = new NuGetVersion(4, 0, 0),
                AssemblyVersion = new Version(4, 0, 0, 0),
                ItemType = ConflictItemType.Reference
            };

            Assert.IsNull(resolver.Resolve(packageItem2, platformItem));
            Assert.IsNull(resolver.Resolve(platformItem, packageItem2));
        }
    }
}
