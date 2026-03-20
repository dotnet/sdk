// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACheckIfPackageReferenceShouldBeFrameworkReference
    {
        [Fact]
        public void MatchingPackage_ShouldRemoveAndAdd()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.All", new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeTrue();
            task.ShouldAddFrameworkReference.Should().BeTrue();
        }

        [Fact]
        public void FrameworkAlreadyExists_ShouldNotAdd()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.All", new Dictionary<string, string>()),
                },
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>()),
                },
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeTrue();
            task.ShouldAddFrameworkReference.Should().BeFalse("framework reference already exists");
        }

        [Fact]
        public void NoMatch_ShouldDoNothing()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("SomeOtherPackage", new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeFalse();
            task.ShouldAddFrameworkReference.Should().BeFalse();
        }

        [Fact]
        public void EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeFalse("empty ItemSpec does not match the package to replace");
            task.ShouldAddFrameworkReference.Should().BeFalse();
        }

        [Fact]
        public void EmptyArrays_Succeeds()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "SomePackage",
                FrameworkReferenceToUse = "SomeFramework",
                PackageReferences = Array.Empty<ITaskItem>(),
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeFalse();
            task.ShouldAddFrameworkReference.Should().BeFalse();
        }

        [Fact]
        public void RelativePathItemSpec_MatchesCorrectly()
        {
            const string pathLikeSpec = "some/path/Microsoft.AspNetCore.All";
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = pathLikeSpec,
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem(pathLikeSpec, new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeTrue(
                "string matching should work correctly with path-like ItemSpec");
            task.ShouldAddFrameworkReference.Should().BeTrue();
        }
    }
}
