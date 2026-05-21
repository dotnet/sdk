// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACollatePackageDownloads
    {
        [Fact]
        public void GroupsVersions()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("NuGet.Common", new Dictionary<string, string> { { "Version", "5.0.0" } }),
                    new MockTaskItem("NuGet.Common", new Dictionary<string, string> { { "Version", "6.0.0" } }),
                    new MockTaskItem("NuGet.Protocol", new Dictionary<string, string> { { "Version", "5.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().HaveCount(2, "two distinct package names");

            var nugetCommon = Array.Find(task.PackageDownloads, p => p.ItemSpec == "NuGet.Common");
            nugetCommon.Should().NotBeNull();
            nugetCommon!.GetMetadata("Version").Should().Contain("[5.0.0]").And.Contain("[6.0.0]");
        }

        [Fact]
        public void SingleVersion()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].GetMetadata("Version").Should().Be("[1.0.0]");
        }

        [Fact]
        public void NullPackages_Throws()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = null,
            };

            Action act = () => task.Execute();
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void EmptyArray_Succeeds()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().BeEmpty();
        }

        [Fact]
        public void EmptyItemSpec_HandlesGracefully()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].ItemSpec.Should().BeEmpty();
            task.PackageDownloads[0].GetMetadata("Version").Should().Be("[1.0.0]");
        }

        [Theory]
        [InlineData("packages/NuGet.Common")]
        public void PathItemSpec_PreservesFormat(string pathSpec)
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem(pathSpec, new Dictionary<string, string> { { "Version", "5.0.0" } }),
                    new MockTaskItem(pathSpec, new Dictionary<string, string> { { "Version", "6.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();

            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].ItemSpec.Should().Be(pathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
            task.PackageDownloads[0].GetMetadata("Version").Should().Contain("[5.0.0]").And.Contain("[6.0.0]");
        }

        // Backslash is only a path separator on Windows. On Linux, TaskItem normalizes '\' to '/'.
        [WindowsOnlyFact]
        public void PathItemSpec_PreservesBackslashOnWindows()
        {
            var pathSpec = "packages\\NuGet.Common";
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem(pathSpec, new Dictionary<string, string> { { "Version", "5.0.0" } }),
                    new MockTaskItem(pathSpec, new Dictionary<string, string> { { "Version", "6.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();

            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].ItemSpec.Should().Be(pathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
            task.PackageDownloads[0].GetMetadata("Version").Should().Contain("[5.0.0]").And.Contain("[6.0.0]");
        }
    }
}
