// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToUseFrameworkRoslyn : SdkTest
    {
        public GivenThatWeWantToUseFrameworkRoslyn(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void It_downloads_Microsoft_Net_Compilers_Toolset_Framework_when_requested()
        {
            const string testProjectName = "NetCoreApp";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "net6.0",
            };

            project.AdditionalProperties.Add("BuildWithNetFrameworkHostedCompiler", "true");

            var testAsset = _testAssetsManager
                .CreateTestProject(project);

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            var customPackagesDir = Path.Combine(testAsset.Path, "nuget-packages");

            testAsset.GetRestoreCommand(Log, relativePath: testProjectName)
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir)
                .Execute().Should().Pass();

            var toolsetPackageDir = Path.Combine(customPackagesDir, "microsoft.net.sdk.compilers.toolset");

            Assert.True(Directory.Exists(toolsetPackageDir));

            var toolsetPackageVersion = Directory.EnumerateDirectories(toolsetPackageDir).Should().ContainSingle().Subject;

            new BuildCommand(testAsset)
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir)
                .Execute().Should().Pass().And
                .HaveStdOutContaining(Path.Combine(toolsetPackageDir, toolsetPackageVersion, "csc.exe") + " /noconfig");
        }

        [FullMSBuildOnlyFact]
        public void It_downloads_Microsoft_Net_Compilers_Toolset_Framework_when_MSBuild_is_torn()
        {
            const string testProjectName = "NetCoreApp";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "net6.0",
            };

            // simulate mismatched MSBuild versions
            project.AdditionalProperties.Add("_IsDisjointMSBuildVersion", "true");

            var testAsset = _testAssetsManager
                .CreateTestProject(project);

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            var customPackagesDir = Path.Combine(testAsset.Path, "nuget-packages");

            testAsset.GetRestoreCommand(Log, relativePath: testProjectName)
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir)
                .Execute().Should().Pass();

            var toolsetPackageDir = Path.Combine(customPackagesDir, "microsoft.net.sdk.compilers.toolset");

            Assert.True(Directory.Exists(toolsetPackageDir));

            var toolsetPackageVersion = Directory.EnumerateDirectories(toolsetPackageDir).Should().ContainSingle().Subject;

            new BuildCommand(testAsset)
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir)
                .Execute().Should().Pass().And
                .HaveStdOutContaining(Path.Combine(toolsetPackageDir, toolsetPackageVersion, "csc.exe") + " /noconfig");
        }

        [FullMSBuildOnlyFact]
        public void It_throws_a_warning_when_adding_the_PackageReference_directly()
        {
            const string testProjectName = "NetCoreApp";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "net6.0",
            };

            project.PackageReferences.Add(new TestPackageReference("Microsoft.Net.Compilers.Toolset.Framework", "4.7.0-2.23260.7"));

            var testAsset = _testAssetsManager
                .CreateTestProject(project);

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            var result = restoreCommand.Execute();
            result.Should().Pass();
            result.Should().HaveStdOutContaining("NETSDK1205");
        }
    }
}
