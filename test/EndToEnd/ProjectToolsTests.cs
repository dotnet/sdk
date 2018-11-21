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
    public class ProjectToolsTests : TestBase, IClassFixture<ProjectToolsTests.TestPackagesFixture>
    {
        public string TestPackagesDirectory { get; private set; }
        public string TestNuGetCache { get; private set; }


        public ProjectToolsTests(TestPackagesFixture testPackagesFixture)
        {
            TestPackagesDirectory = testPackagesFixture.TestPackagesDirectory;
            TestNuGetCache = testPackagesFixture.TestNuGetCache;
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
                .WithEnvironmentVariable("NUGET_PACKAGES", TestNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", TestPackagesDirectory)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithEnvironmentVariable("NUGET_PACKAGES", TestNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", TestPackagesDirectory)
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
                .WithEnvironmentVariable("NUGET_PACKAGES", TestNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", TestPackagesDirectory)
                .Execute()
                .Should()
                .Pass();


            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .WithEnvironmentVariable("NUGET_PACKAGES", TestNuGetCache)
                .WithEnvironmentVariable("TEST_PACKAGES", TestPackagesDirectory)
                .ExecuteWithCapturedOutput("-d prefercliruntime")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello I prefer the cli runtime World!");;
        }

        public class TestPackagesFixture
        {
            public string TestPackagesDirectory { get; private set; }
            public string TestNuGetCache { get; private set; }

            public TestPackagesFixture()
            {
                TestPackagesDirectory = SetupTestPackages();

                TestNuGetCache = TestAssets.CreateTestDirectory(testProjectName: string.Empty,
                                           callingMethod: "packages",
                                           identifier: string.Empty)
                                .FullName;

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
                }

                var testPackageNames = new[]
                {
                    "dotnet-portable",
                    "dotnet-prefercliruntime"
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
}
