// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACheckForImplicitPackageReferenceOverrides
    {
        [Fact]
        public void DetectsOverrides()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = new ITaskItem[]
                {
                    new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(2, "both the implicit and explicit duplicates are removed");
            task.ItemsToAdd.Should().HaveCount(1, "the explicit item is re-added with AllowExplicitVersion");
        }

        [Fact]
        public void NullPackageReferenceItems_Throws()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = null,
            };

            Action act = () => task.Execute();
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void EmptyArray_Succeeds()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(2, "both items with empty ItemSpec should be in remove list");
            task.ItemsToAdd.Should().HaveCount(1, "the explicit item should be re-added");
        }

        [Fact]
        public void RelativePathItemSpec_PreservesFormat()
        {
            const string pathLikeSpec = "some/path/Newtonsoft.Json";
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = new ITaskItem[]
                {
                    new MockTaskItem(pathLikeSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem(pathLikeSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();

            task.ItemsToRemove.Should().HaveCount(2);
            task.ItemsToRemove.Should().AllSatisfy(item =>
                item.ItemSpec.Should().Be(pathLikeSpec,
                    "output ItemSpec must preserve the exact input format without path normalization"));

            task.ItemsToAdd.Should().HaveCount(1);
            task.ItemsToAdd[0].ItemSpec.Should().Be(pathLikeSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
        }
    }
}
