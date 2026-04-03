// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.IntegrationTests
{
    public class TemplateDiscoveryTests : TestBase
    {
        private readonly ITestOutputHelper _log;

        public TemplateDiscoveryTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public async Task CanRunDiscoveryTool()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);
            packageLocation = await packageManager.GetNuGetPackage("Microsoft.Azure.WebJobs.ProjectTemplates");

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            string[] cacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            var settingsPath = TestUtils.CreateTemporaryFolder();

            foreach (var cacheFilePath in cacheFilePaths)
            {
                Assert.True(File.Exists(cacheFilePath));
                new DotnetNewCommand(_log)
                    .WithCustomHive(settingsPath)
                    .WithoutTelemetry()
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr();

                new DotnetNewCommand(_log, "func", "--search")
                    .WithCustomHive(settingsPath)
                    .WithoutTelemetry()
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Exception")
                    .And.HaveStdOutContaining("Microsoft.Azure.WebJobs.ProjectTemplates");

                new DotnetNewCommand(_log)
                      .WithCustomHive(settingsPath)
                      .WithoutTelemetry()
                      .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                      .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                      .Execute()
                      .Should()
                      .ExitWith(0)
                      .And.NotHaveStdErr();

                new DotnetNewCommand(_log, "func", "--search")
                    .WithCustomHive(settingsPath)
                    .WithoutTelemetry()
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Exception")
                    .And.HaveStdOutContaining("Microsoft.Azure.WebJobs.ProjectTemplates");
            }
        }

        [Fact]
        public void CanReadAuthor()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            var jObjectV1 = JsonNode.Parse(File.ReadAllText(Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json")))!.AsObject();
            Assert.Equal("TestAuthor", jObjectV1!["PackToTemplateMap"]!.AsObject().Single(p => p.Key.StartsWith("Microsoft.TemplateEngine.TestTemplates")).Value!["Owners"]!.AsArray().Select(n => n!.GetValue<string>()).Single());
            var jObjectV2 = JsonNode.Parse(File.ReadAllText(Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")))!.AsObject();
            Assert.Equal("TestAuthor", jObjectV2!["TemplatePackages"]![0]!["Owners"]!.GetValue<string>());
        }

        [Fact]
        public void CanReadDescription()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            var jObjectV2 = JsonNode.Parse(File.ReadAllText(Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")))!.AsObject();
            Assert.Equal("description", jObjectV2!["TemplatePackages"]![0]!["Description"]!.GetValue<string>());
        }

        [Fact]
        public void CanReadIconUrl()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            var jObjectV2 = JsonNode.Parse(File.ReadAllText(Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")))!.AsObject();
            Assert.Equal("https://icon", jObjectV2!["TemplatePackages"]![0]!["IconUrl"]!.GetValue<string>());
        }

        [Fact]
        public async Task CanDetectNewPackagesInDiffMode()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            File.Move(packageLocation, Path.Combine(Path.GetDirectoryName(packageLocation)!, "Test.Templates##1.0.0.nupkg"));

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "false")
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining(
@"Template packages:
   new: 1
      Test.Templates@1.0.0
   updated: 0
   removed: 0
   not changed: 0")
                .And.HaveStdOutContaining(
@"Non template packages:
   new: 0
   updated: 0
   removed: 0
   not changed: 0");

            string cacheV1Path = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json");
            string cacheV2Path = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json");
            string nonTemplatePackagesList = Path.Combine(testDir, "SearchCache", "nonTemplatePacks.json");

            Assert.True(File.Exists(cacheV1Path));
            Assert.True(File.Exists(cacheV2Path));
            Assert.True(File.Exists(nonTemplatePackagesList));

            packageLocation = await packageManager.GetNuGetPackage("Microsoft.Azure.WebJobs.ProjectTemplates");

            File.Move(packageLocation, Path.Combine(Path.GetDirectoryName(packageLocation)!, "Microsoft.Azure.WebJobs.ProjectTemplates##1.0.0.nupkg"));

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "true",
                "--diff-override-cache",
                cacheV2Path)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining(
@"Template packages:
   new: 1
      Microsoft.Azure.WebJobs.ProjectTemplates@1.0.0
   updated: 0
   removed: 0
   not changed: 1")
                .And.HaveStdOutContaining(
@"Non template packages:
   new: 0
   updated: 0
   removed: 0
   not changed: 0");

            Assert.True(File.Exists(cacheV1Path));
            Assert.True(File.Exists(cacheV2Path));
            Assert.True(File.Exists(nonTemplatePackagesList));

            var jObjectV1 = JsonNode.Parse(File.ReadAllText(cacheV1Path))!.AsObject();
            Assert.Equal(2, jObjectV1["PackToTemplateMap"]?.AsObject().Count);
            var jObjectV2 = JsonNode.Parse(File.ReadAllText(cacheV2Path))!.AsObject();
            Assert.Equal(2, jObjectV2["TemplatePackages"]?.AsArray().Count);
        }

        [Fact]
        public void CanDetectUpdatedPackagesInDiffMode()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            string testFileName = Path.Combine(Path.GetDirectoryName(packageLocation)!, "Test.Templates##1.0.0.nupkg");
            File.Move(packageLocation, testFileName);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "false")
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining(
@"Template packages:
   new: 1
      Test.Templates@1.0.0
   updated: 0
   removed: 0
   not changed: 0")
                .And.HaveStdOutContaining(
@"Non template packages:
   new: 0
   updated: 0
   removed: 0
   not changed: 0");

            string cacheV1Path = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json");
            string cacheV2Path = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json");
            string nonTemplatePackagesList = Path.Combine(testDir, "SearchCache", "nonTemplatePacks.json");

            Assert.True(File.Exists(cacheV1Path));
            Assert.True(File.Exists(cacheV2Path));
            Assert.True(File.Exists(nonTemplatePackagesList));

            File.Move(testFileName, Path.Combine(Path.GetDirectoryName(testFileName)!, "Test.Templates##1.0.1.nupkg"));

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "true",
                "--diff-override-cache",
                cacheV2Path)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining(
@"Template packages:
   new: 0
   updated: 1
      Test.Templates, 1.0.0 --> 1.0.1
   removed: 0
   not changed: 0")
                .And.HaveStdOutContaining(
@"Non template packages:
   new: 0
   updated: 0
   removed: 0
   not changed: 0");

            Assert.True(File.Exists(cacheV1Path));
            Assert.True(File.Exists(cacheV2Path));
            Assert.True(File.Exists(nonTemplatePackagesList));

            var jObjectV1 = JsonNode.Parse(File.ReadAllText(cacheV1Path))!.AsObject();
            Assert.Equal(1, jObjectV1["PackToTemplateMap"]?.AsObject().Count);
            var jObjectV2 = JsonNode.Parse(File.ReadAllText(cacheV2Path))!.AsObject();
            Assert.Equal(1, jObjectV2["TemplatePackages"]?.AsArray().Count);
            Assert.Equal("1.0.1", jObjectV2["TemplatePackages"]?[0]?["Version"]?.GetValue<string>());
        }

        [Fact]
        public void CanDetectRemovedPackagesInDiffMode()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            string testFileName = Path.Combine(Path.GetDirectoryName(packageLocation)!, "Test.Templates##1.0.0.nupkg");
            File.Move(packageLocation, testFileName);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "false")
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining(
@"Template packages:
   new: 1
      Test.Templates@1.0.0
   updated: 0
   removed: 0
   not changed: 0")
                .And.HaveStdOutContaining(
@"Non template packages:
   new: 0
   updated: 0
   removed: 0
   not changed: 0");

            string cacheV1Path = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json");
            string cacheV2Path = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json");
            string nonTemplatePackagesList = Path.Combine(testDir, "SearchCache", "nonTemplatePacks.json");

            Assert.True(File.Exists(cacheV1Path));
            Assert.True(File.Exists(cacheV2Path));
            Assert.True(File.Exists(nonTemplatePackagesList));

            File.Delete(testFileName);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "true",
                "--diff-override-cache",
                cacheV2Path)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining(
@"Template packages:
   new: 0
   updated: 0
   removed: 1
      Test.Templates@1.0.0
   not changed: 0")
                .And.HaveStdOutContaining(
@"Non template packages:
   new: 0
   updated: 0
   removed: 0
   not changed: 0")
                .And.HaveStdOutContaining(
@"[Error]: the following 1 packages were removed
   Test.Templates@1.0.0
Checking template packages via API: 
Package Test.Templates was unlisted.");

            Assert.True(File.Exists(cacheV1Path));
            Assert.True(File.Exists(cacheV2Path));
            Assert.True(File.Exists(nonTemplatePackagesList));

            var jObjectV1 = JsonNode.Parse(File.ReadAllText(cacheV1Path))!.AsObject();
            Assert.Equal(0, jObjectV1["PackToTemplateMap"]?.AsObject().Count);
            var jObjectV2 = JsonNode.Parse(File.ReadAllText(cacheV2Path))!.AsObject();
            Assert.Equal(0, jObjectV2["TemplatePackages"]?.AsArray().Count);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanReadCliData()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            string[] cacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            var settingsPath = TestUtils.CreateTemporaryFolder();
            CheckTemplateOptionsSearch(cacheFilePaths, settingsPath);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanReadCliDataFromDiff()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = PackTestTemplatesNuGetPackage(packageManager);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "false")
                .Execute()
                .Should()
                .ExitWith(0);

            string[] cacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            var settingsPath = TestUtils.CreateTemporaryFolder();
            CheckTemplateOptionsSearch(cacheFilePaths, settingsPath);

            string testDir2 = TestUtils.CreateTemporaryFolder();
            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir2,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "true",
                "--diff-override-cache",
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json"))
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("not changed: 1");

            string[] updatedCacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            CheckTemplateOptionsSearch(updatedCacheFilePaths, settingsPath);
        }

        private void CheckTemplateOptionsSearch(IEnumerable<string> cacheFilePaths, string settingsPath)
        {
            foreach (var cacheFilePath in cacheFilePaths)
            {
                Assert.True(File.Exists(cacheFilePath));
                new DotnetNewCommand(_log)
                      .WithCustomHive(settingsPath)
                      .WithoutTelemetry()
                      .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                      .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                      .Execute()
                      .Should()
                      .ExitWith(0)
                      .And.NotHaveStdErr();

                new DotnetNewCommand(_log, "CliHostFile", "--search")
                    .WithCustomHive(settingsPath)
                    .WithoutTelemetry()
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Exception")
                    .And.HaveStdOutContaining("TestAssets.TemplateWithCliHostFile")
                    .And.HaveStdOutContaining("Microsoft.TemplateEngine.TestTemplates");

                new DotnetNewCommand(_log, "--search", "--param")
                     .WithCustomHive(settingsPath)
                     .WithoutTelemetry()
                     .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                     .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                     .Execute()
                     .Should()
                     .ExitWith(0)
                     .And.NotHaveStdErr()
                     .And.NotHaveStdOutContaining("Exception")
                     .And.HaveStdOutContaining("TestAssets.TemplateWithCliHostFile")
                     .And.HaveStdOutContaining("Microsoft.TemplateEngine.TestTemplates");

                new DotnetNewCommand(_log, "--search", "-p")
                    .WithCustomHive(settingsPath)
                    .WithoutTelemetry()
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Exception")
                    .And.HaveStdOutContaining("TestAssets.TemplateWithCliHostFile")
                    .And.HaveStdOutContaining("Microsoft.TemplateEngine.TestTemplates");

                new DotnetNewCommand(_log, "--search", "--test-param")
                    .WithCustomHive(settingsPath)
                    .WithoutTelemetry()
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should().Fail();
            }
        }
    }
}
