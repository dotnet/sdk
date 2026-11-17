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
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader.Tests
{
    [TestClass]
    public class NuGetPackageDownloaderTests : SdkTest
    {
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
