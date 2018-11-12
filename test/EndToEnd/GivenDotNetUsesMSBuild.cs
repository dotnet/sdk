// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : TestBase
    {
        private string _testPackagesDirectory;
        private string _testNuGetCache;

        public GivenDotNetUsesMSBuild()
        {
            _testPackagesDirectory = SetupTestPackages();

            _testNuGetCache = TestAssets.CreateTestDirectory(testProjectName: string.Empty,
                                           callingMethod: "packages",
                                           identifier: string.Empty)
                                .FullName;
        }

        [Fact]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;
            
            string newArgs = "console --debug:ephemeral-hive --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            var runCommand = new RunCommand()
                .WithWorkingDirectory(projectDirectory);

            //  Set DOTNET_ROOT as workaround for https://github.com/dotnet/cli/issues/10196
            var dotnetRoot = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);
            if (!string.IsNullOrEmpty(dotnetRoot))
            {
                runCommand = runCommand.WithEnvironmentVariable(Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                    dotnetRoot);
            }

            runCommand.ExecuteWithCapturedOutput()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World!");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [Fact]
        public void ItCanRunToolsInACSProj()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                                         .CreateInstance()
                                         .WithSourceFiles();
         
            var testProjectDirectory = testInstance.Root;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .ExecuteWithCapturedOutput("-d portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!");;
        }

        [Fact]
        public void ItCanRunToolsThatPrefersTheCliRuntimeEvenWhenTheToolItselfDeclaresADifferentRuntime()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                                         .CreateInstance()
                                         .WithSourceFiles();

            var testProjectDirectory = testInstance.Root;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .Execute()
                .Should()
                .Pass();


            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .ExecuteWithCapturedOutput("-d prefercliruntime")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello I prefer the cli runtime World!");;
        }

        [Fact]
        public void ItCanRunAToolThatInvokesADependencyToolInACSProj()
        {
            var repoDirectoriesProvider = new RepoDirectoriesProvider();

            var testInstance = TestAssets.Get("TestAppWithProjDepTool")
                                         .CreateInstance()
                                         .WithSourceFiles();

            var configuration = "Debug";

            var testProjectDirectory = testInstance.Root;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .Execute()
                .Should()
                .Pass();

            new BuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .Execute($"-c {configuration} ")
                .Should()
                .Pass();

            new DotnetCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithEnvironmentVariable("NUGET_PACKAGES", _testNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", _testPackagesDirectory)
                .ExecuteWithCapturedOutput(
                    $"-d dependency-tool-invoker -c {configuration} -f netcoreapp3.0 portable")
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello Portable World!");
        }

        [Fact]
        public void BuildTestPackages()
        {
            
        }

        private string SetupTestPackages()
        {
            var directory = TestAssets.CreateTestDirectory(
                testProjectName: string.Empty,
                callingMethod: "TestPackages",
                identifier: string.Empty);

            string testPackagesDirectory = Path.Combine(directory.FullName, "testPackages");

            if (!Directory.Exists(testPackagesDirectory))
            {
                new DirectoryInfo(testPackagesDirectory).Create();
                //Directory.CreateDirectory(testPackagesDirectory);
            }

            var testPackageNames = new[]
            {
                "dotnet-portable",
                "dotnet-prefercliruntime",
                "dotnet-dependency-tool-invoker"
            };

            foreach (var testPackageName in testPackageNames)
            {
                

                var assetInfo = TestAssets.Get(TestAssetKinds.TestPackages, testPackageName);

                var testProjectDirectory = new DirectoryInfo(Path.Combine(directory.FullName, testPackageName));

                if (!testProjectDirectory.Exists)
                {
                    testProjectDirectory.Create();
                }

                var testInstance = new TestAssetInstance(assetInfo, testProjectDirectory)
                    .WithSourceFiles()
                    .WithRestoreFiles();

                new PackCommand()
                    .WithWorkingDirectory(testProjectDirectory)
                    .Execute()
                    .Should()
                    .Pass();

                string nupkgFilePathInOutput = Directory.GetFiles(Path.Combine(testProjectDirectory.FullName, "bin", "Debug"), "*.nupkg")
                    .Single();

                string nupkgFile = Path.Combine(testPackagesDirectory, Path.GetFileName(nupkgFilePathInOutput));

                File.Copy(nupkgFilePathInOutput, nupkgFile);

            }

            return testPackagesDirectory;
        }
    }
}
