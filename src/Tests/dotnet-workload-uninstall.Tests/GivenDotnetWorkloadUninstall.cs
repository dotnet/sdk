// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Workload.Uninstall.Tests
{
    public class GivenDotnetWorkloadUninstall : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        public GivenDotnetWorkloadUninstall(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockWorkloadsSample.json");
        }

        [Fact]
        public void GivenWorkloadUninstallItErrorsWhenWorkloadIsNotInstalled()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var exceptionThrown = Assert.Throws<GracefulException>(() => UninstallWorkload("mock-1", testDirectory, "6.0.100"));
            exceptionThrown.Message.Should().Contain("mock-1");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadUninstallItCanUninstallWorkload(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var sdkFeatureVersion = "6.0.100";
            var installingWorkload = "mock-1";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            InstallWorkload(installingWorkload, testDirectory, sdkFeatureVersion);

            // Assert install was successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);

            UninstallWorkload(installingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful
            installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(0);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installingWorkload))
                .Should().BeFalse();
            packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadUninstallItCanUninstallOnlySpecifiedWorkload(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var sdkFeatureVersion = "6.0.100";
            var installedWorkload = "mock-1";
            var uninstallingWorkload = "mock-2";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
            }

            InstallWorkload(installedWorkload, testDirectory, sdkFeatureVersion);
            InstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert installs were successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(3);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installedWorkload))
                .Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(4);

            UninstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful, other workload is still installed
            installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", installedWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenWorkloadUninstallItCanUninstallOnlySpecifiedFeatureBand(bool userLocal)
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default").Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var prevSdkFeatureVersion = "5.0.100";
            var sdkFeatureVersion = "6.0.100";
            var uninstallingWorkload = "mock-1";

            string installRoot = userLocal ? userProfileDir : dotnetRoot;
            if (userLocal)
            {
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, sdkFeatureVersion);
                WorkloadFileBasedInstall.SetUserLocal(dotnetRoot, prevSdkFeatureVersion);
            }

            InstallWorkload(uninstallingWorkload, testDirectory, prevSdkFeatureVersion);
            InstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert installs were successful
            var installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", prevSdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            var packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
            var featureBandMarkerFiles = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"))
                .SelectMany(packIdDirs => Directory.GetDirectories(packIdDirs))
                .SelectMany(packVersionDirs => Directory.GetFiles(packVersionDirs));
            featureBandMarkerFiles.Count().Should().Be(6); // 3 packs x 2 feature bands

            UninstallWorkload(uninstallingWorkload, testDirectory, sdkFeatureVersion);

            // Assert uninstall was successful, other workload is still installed
            installPacks = Directory.GetDirectories(Path.Combine(installRoot, "packs"));
            installPacks.Count().Should().Be(2);
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", sdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "metadata", "workloads", prevSdkFeatureVersion, "InstalledWorkloads", uninstallingWorkload))
                .Should().BeTrue();
            packRecordDirs = Directory.GetDirectories(Path.Combine(installRoot, "metadata", "workloads", "InstalledPacks", "v1"));
            packRecordDirs.Count().Should().Be(3);
        }

        private void InstallWorkload(string installingWorkload, string testDirectory, string sdkFeatureVersion)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            bool userLocal = WorkloadFileBasedInstall.IsUserLocal(dotnetRoot, sdkFeatureVersion);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var installParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", installingWorkload });
            var installCommand = new WorkloadInstallCommand(installParseResult, reporter: _reporter, workloadResolver: workloadResolver, nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater, userProfileDir: userProfileDir, version: sdkFeatureVersion, dotnetDir: dotnetRoot, tempDirPath: testDirectory);
            installCommand.Execute();
        }

        private void UninstallWorkload(string uninstallingWorkload, string testDirectory, string sdkFeatureVersion)
        {
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            bool userLocal = WorkloadFileBasedInstall.IsUserLocal(dotnetRoot, sdkFeatureVersion);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot, userLocal, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var uninstallParseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "uninstall", uninstallingWorkload });
            var uninstallCommand = new WorkloadUninstallCommand(uninstallParseResult, reporter: _reporter, workloadResolver, nugetDownloader,
                dotnetDir: dotnetRoot, version: sdkFeatureVersion, userProfileDir: userProfileDir);
            uninstallCommand.Execute();
        }
    }
}
