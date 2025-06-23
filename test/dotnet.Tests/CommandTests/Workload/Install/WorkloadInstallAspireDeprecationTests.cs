// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using ManifestReaderTests;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    public class WorkloadInstallAspireDeprecationTests : SdkTest
    {
        private readonly BufferedReporter _reporter;

        public WorkloadInstallAspireDeprecationTests(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenOnlyAspireWorkloadItShowsDeprecationMessage()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");

            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var workloadInstaller = new MockPackWorkloadInstaller();

            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "aspire" });

            var command = new WorkloadInstallCommand(
                parseResult,
                reporter: _reporter,
                workloadResolverFactory: workloadResolverFactory,
                workloadInstaller: workloadInstaller,
                nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater,
                tempDirPath: testDirectory);

            var exitCode = command.Execute();

            // Should exit successfully but show deprecation message
            exitCode.Should().Be(0);
            _reporter.Lines.Should().Contain(line => line.Contains("Aspire workload is deprecated"));
            _reporter.Lines.Should().Contain(line => line.Contains("https://aka.ms/aspire/support-policy"));

            // Should not have installed any workloads
            workloadInstaller.InstallationRecordRepository.InstalledWorkloads.Should().BeEmpty();
        }

        [Fact]
        public void GivenAspireWithOtherWorkloadsItShowsDeprecationAndInstallsOthers()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");

            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var workloadInstaller = new MockPackWorkloadInstaller();

            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "aspire", "xamarin-android" });

            var command = new WorkloadInstallCommand(
                parseResult,
                reporter: _reporter,
                workloadResolverFactory: workloadResolverFactory,
                workloadInstaller: workloadInstaller,
                nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater,
                tempDirPath: testDirectory);

            var exitCode = command.Execute();

            // Should exit successfully, show deprecation message, but install android workload
            exitCode.Should().Be(0);
            _reporter.Lines.Should().Contain(line => line.Contains("Aspire workload is deprecated"));
            _reporter.Lines.Should().Contain(line => line.Contains("https://aka.ms/aspire/support-policy"));

            // Should have installed android but not aspire
            workloadInstaller.InstallationRecordRepository.InstalledWorkloads.Should().Contain(new WorkloadId("xamarin-android"));
            workloadInstaller.InstallationRecordRepository.InstalledWorkloads.Should().NotContain(new WorkloadId("aspire"));
        }

        [Fact]
        public void GivenAspireWorkloadDeprecationMessageIsShownOnlyOnce()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var dotnetRoot = Path.Combine(testDirectory, "dotnet");
            var userProfileDir = Path.Combine(testDirectory, "user-profile");

            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), dotnetRoot);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver, userProfileDir);
            var nugetDownloader = new MockNuGetPackageDownloader(dotnetRoot);
            var manifestUpdater = new MockWorkloadManifestUpdater();
            var workloadInstaller = new MockPackWorkloadInstaller();

            var parseResult = Parser.Instance.Parse(new string[] { "dotnet", "workload", "install", "aspire", "xamarin-android" });

            var command = new WorkloadInstallCommand(
                parseResult,
                reporter: _reporter,
                workloadResolverFactory: workloadResolverFactory,
                workloadInstaller: workloadInstaller,
                nugetPackageDownloader: nugetDownloader,
                workloadManifestUpdater: manifestUpdater,
                tempDirPath: testDirectory);

            var exitCode = command.Execute();

            // Should exit successfully and show deprecation message only once
            exitCode.Should().Be(0);

            // Count occurrences of the deprecation message
            var deprecationLines = _reporter.Lines.Where(line => line.Contains("Aspire workload is deprecated")).ToList();
            deprecationLines.Should().HaveCount(1, "deprecation message should be shown exactly once");
        }

        private string _manifestPath => Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
    }
}
