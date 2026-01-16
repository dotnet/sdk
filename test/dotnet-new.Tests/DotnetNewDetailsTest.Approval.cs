// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewDetailsTest : BaseIntegrationTest
    {
        private const string _nuGetPackageId = "Uno.ProjectTemplates.Dotnet";

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/6811")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanDisplayDetails_RemotePackage_NuGetFeedWithVersion()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "details", _nuGetPackageId, "--version", "4.8.0-dev.604")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanDisplayDetails_RemotePackage_NuGetFeedNoVersion()
        {
            var folder = CreateTemporaryFolder();

            var createCommandResult = () => new DotnetNewCommand(_log, "details", _nuGetPackageId)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(folder)
                .Execute();

            createCommandResult().Should().Fail();

            File.WriteAllText(Path.Combine(folder, "NuGet.Config"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""NuGet.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>
");

            var commandResult = createCommandResult();

            commandResult.Should().Pass();

            return Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/6811")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanDisplayDetails_RemotePackage_OtherFeedWithVersion()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "details", "Microsoft.Azure.WebJobs.ItemTemplates", "--version", "4.0.2288")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public async Task CanDisplayDetails_RemotePackage_OtherFeedNoVersion()
        {
            string packageName = "Microsoft.Azure.WebJobs.ItemTemplates";
            string latestVersion = await GetLatestVersion(packageName);

            CommandResult commandResult = new DotnetNewCommand(_log, "details", packageName)
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            ExtractVersion(commandResult.StdOut)
                .Should()
                .Be(latestVersion);
        }

        [Fact]
        public Task CanDisplayDetails_InstalledPackage_LocalPackage()
        {
            string packageLocation = PackTestNuGetPackage(_log);
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", packageLocation)
                .WithoutBuiltInTemplates()
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, "details", "Microsoft.TemplateEngine.TestTemplates")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform();
        }

        [Fact]
        public Task CanDisplayDetails_InstalledPackage_NuGetFeed()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", _nuGetPackageId, "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithoutBuiltInTemplates().WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, "details", _nuGetPackageId)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public async Task CanDisplayDetails_InstalledPackage_OtherFeed()
        {
            string packageName = "Microsoft.Azure.WebJobs.ItemTemplates";
            string latestVersion = await GetLatestVersion(packageName);

            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", packageName)
                .WithoutBuiltInTemplates().WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, "details", packageName)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            ExtractVersion(commandResult.StdOut)
                .Should()
                .Be(latestVersion);
        }

        [Fact]
        public Task CanDisplayDetails_InstalledPackage_FolderInstallation()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0);

            CommandResult commandResult = new DotnetNewCommand(_log, "details", basicFSharp)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(basicFSharp, "%TEMPLATE FOLDER%"));
        }

        private async Task<string> GetLatestVersion(string packageName)
        {
            using (HttpClient client = new HttpClient())
            {
                string json = await client.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{packageName.ToLowerInvariant()}/index.json");
                JObject obj = JObject.Parse(json);

                var versions = obj["versions"]?.ToObject<List<string>>();
                if (versions == null || versions.Count == 0)
                {
                    throw new Exception("No versions found.");
                }

                return versions.Last();
            }
        }

        private string ExtractVersion(string stdOut)
        {
            var match = Regex.Match(stdOut, @"Package version:\s*(\S+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            throw new Exception("Version not found in the output.");
        }
    }
}
