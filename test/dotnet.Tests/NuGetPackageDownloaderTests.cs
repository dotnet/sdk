// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader.Tests
{
    [TestClass]
    public class NuGetPackageDownloaderTests : SdkTest
    {
        [TestMethod]
        [DataRow("")]
        [DataRow("Con")]
        public async Task IdCompletionMapsReturnedIdsRatherThanTheStem(string stem)
        {
            string directory = WriteCompletionConfig();
            List<string> queriedSources = [];
            NuGetPackageDownloader downloader = new(new DirectoryPath(), currentWorkingDirectory: directory,
                getAutocompleteResource: (source, _) =>
                {
                    queriedSources.Add(source.Name);
                    return Task.FromResult<AutoCompleteResource>(new CompletionResource(
                        source.Name == "public"
                            ? ["Other.Tool", "Contoso.Tools.Example", "Contoso.Exact", "Disabled.Tool", "Unmapped.Tool"]
                            : source.Name == "corp"
                                ? ["contoso.Tools.Example", "CONTOSO.TOOLS.EXAMPLE", "Contoso.Exact"]
                                : ["Contoso.Exact"]));
                });

            var ids = await downloader.GetPackageIdsAsync(stem, allowPrerelease: false, cancellationToken: TestContext.CancellationToken);

            ids.Should().BeEquivalentTo(stem.Length == 0
                ? ["Other.Tool", "contoso.Tools.Example", "Contoso.Exact"]
                : new[] { "contoso.Tools.Example", "Contoso.Exact" });
            queriedSources.Should().BeEquivalentTo("public", "corp", "exact");
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, true, false)]
        [DataRow(true, false, true)]
        public async Task IdCompletionPreservesUnmappedAndOverrideBehavior(bool configuredMapping, bool optOut, bool explicitSource)
        {
            string directory = WriteCompletionConfig(configuredMapping);
            NuGetPackageDownloader downloader = new(new DirectoryPath(), currentWorkingDirectory: directory,
                shouldUsePackageSourceMapping: !optOut,
                getAutocompleteResource: (_, _) => Task.FromResult<AutoCompleteResource>(
                    new CompletionResource(["Contoso.Exact", "Other.Tool"])));
            PackageSourceLocation? location = explicitSource
                ? new(sourceFeedOverrides: ["https://public.example.test/v3/index.json"])
                : null;

            var ids = await downloader.GetPackageIdsAsync("", allowPrerelease: true, location, TestContext.CancellationToken);

            ids.Should().BeEquivalentTo("Contoso.Exact", "Other.Tool");
        }

        [TestMethod]
        [DataRow("Contoso.Exact", "exact")]
        [DataRow("CONTOSO.Tools.Example", "corp")]
        [DataRow("Disabled.Tool", null)]
        [DataRow("Unmapped.Tool", null)]
        public async Task VersionCompletionOnlyQueriesFullIdMappedSources(string id, string? expectedSource)
        {
            string directory = WriteCompletionConfig();
            List<string> queriedSources = [];
            NuGetPackageDownloader downloader = new(new DirectoryPath(), currentWorkingDirectory: directory,
                getAutocompleteResource: (source, _) =>
                {
                    queriedSources.Add(source.Name);
                    return Task.FromResult<AutoCompleteResource>(new CompletionResource([]));
                });

            var versions = await downloader.GetPackageVersionsAsync(new ToolPackage.PackageId(id), "1.0", allowPrerelease: true,
                cancellationToken: TestContext.CancellationToken);

            queriedSources.Should().Equal(expectedSource is null ? [] : new[] { expectedSource });
            versions.Select(version => version.ToNormalizedString()).Should().Equal(expectedSource is null ? [] : ["1.0.0-preview.1"]);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task CompletionKeepsHealthyResultsWhenAResourceIsUnsupportedOrFails(bool versions, bool discoveryFailure)
        {
            string directory = WriteCompletionConfig(mappingEnabled: false);
            NuGetPackageDownloader downloader = new(new DirectoryPath(), currentWorkingDirectory: directory,
                getAutocompleteResource: (source, _) => source.Name switch
                {
                    "public" => Task.FromResult<AutoCompleteResource>(new CompletionResource(["Other.Tool"])),
                    "corp" when discoveryFailure => Task.FromException<AutoCompleteResource>(new InvalidOperationException("unavailable")),
                    "corp" => Task.FromResult<AutoCompleteResource>(new CompletionResource([], fail: true)),
                    _ => Task.FromResult<AutoCompleteResource>(null!),
                });

            if (versions)
            {
                (await downloader.GetPackageVersionsAsync(new ToolPackage.PackageId("Other.Tool"), "1.0", allowPrerelease: true,
                    cancellationToken: TestContext.CancellationToken))
                    .Select(version => version.ToNormalizedString()).Should().Equal("1.0.0-preview.1");
            }
            else
            {
                (await downloader.GetPackageIdsAsync("", allowPrerelease: false, cancellationToken: TestContext.CancellationToken))
                    .Should().Equal("Other.Tool");
            }
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, true, false)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public async Task CompletionValidatesHttpSourcesIndependently(bool versions, bool mappingEnabled, bool allowInsecureConnections)
        {
            string directory = WriteCompletionConfig(mappingEnabled);
            string configPath = Path.Combine(directory, "NuGet.Config");
            XDocument config = XDocument.Load(configPath);
            config.Root!.Element("packageSources")!.Add(new XElement("add",
                new XAttribute("key", "http"),
                new XAttribute("value", "http://http.example.test/v3/index.json"),
                new XAttribute("allowInsecureConnections", allowInsecureConnections)));
            if (mappingEnabled)
            {
                config.Root.Element("packageSourceMapping")!.Add(new XElement("packageSource",
                    new XAttribute("key", "http"), new XElement("package", new XAttribute("pattern", "*"))));
            }
            config.Save(configPath);

            List<string> queriedSources = [];
            NuGetPackageDownloader downloader = new(new DirectoryPath(), currentWorkingDirectory: directory,
                getAutocompleteResource: (source, _) =>
                {
                    queriedSources.Add(source.Name);
                    return Task.FromResult<AutoCompleteResource>(source.Name is "public" or "http"
                        ? new CompletionResource(["Other.Tool"])
                        : null!);
                });

            if (versions)
            {
                (await downloader.GetPackageVersionsAsync(new ToolPackage.PackageId("Other.Tool"), "1.0", allowPrerelease: true,
                    cancellationToken: TestContext.CancellationToken))
                    .Select(version => version.ToNormalizedString()).Should().Equal("1.0.0-preview.1");
            }
            else
            {
                (await downloader.GetPackageIdsAsync("Other", allowPrerelease: false, cancellationToken: TestContext.CancellationToken))
                    .Should().Equal("Other.Tool");
            }

            queriedSources.Should().Contain("public");
            queriedSources.Contains("http").Should().Be(allowInsecureConnections);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CompletionTimeoutIncludesResourceDiscovery(bool versions)
        {
            string directory = WriteCompletionConfig(mappingEnabled: false);
            bool cancelled = false;
            NuGetPackageDownloader downloader = new(new DirectoryPath(), currentWorkingDirectory: directory,
                getAutocompleteResource: async (source, token) =>
                {
                    if (source.Name != "corp")
                    {
                        return null!;
                    }

                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                        throw;
                    }
                    return null!;
                });

            Task query = versions
                ? downloader.GetPackageVersionsAsync(new ToolPackage.PackageId("Other.Tool"), cancellationToken: TestContext.CancellationToken)
                : downloader.GetPackageIdsAsync("", allowPrerelease: false, cancellationToken: TestContext.CancellationToken);
            await query.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

            cancelled.Should().BeTrue();
        }

        private string WriteCompletionConfig(bool mappingEnabled = true)
        {
            string directory = TestAssetsManager.CreateTestDirectory().Path;
            new XDocument(
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("clear"),
                        new[] { "public", "corp", "exact", "disabled", "no-rules" }.Select(name =>
                            new XElement("add", new XAttribute("key", name), new XAttribute("value", $"https://{name}.example.test/v3/index.json")))),
                    new XElement("disabledPackageSources",
                        new XElement("clear"),
                        new XElement("add", new XAttribute("key", "disabled"), new XAttribute("value", "true"))),
                    new XElement("packageSourceMapping",
                        new XElement("clear"),
                        mappingEnabled
                            ? new Dictionary<string, string>
                            {
                                ["PUBLIC"] = "*",
                                ["corp"] = "Contoso.*",
                                ["exact"] = "Contoso.Exact",
                                ["disabled"] = "Disabled.*",
                                ["absent"] = "Unmapped.*",
                            }.Select(pair => new XElement("packageSource", new XAttribute("key", pair.Key),
                                new XElement("package", new XAttribute("pattern", pair.Value))))
                            : [])))
                .Save(Path.Combine(directory, "NuGet.Config"));
            return directory;
        }

        private sealed class CompletionResource(string[] ids, bool fail = false) : AutoCompleteResource
        {
            public override Task<IEnumerable<string>> IdStartsWith(string packageIdPrefix, bool includePrerelease, ILogger log, CancellationToken token)
                => fail
                    ? Task.FromException<IEnumerable<string>>(new InvalidOperationException("unavailable"))
                    : Task.FromResult(ids.Where(id => id.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase)));

            public override Task<IEnumerable<NuGetVersion>> VersionStartsWith(
                string packageId, string versionPrefix, bool includePrerelease, SourceCacheContext sourceCacheContext, ILogger log, CancellationToken token)
            {
                versionPrefix.Should().Be("1.0");
                includePrerelease.Should().BeTrue();
                return fail
                    ? Task.FromException<IEnumerable<NuGetVersion>>(new InvalidOperationException("unavailable"))
                    : Task.FromResult<IEnumerable<NuGetVersion>>([NuGetVersion.Parse("1.0.0-preview.1")]);
            }
        }

        [TestMethod]
        [DataRow("package add --project", false, false)]
        [DataRow("add", false, false)]
        [DataRow("package add --file", false, false)]
        [DataRow("package add --project", true, false)]
        [DataRow("add", true, false)]
        [DataRow("package add --file", true, false)]
        [DataRow("package add --project", false, true)]
        [DataRow("package add --project", true, true)]
        [DataRow("add", true, true)]
        public void PackageCompletionUsesSelectedTargetAndInvocationRelativeSource(string commandForm, bool explicitSource, bool midLine)
        {
            string root = TestAssetsManager.CreateTestDirectory().Path;
            string invocation = Path.Combine(root, "invocation");
            string target = Path.Combine(root, "target");
            string targetFeed = Path.Combine(root, "target-feed");
            string overrideFeed = Path.Combine(invocation, "override");
            foreach (string directory in new[] { invocation, target, targetFeed, overrideFeed })
            {
                Directory.CreateDirectory(directory);
            }
            string targetFile = Path.Combine(target, commandForm.EndsWith("--file", StringComparison.Ordinal) ? "app.cs" : "app.csproj");
            File.WriteAllText(targetFile, "");
            CreatePackage(targetFeed, "Contoso.Tools.Example", "1.0.0");
            CreatePackage(overrideFeed, "Override.Only", "2.0.0");
            new XDocument(
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add", new XAttribute("key", "parent"), new XAttribute("value", overrideFeed))),
                    new XElement("packageSourceMapping",
                        new XElement("clear"),
                        new XElement("packageSource", new XAttribute("key", "parent"),
                            new XElement("package", new XAttribute("pattern", "Contoso.Tools.Example"))))))
                .Save(Path.Combine(root, "NuGet.Config"));
            File.WriteAllText(Path.Combine(invocation, "NuGet.Config"), """
                <configuration>
                  <packageSources><clear /></packageSources>
                  <packageSourceMapping><clear /></packageSourceMapping>
                </configuration>
                """);
            new XDocument(
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add", new XAttribute("key", "target"), new XAttribute("value", targetFeed)),
                        new XElement("add", new XAttribute("key", "unmapped"), new XAttribute("value", "http://127.0.0.1:9/v3/index.json"))),
                    new XElement("packageSourceMapping",
                        new XElement("clear"),
                        new XElement("packageSource", new XAttribute("key", "target"),
                            new XElement("package", new XAttribute("pattern", "Contoso.Tools.*"))))))
                .Save(Path.Combine(target, "NuGet.Config"));

            string package = explicitSource ? "Override.Only" : "Contoso.Tools.Example";
            string version = explicitSource ? "2.0.0" : "1.0.0";
            string prefix = explicitSource ? "Over" : "Con";
            string relativeTarget = Path.GetRelativePath(invocation, targetFile);
            string commandPrefix = midLine && commandForm != "add"
                ? "dotnet package add"
                : $"dotnet {commandForm} \"{relativeTarget}\"";
            string commandSuffix = midLine && commandForm != "add"
                ? $" {commandForm["package add".Length..]} \"{relativeTarget}\""
                : "";
            if (commandForm == "add")
            {
                commandPrefix += " package";
            }
            if (explicitSource)
            {
                if (midLine)
                {
                    commandSuffix += " --source override";
                }
                else
                {
                    commandPrefix += " --source override";
                }
            }

            foreach (var (arguments, expected) in new[] { (prefix, package), ($"{package} --version {version[..2]}", version) })
            {
                string input = $"{commandPrefix} {arguments}";
                new DotnetCommand(Log, "complete", input + commandSuffix, "--position", input.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .WithWorkingDirectory(invocation)
                    .WithEnvironmentVariable("DOTNET_NOLOGO", "1")
                    .WithEnvironmentVariable("DOTNET_GENERATE_ASPNET_CERTIFICATE", "0")
                    .Execute()
                    .Should().Pass().And.HaveStdOutContaining(expected).And.NotHaveStdErr();
            }
        }

        [TestMethod]
        [DataRow("package add --project", true)]
        [DataRow("add", true)]
        [DataRow("package add --file", true)]
        [DataRow("package add --project", false)]
        [DataRow("add", false)]
        [DataRow("package add --file", false)]
        public void PackageCompletionUsesExistingDirectoriesAndIgnoresMissingTargets(string commandForm, bool existingDirectory)
        {
            string root = TestAssetsManager.CreateTestDirectory().Path;
            string feed = Path.Combine(root, "feed");
            Directory.CreateDirectory(feed);
            CreatePackage(feed, "Contoso.Tools.Example", "1.0.0");
            File.WriteAllText(Path.Combine(root, "one.csproj"), "");
            File.WriteAllText(Path.Combine(root, "two.csproj"), "");
            new XDocument(
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed))),
                    new XElement("packageSourceMapping", new XElement("clear"))))
                .Save(Path.Combine(root, "NuGet.Config"));

            string target = existingDirectory ? root : Path.Combine(root, "missing.csproj");
            string command = $"dotnet {commandForm} \"{target}\"";
            if (commandForm == "add")
            {
                command += " package";
            }

            foreach (var (suffix, expected) in new[] { ("Con", "Contoso.Tools.Example"), ("Contoso.Tools.Example --version 1.", "1.0.0") })
            {
                var result = new DotnetCommand(Log, "complete", $"{command} {suffix}")
                    .WithWorkingDirectory(root)
                    .WithEnvironmentVariable("DOTNET_NOLOGO", "1")
                    .WithEnvironmentVariable("DOTNET_GENERATE_ASPNET_CERTIFICATE", "0")
                    .Execute();
                result.Should().Pass().And.NotHaveStdErr();
                if (existingDirectory)
                {
                    result.Should().HaveStdOutContaining(expected);
                }
                else
                {
                    result.Should().HaveStdOut("");
                }
            }
        }

        [TestMethod]
        public async Task BackgroundWorkloadUpdaterUsesTheCapturedInvocationDirectory()
        {
            TestDirectory testDirectory = TestAssetsManager.CreateTestDirectory();
            string invocationDirectory = Path.Combine(testDirectory.Path, "repository");
            string feedDirectory = Path.Combine(testDirectory.Path, "feed");
            Directory.CreateDirectory(invocationDirectory);
            Directory.CreateDirectory(feedDirectory);
            CreatePackage(feedDirectory, "Test.Package", "1.0.0");

            string workloadVersion = new SdkFeatureBand(Product.Version) + ".1";
            string globalJsonPath = Path.Combine(invocationDirectory, "global.json");
            File.WriteAllText(globalJsonPath, JsonSerializer.Serialize(new { sdk = new { workloadVersion } }));
            new XDocument(
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add", new XAttribute("key", "repo-only"), new XAttribute("value", feedDirectory))),
                    new XElement("packageSourceMapping",
                        new XElement("clear"),
                        new XElement("packageSource", new XAttribute("key", "repo-only"),
                            new XElement("package", new XAttribute("pattern", "Test.Package"))))))
                .Save(Path.Combine(invocationDirectory, "NuGet.Config"));

            const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
            var updater = (WorkloadAdvertisingManifestUpdater)typeof(WorkloadManifestUpdater)
                .GetMethod("GetAdvertisingUpdaterInstance", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [Path.Combine(testDirectory.Path, "home"), invocationDirectory])!;
            var resolver = (IWorkloadResolver)typeof(WorkloadAdvertisingManifestUpdater)
                .GetField("_workloadResolver", InstanceFields)!.GetValue(updater)!;
            resolver.GetWorkloadManifestProvider().GetWorkloadVersion().GlobalJsonPath.Should().Be(globalJsonPath);

            var downloader = (NuGetPackageDownloader)typeof(WorkloadAdvertisingManifestUpdater)
                .GetField("_nugetPackageDownloader", InstanceFields)!.GetValue(updater)!;
            var sources = (IEnumerable<PackageSource>)typeof(NuGetPackageDownloader)
                .GetMethod("LoadDefaultSources", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(downloader, [new ToolPackage.PackageId("Test.Package"), null, null])!;
            sources.Should().ContainSingle().Which.Source.Should().Be(feedDirectory);

            var version = await downloader.GetLatestPackageVersion(new ToolPackage.PackageId("Test.Package"));
            version.ToNormalizedString().Should().Be("1.0.0");
        }

        [TestMethod]
        public async Task GetLatestPackageVersionsReturnsAllPreviewVersionsWhenCountIsZero()
        {
            TestDirectory testDirectory = TestAssetsManager.CreateTestDirectory();
            string feedDirectory = Path.Combine(testDirectory.Path, "feed");
            Directory.CreateDirectory(feedDirectory);
            CreatePackage(feedDirectory, "Test.Package", "1.0.0-preview.1");
            CreatePackage(feedDirectory, "Test.Package", "1.0.0-preview.2");

            NuGetPackageDownloader downloader = new(
                new DirectoryPath(Path.Combine(testDirectory.Path, "packages")),
                currentWorkingDirectory: testDirectory.Path);
            PackageSourceLocation sourceLocation = new(sourceFeedOverrides: [feedDirectory]);

            var versions = await downloader.GetLatestPackageVersions(
                new ToolPackage.PackageId("Test.Package"),
                numberOfResults: 0,
                sourceLocation,
                includePreview: true);

            versions.Select(version => version.ToNormalizedString())
                .Should().Equal("1.0.0-preview.2", "1.0.0-preview.1");
        }

        private static void CreatePackage(string feedDirectory, string packageId, string version)
        {
            string packagePath = Path.Combine(feedDirectory, $"{packageId}.{version}.nupkg");
            using ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
            ZipArchiveEntry nuspec = archive.CreateEntry($"{packageId}.nuspec");
            using StreamWriter writer = new(nuspec.Open());
            writer.Write($"""
                <?xml version="1.0" encoding="utf-8"?>
                <package>
                  <metadata>
                    <id>{packageId}</id>
                    <version>{version}</version>
                    <authors>Test</authors>
                    <description>Test package</description>
                  </metadata>
                </package>
                """);
        }
    }
}
