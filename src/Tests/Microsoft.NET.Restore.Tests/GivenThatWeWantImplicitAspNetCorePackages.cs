using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantImplicitAspNetCorePackages : SdkTest
    {
        public GivenThatWeWantImplicitAspNetCorePackages(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_warns_when_explicit_aspnet_package_ref_exists(string packageId)
        {
            const string testProjectName = "AspNetCoreWithExplicitRef";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                PackageReferences =
                {
                    new TestPackageReference(packageId, "2.1.0")
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(project)
                .Restore(Log, project.Name);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                project.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute()
                .Should().Pass()
                .And
                .HaveStdOutContaining("warning NETSDK1071:")
                .And
                .HaveStdOutContaining(testProjectName + ".csproj");

            LockFile lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var target =
                lockFile.GetTarget(NuGetFramework.Parse(".NETCoreApp,Version=v2.1"), null);
            var metapackageLibrary =
                target.Libraries.Single(l => l.Name == packageId);
            metapackageLibrary.Version.ToString().Should().Be("2.1.0");
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_warns_when_aspnet_package_ref_is_overridden(string packageId)
        {
            const string testProjectName = "AspNetCoreWithDuplicateRefRef";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                RuntimeFrameworkName = packageId,
                PackageReferences =
                {
                    new TestPackageReference(packageId, "2.1.1")
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(project)
                .Restore(Log, project.Name);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                project.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute()
                .Should().Pass()
                .And
                .HaveStdOutContaining("warning NETSDK1023:")
                .And
                .HaveStdOutContaining(testProjectName + ".csproj");
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_restores_when_RuntimeFrameworkName_is_set(string packageId)
        {
            const string testProjectName = "RuntimeFrameworkNameProject";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                RuntimeFrameworkName = packageId,
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(project)
                .Restore(Log, project.Name);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                project.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute()
                .Should().Pass()
                .And
                .NotHaveStdOutContaining("warning");;

            LockFile lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var target =
                lockFile.GetTarget(NuGetFramework.Parse(".NETCoreApp,Version=v2.1"), null);
            var aspnetLibrary =
                target.Libraries.Single(l => l.Name == packageId);
            aspnetLibrary.Version.ToString().Should().Be("2.1.1");

            var netcoreLibrary =
                target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netcoreLibrary.Version.ToString().Should().Be("2.1.1");
        }
    }
}
