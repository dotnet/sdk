// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using Xunit;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Tests;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class NuGetPackageInstallerTests : SdkTest
    {
        [Fact]
        public async void GivenNoFeedInstallFailsWithException()
        {
            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(TestPackageId, new NuGetVersion(TestPackageVersion)));
        }

        [Fact]
        public async void GivenASourceInstallSucceeds()
        {
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(additionalFeeds: new[] {GetTestLocalFeedPath()}));
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(_tempDirectory.Value, "Package should be downloaded to the input folder");
        }

        [Fact]
        public async void GivenAFailedSourceItShouldError()
        {
            var nonExistFeed = new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(
                    TestPackageId,
                    new NuGetVersion(TestPackageVersion),
                    new PackageSourceLocation(additionalFeeds: new[] {nonExistFeed.Value})));
        }

        [Fact]
        public void GivenNugetConfigInstallSucceeds()
        {
        }

        [Fact]
        public void GivenAValidNugetConfigAndFailedSourceItShouldError()
        {
        }

        [Fact]
        public void GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir()
        {
        }

        [Fact]
        public void GivenAllButNoPackageVersionItCanInstallThePackage()
        {
        }

        [Fact]
        public void GivenARelativeSourcePathInstallSucceeds()
        {
        }

        [Fact]
        public void GivenAEmptySourceAndNugetConfigInstallSucceeds()
        {
        }

        [UnixOnlyFact]
        public void GivenAPackageWithCasingAndenUSPOSIXInstallSucceeds()
        {
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
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");

        private DirectoryPath _tempDirectory;
        private NuGetTestLogger _logger;
        private NuGetPackageDownloader _installer;

        public NuGetPackageInstallerTests(ITestOutputHelper log) : base(log)
        {
            _tempDirectory = GetUniqueTempProjectPathEachTest();
            _logger = new NuGetTestLogger();
            _installer = new NuGetPackageDownloader(_tempDirectory, logger: _logger);
        }
    }
}
