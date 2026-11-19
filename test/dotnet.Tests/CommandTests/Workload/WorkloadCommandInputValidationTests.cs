// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ManifestReaderTests;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.Uninstall;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Workload.Install.Tests;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Tests
{
    public class WorkloadCommandInputValidationTests
    {
        [Fact]
        public void WorkloadInstallGracefullyErrorsOnInvalidWorkloadName()
        {
            using var testDirectory = new DisposableTestDirectory();
            const string invalidWorkloadId = "../fake";
            string dotnetRoot = Path.Combine(testDirectory.Path, "dotnet");
            Directory.CreateDirectory(dotnetRoot);

            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(Array.Empty<string>()), dotnetRoot);
            var workloadResolverFactory = new MockWorkloadResolverFactory(dotnetRoot, "6.0.100", workloadResolver, testDirectory.Path);
            var command = new WorkloadInstallCommand(
                Parser.Parse(["dotnet", "workload", "install", invalidWorkloadId, "--skip-manifest-update"]),
                reporter: new BufferedReporter(),
                workloadResolverFactory,
                workloadInstaller: new MockPackWorkloadInstaller(dotnetRoot),
                nugetPackageDownloader: new MockNuGetPackageDownloader(dotnetRoot),
                workloadManifestUpdater: new MockWorkloadManifestUpdater(),
                tempDirPath: testDirectory.Path);

            var exceptionThrown = Assert.Throws<GracefulException>(() => command.Execute());
            exceptionThrown.Message.Should().Be(string.Format(CliCommandStrings.WorkloadNotRecognized, invalidWorkloadId));
        }

        [Fact]
        public void WorkloadUninstallGracefullyErrorsOnInvalidWorkloadName()
        {
            const string invalidWorkloadId = "../mock-1";

            var exceptionThrown = Assert.Throws<GracefulException>(
                () => new WorkloadUninstallCommand(
                    Parser.Parse(["dotnet", "workload", "uninstall", invalidWorkloadId]),
                    reporter: new BufferedReporter(),
                    workloadResolverFactory: new MockWorkloadResolverFactory(),
                    nugetPackageDownloader: new MockNuGetPackageDownloader()));

            exceptionThrown.Message.Should().Be(string.Format(CliCommandStrings.WorkloadNotRecognized, invalidWorkloadId));
        }

        private sealed class DisposableTestDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                nameof(WorkloadCommandInputValidationTests),
                Guid.NewGuid().ToString("N"));

            public DisposableTestDirectory()
            {
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}
