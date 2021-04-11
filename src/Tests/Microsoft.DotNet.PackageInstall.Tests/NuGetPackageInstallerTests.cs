// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using Xunit;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class NuGetPackageInstallerTests : SdkTest
    {
        [Fact]
        public async Task GivenNoFeedInstallFailsWithException()
        {
            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(TestPackageId, new NuGetVersion(TestPackageVersion)));
        }

        [Fact]
        public async Task GivenASourceInstallSucceeds()
        {
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(overrideSourceFeeds: new[] {GetTestLocalFeedPath()}));
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(_tempDirectory.Value, "Package should be downloaded to the input folder");
        }

        [Fact]
        public async Task GivenAFailedSourceItShouldError()
        {
            var nonExistFeed = new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(
                    TestPackageId,
                    new NuGetVersion(TestPackageVersion),
                    new PackageSourceLocation(overrideSourceFeeds: new[] {nonExistFeed.Value})));
        }

        [Fact]
        public async Task GivenNugetConfigInstallSucceeds()
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var fileSystem = new FileSystemWrapper();
            WriteNugetConfigFileToPointToTheFeed(fileSystem, nugetConfigPath);

            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(nugetConfig: nugetConfigPath));
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task GivenAValidNugetConfigAndFailedSourceItShouldError()
        {
            var nonExistFeed = new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            var validNugetConfigPath = GenerateRandomNugetConfigFilePath();
            var fileSystem = new FileSystemWrapper();
            WriteNugetConfigFileToPointToTheFeed(fileSystem, validNugetConfigPath);

            // "source" option will override everything like nuget.config just like "dotner restore --source ..."
            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(
                    TestPackageId,
                    new NuGetVersion(TestPackageVersion),
                    new PackageSourceLocation(nugetConfig: validNugetConfigPath,
                        overrideSourceFeeds: new[] {nonExistFeed.Value})));
        }

        [Fact]
        public async Task GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir()
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var directoryBelowNugetConfig = nugetConfigPath.GetDirectoryPath().WithSubDirectories("subDir");
            Directory.CreateDirectory(directoryBelowNugetConfig.Value);

            var fileSystem = new FileSystemWrapper();
            WriteNugetConfigFileToPointToTheFeed(fileSystem, nugetConfigPath);

            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(rootConfigDirectory: directoryBelowNugetConfig));
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task GivenAllButNoPackageVersionItCanInstallThePackage()
        {
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                packageSourceLocation: new PackageSourceLocation(overrideSourceFeeds: new[] {GetTestLocalFeedPath()}));
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task GivenARelativeSourcePathInstallSucceeds()
        {
            string getTestLocalFeedPath = GetTestLocalFeedPath();
            string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, getTestLocalFeedPath);
            Log.WriteLine(relativePath);
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(overrideSourceFeeds: new[] {relativePath}));
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(_tempDirectory.Value, "Package should be downloaded to the input folder");
        }

        [Fact]
        public async Task WhenPassedIncludePreviewItInstallSucceeds()
        {
            string getTestLocalFeedPath = GetTestLocalFeedPath();
            string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, getTestLocalFeedPath);
            Log.WriteLine(relativePath);
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                packageSourceLocation: new PackageSourceLocation(overrideSourceFeeds: new[] {relativePath}),
                includePreview: true);
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(TestPackageId + "." + TestPreviewPackageVersion,
                "Package should download higher package version");
        }

        private static DirectoryPath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            return tempProjectDirectory;
        }

        private static void WriteNugetConfigFileToPointToTheFeed(IFileSystem fileSystem, FilePath? filePath)
        {
            if (!filePath.HasValue) return;

            fileSystem.Directory.CreateDirectory(filePath.Value.GetDirectoryPath().Value);

            fileSystem.File.WriteAllText(filePath.Value.Value, FormatNuGetConfig(
                localFeedPath: GetTestLocalFeedPath()));
        }

        public static string FormatNuGetConfig(string localFeedPath)
        {
            const string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""Test Source"" value=""{0}"" />
<add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
<add key=""myget-legacy"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json"" />
</packageSources>
</configuration>";
            return string.Format(template, localFeedPath);
        }

        private static FilePath GenerateRandomNugetConfigFilePath()
        {
            const string nugetConfigName = "nuget.config";
            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());

            FilePath nugetConfigFullPath =
                new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
            return nugetConfigFullPath;
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private const string TestPreviewPackageVersion = "2.0.1-preview1";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");

        private readonly DirectoryPath _tempDirectory;
        private NuGetTestLogger _logger;
        private readonly NuGetPackageDownloader _installer;

        public NuGetPackageInstallerTests(ITestOutputHelper log) : base(log)
        {
            _tempDirectory = GetUniqueTempProjectPathEachTest();
            _logger = new NuGetTestLogger();
            _installer = new NuGetPackageDownloader(_tempDirectory, logger: _logger);
        }
    }
}
