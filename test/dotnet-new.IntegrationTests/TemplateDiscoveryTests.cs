// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class TemplateDiscoveryTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private static TemplateDiscoveryTool s_templateDiscoveryTool = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_templateDiscoveryTool = new TemplateDiscoveryTool(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_templateDiscoveryTool?.Dispose();

        private TemplateDiscoveryTool _templateDiscoveryTool => s_templateDiscoveryTool;

        [TestMethod]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
        [Microsoft.NET.TestFramework.ArchitectureCondition(ConditionMode.Exclude, Architecture.Arm64)]
        public async Task CanRunDiscoveryTool()
        {
            string testDir = CreateTemporaryFolder();
            string testTemplatesPackagePath = PackTestNuGetPackage(_log);
            using var packageManager = new PackageManager();
            string packagePath = await packageManager.GetNuGetPackage(
                templatePackName: "Microsoft.Azure.WebJobs.ProjectTemplates",
                downloadDirectory: Path.GetDirectoryName(testTemplatesPackagePath));

            _templateDiscoveryTool.Run(
                _log,
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packagePath) ?? throw new Exception("Couldn't get package location directory"),
                "-v");

            string[] cacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            var settingsPath = CreateTemporaryFolder();

            foreach (var cacheFilePath in cacheFilePaths)
            {
                Assert.IsTrue(File.Exists(cacheFilePath));
                new DotnetNewCommand(_log)
                    .WithCustomHive(settingsPath)
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr();

                new DotnetNewCommand(_log, "search", "func")
                    .WithCustomHive(settingsPath)
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

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanReadCliData()
        {
            string testDir = CreateTemporaryFolder();
            string packageLocation = PackTestNuGetPackage(_log);

            _templateDiscoveryTool.Run(
                _log,
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v");

            string[] cacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            var settingsPath = CreateTemporaryFolder();
            CheckTemplateOptionsSearch(cacheFilePaths, settingsPath);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/42541")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanReadCliDataFromDiff()
        {
            string testDir = CreateTemporaryFolder();
            string packageLocation = PackTestNuGetPackage(_log);

            _templateDiscoveryTool.Run(
                _log,
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "false");

            string[] cacheFilePaths = new[]
            {
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json"),
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json")
            };
            var settingsPath = CreateTemporaryFolder();
            CheckTemplateOptionsSearch(cacheFilePaths, settingsPath);

            string testDir2 = CreateTemporaryFolder();
            _templateDiscoveryTool.Run(
                _log,
                "--basePath",
                testDir2,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation) ?? throw new Exception("Couldn't get package location directory"),
                "-v",
                "--diff",
                "true",
                "--diff-override-cache",
                Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfoVer2.json"))
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
                Assert.IsTrue(File.Exists(cacheFilePath));
                new DotnetNewCommand(_log)
                      .WithCustomHive(settingsPath)
                      .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                      .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                      .Execute()
                      .Should()
                      .ExitWith(0)
                      .And.NotHaveStdErr();

                new DotnetNewCommand(_log, "search", "CliHostFile")
                    .WithCustomHive(settingsPath)
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Exception")
                    .And.HaveStdOutContaining("TestAssets.TemplateWithCliHostFile")
                    .And.HaveStdOutContaining("Microsoft.TemplateEngine.TestTemplates");

                new DotnetNewCommand(_log, "search", "--param")
                     .WithCustomHive(settingsPath)
                     .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                     .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                     .Execute()
                     .Should()
                     .ExitWith(0)
                     .And.NotHaveStdErr()
                     .And.NotHaveStdOutContaining("Exception")
                     .And.HaveStdOutContaining("TestAssets.TemplateWithCliHostFile")
                     .And.HaveStdOutContaining("Microsoft.TemplateEngine.TestTemplates");

                new DotnetNewCommand(_log, "search", "-p")
                    .WithCustomHive(settingsPath)
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And.NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Exception")
                    .And.HaveStdOutContaining("TestAssets.TemplateWithCliHostFile")
                    .And.HaveStdOutContaining("Microsoft.TemplateEngine.TestTemplates");

                new DotnetNewCommand(_log, "search", "--test-param")
                    .WithCustomHive(settingsPath)
                    .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                    .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                    .Execute()
                    .Should().Fail();
            }
        }
    }
}
