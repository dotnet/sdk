using System.IO;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.IntegrationTests
{
    public class TemplateDiscoveryTests
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
            string packageLocation = packageManager.PackTestTemplatesNuGetPackage();
            packageLocation = await packageManager.GetNuGetPackage("Microsoft.Azure.WebJobs.ProjectTemplates").ConfigureAwait(false);

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            string cacheFilePath = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json");
            Assert.True(File.Exists(cacheFilePath));

            var settingsPath = TestUtils.CreateTemporaryFolder();

            new DotnetNew3Command(_log)
                  .WithCustomHive(settingsPath)
                  .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                  .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And.NotHaveStdErr();

            new DotnetNew3Command(_log, "func", "--search")
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

        [Fact]
        public void CanReadCliData()
        {
            string testDir = TestUtils.CreateTemporaryFolder();
            using var packageManager = new PackageManager();
            string packageLocation = packageManager.PackTestTemplatesNuGetPackage();

            new DotnetCommand(
                _log,
                "Microsoft.TemplateSearch.TemplateDiscovery.dll",
                "--basePath",
                testDir,
                "--packagesPath",
                Path.GetDirectoryName(packageLocation),
                "-v")
                .Execute()
                .Should()
                .ExitWith(0);

            string cacheFilePath = Path.Combine(testDir, "SearchCache", "NuGetTemplateSearchInfo.json");
            Assert.True(File.Exists(cacheFilePath));

            var settingsPath = TestUtils.CreateTemporaryFolder();

            new DotnetNew3Command(_log)
                  .WithCustomHive(settingsPath)
                  .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                  .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And.NotHaveStdErr();

            new DotnetNew3Command(_log, "CliHostFile", "--search")
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

            new DotnetNew3Command(_log, "--search", "--param")
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

            new DotnetNew3Command(_log, "--search", "-p")
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

            new DotnetNew3Command(_log, "--search", "--test-param")
                .WithCustomHive(settingsPath)
                .WithEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", cacheFilePath)
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
                .Execute()
                .Should().Fail();
        }
    }
}
