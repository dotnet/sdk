// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader.Tests
{
    [TestClass]
    public class NuGetPackageDownloaderTests : SdkTest
    {
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
