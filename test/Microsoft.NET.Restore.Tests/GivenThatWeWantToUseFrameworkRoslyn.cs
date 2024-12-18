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

        [FullMSBuildOnlyFact]
        public void It_throws_an_error_when_the_package_is_not_downloaded()
        {
            const string testProjectName = "NetCoreApp";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "net6.0",
            };
            
            project.AdditionalProperties.Add("BuildWithNetFrameworkHostedCompiler", "false");

            var testAsset = _testAssetsManager
                .CreateTestProject(project);

            var customPackagesDir = Path.Combine(testAsset.Path, "nuget-packages");

            testAsset.GetRestoreCommand(Log, relativePath: testProjectName)
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir)
                .Execute().Should().Pass();

            var buildCommand = (BuildCommand)new BuildCommand(testAsset)
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir);
            buildCommand.ExecuteWithoutRestore("/p:BuildWithNetFrameworkHostedCompiler=true")
                .Should().Fail().And.HaveStdOutContaining("NETSDK1216");
        }

        [FullMSBuildOnlyFact]
        public void It_throws_a_warning_when_NuGetPackageRoot_is_empty()
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

            var command = (MSBuildCommand)new MSBuildCommand(testAsset, "Restore;Build")
                .WithEnvironmentVariable("NUGET_PACKAGES", customPackagesDir);
            command.ExecuteWithoutRestore()
                .Should().Pass().And.HaveStdOutContaining("NETSDK1221");

            // The package is downloaded, but the targets cannot find the path to it
            // because NuGetPackageRoot is empty during `/t:Restore;Build`.
            // See https://github.com/dotnet/sdk/issues/43016.
            var toolsetPackageDir = Path.Combine(customPackagesDir, "microsoft.net.sdk.compilers.toolset");
            new DirectoryInfo(toolsetPackageDir).Should().Exist();
        }

        [FullMSBuildOnlyFact] // https://github.com/dotnet/sdk/issues/44605
        public void It_does_not_throw_a_warning_when_NuGetPackageRoot_is_empty_in_wpftmp()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopWpf")
                .WithSource();
                
            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            var buildCommand = new BuildCommand(testAsset, relativePathToProject: "FxWpf")
            {
                WorkingDirectory = Path.Combine(testAsset.Path, "FxWpf")
            };

            // simulate mismatched MSBuild versions via _IsDisjointMSBuildVersion
            buildCommand.Execute("-p:_IsDisjointMSBuildVersion=true")
                .Should().Pass().And.NotHaveStdOutContaining("NETSDK1221");

            Assert.True(File.Exists(Path.Combine(testAsset.Path, "obj", "net472", "MainWindow.g.cs")));
        }
    }
}
