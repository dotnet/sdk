// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACheckForDuplicateFrameworkReferences
    {
        [Fact]
        public void DetectsDuplicates()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(1);
            task.ItemsToRemove[0].ItemSpec.Should().Be("Microsoft.AspNetCore.App");
            task.ItemsToAdd.Should().HaveCount(1);
        }

        [Fact]
        public void NoDuplicates_NoOutput()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void NullFrameworkReferences_Succeeds()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = null,
            };

            task.Execute().Should().BeTrue("the null guard in ExecuteCore returns early");
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void EmptyArray_Succeeds()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
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
            task.ItemsToRemove.Should().HaveCount(1, "the implicit duplicate with empty ItemSpec should be removed");
        }

        [Fact]
        public void RelativePathItemSpec_PreservesFormat()
        {
            const string relativePathSpec = "some/relative/path.dll";
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem(relativePathSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem(relativePathSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();

            task.ItemsToRemove.Should().HaveCount(1);
            task.ItemsToRemove[0].ItemSpec.Should().Be(relativePathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");

            task.ItemsToAdd.Should().HaveCount(1);
            task.ItemsToAdd[0].ItemSpec.Should().Be(relativePathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
        }

        [Fact]
        public void MultipleExplicitDuplicates_LogsError()
        {
            var engine = new MockBuildEngine();
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = engine,
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute();
            engine.Errors.Should().NotBeEmpty("multiple explicit duplicates should trigger an error");
        }
    }
}
