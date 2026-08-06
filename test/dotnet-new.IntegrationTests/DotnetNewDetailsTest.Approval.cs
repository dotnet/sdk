// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public partial class DotnetNewDetailsTest : BaseIntegrationTest
    {
        private const string _nuGetPackageId = "Microsoft.Android.Templates";

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/templating/issues/6811")]
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

        [TestMethod]
        public Task CanDisplayDetails_RemotePackage_NuGetFeedNoVersion()
        {
            var folder = CreateTemporaryFolder();

            // Write a NuGet.Config that clears all globally-configured sources so that
            // the first call (which omits --nuget-source) cannot fall back to any feed
            // that already carries the package (e.g. a CI-wide dotnet11 feed).
            // This makes the "must fail" assertion deterministic across environments.
            File.WriteAllText(Path.Combine(folder, "NuGet.Config"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
");

            new DotnetNewCommand(_log, "details", _nuGetPackageId)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(folder)
                .Execute()
                .Should()
                .Fail();

            var commandResult = new DotnetNewCommand(_log, "details", _nuGetPackageId, "--nuget-source", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(folder)
                .Execute();

            commandResult.Should().Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex(@"^   Package version:.*$", "   Package version: %VERSION%", RegexOptions.Multiline);
                    output.ScrubByRegex(@"(microsoft\.android\.templates/)[^/]+/", "$1%VERSION%/");
                    output.ScrubByRegex(@"(microsoft\.android\.templates\.)[^/]+(\.nupkg)", "$1%VERSION%$2");
                    output.ScrubByRegex(@"(https://pkgs\.dev\.azure\.com/)(?:dnceng/)?(9ee6d478-d288-47f7-aacc-f6e6d082ae6d/)", "$1$2");
                    // Template list varies between package versions on the public feed;
                    // omit it because the remote details response may not include it.
                    int idx = output.ToString().IndexOf("   Templates:");
                    if (idx >= 0)
                    {
                        output.Remove(idx, output.Length - idx);
                    }
                });
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [TestMethod]
        [Ignore("https://github.com/dotnet/templating/issues/6811")]
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

        [TestMethod]
        public async Task CanDisplayDetails_RemotePackage_OtherFeedNoVersion()
        {
            string packageName = "Microsoft.Azure.WebJobs.ItemTemplates";
            string latestVersion = await GetLatestVersion(packageName);

            CommandResult commandResult = new DotnetNewCommand(_log, "details", packageName, "--nuget-source", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json")
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

        [TestMethod]
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

        [TestMethod]
        public Task CanDisplayDetails_InstalledPackage_NuGetFeed()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", _nuGetPackageId, "--nuget-source", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json")
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

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex(@"^   Package version:.*$", "   Package version: %VERSION%", RegexOptions.Multiline);
                    output.ScrubByRegex(@"(microsoft\.android\.templates/)[^/]+/", "$1%VERSION%/");
                    output.ScrubByRegex(@"(microsoft\.android\.templates\.)[^/]+(\.nupkg)", "$1%VERSION%$2");
                    int idx = output.ToString().IndexOf("   Templates:");
                    if (idx >= 0)
                    {
                        int lineEnd = output.ToString().IndexOf('\n', idx);
                        if (lineEnd >= 0)
                        {
                            output.Remove(lineEnd + 1, output.Length - lineEnd - 1);
                            output.Append("      %TEMPLATES%");
                        }
                    }
                });
        }

        [TestMethod]
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

            CommandResult commandResult = new DotnetNewCommand(_log, "details", packageName, "--nuget-source", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json")
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

        [TestMethod]
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
            using HttpClient client = new();
            // Resolve the SearchQueryService endpoint from the V3 service index
            // This matches the resolution path that `dotnet new details` uses internally
            string indexJson = await client.GetStringAsync("https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json");
            JObject index = JObject.Parse(indexJson);
            JToken? searchResource = index["resources"]?
                .FirstOrDefault(r => HasResourceType(r["@type"], "SearchQueryService"));
            string? searchBaseUrl = searchResource?["@id"]?.ToString().TrimEnd('/');
            if (string.IsNullOrEmpty(searchBaseUrl))
            {
                throw new InvalidOperationException("SearchQueryService resource was not found in the NuGet service index.");
            }

            string json = await client.GetStringAsync(
                $"{searchBaseUrl}?q={Uri.EscapeDataString(packageName)}&skip=0&take=1&prerelease=true&semVerLevel=2.0.0");
            JObject obj = JObject.Parse(json);

            var data = obj["data"] as JArray;
            if (data == null || data.Count == 0)
            {
                throw new Exception($"Package '{packageName}' not found in search results.");
            }

            string? version = data[0]?["version"]?.ToString();
            if (string.IsNullOrEmpty(version))
            {
                throw new Exception($"No version found for package '{packageName}'.");
            }

            return version;
        }

        private static bool HasResourceType(JToken? resourceType, string typePrefix) =>
            resourceType switch
            {
                JValue { Type: JTokenType.String } => resourceType.ToString().StartsWith(typePrefix, StringComparison.Ordinal),
                JArray array => array.Values<string>().Any(type => type?.StartsWith(typePrefix, StringComparison.Ordinal) == true),
                _ => false,
            };

        private string ExtractVersion(string? stdOut)
        {
            var match = Regex.Match(stdOut ?? string.Empty, @"Package version:\s*(\S+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            throw new Exception("Version not found in the output.");
        }
    }
}
