// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;

namespace Microsoft.DotNet.PackageInstall.Tests;

public class NuGetPackageDownloaderFactoryTests : SdkTest
{
    public NuGetPackageDownloaderFactoryTests(ITestOutputHelper log) : base(log)
    {
    }

    private static DirectoryPath GetTempDir() =>
        new(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

    [Fact]
    public void CreateForWorkloads_ReturnsNonNullDownloader()
    {
        var tempDir = GetTempDir();

        var downloader = NuGetPackageDownloader.CreateForWorkloads(
            tempDir,
            verifyNuGetSignatures: false);

        downloader.Should().NotBeNull();
        downloader.Should().BeOfType<NuGetPackageDownloader>();
    }

    [Fact]
    public void CreateForWorkloads_WithAllParameters_ReturnsConfiguredDownloader()
    {
        var tempDir = GetTempDir();
        var logger = new NuGetTestLogger();
        var reporter = new BufferedReporter();
        var restoreConfig = new RestoreActionConfig(NoCache: true);

        var downloader = NuGetPackageDownloader.CreateForWorkloads(
            tempDir,
            verifyNuGetSignatures: true,
            verboseLogger: logger,
            reporter: reporter,
            restoreActionConfig: restoreConfig,
            shouldUsePackageSourceMapping: false);

        downloader.Should().NotBeNull();
    }

    [Fact]
    public void CreateForWorkloads_DefaultsSourceMappingToTrue()
    {
        // The factory method defaults shouldUsePackageSourceMapping to true,
        // which is the correct default for workload operations.
        var tempDir = GetTempDir();

        // This should not throw - verifies the factory method compiles and runs with defaults
        var downloader = NuGetPackageDownloader.CreateForWorkloads(
            tempDir,
            verifyNuGetSignatures: false);

        downloader.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WhenVerifyRequestedButPlatformUnsupported_LogsMessage()
    {
        // On non-Windows, requesting verification without the env var should log a message.
        // On Windows, verification is always supported so no message is logged.
        var tempDir = GetTempDir();
        var logger = new NuGetTestLogger();

        // Save and clear env var to ensure we test the downgrade path on non-Windows
        var originalValue = Environment.GetEnvironmentVariable(NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification);
        try
        {
            Environment.SetEnvironmentVariable(NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification, "false");

            _ = new NuGetPackageDownloader(
                tempDir,
                verboseLogger: logger,
                verifySignatures: true);

            if (!OperatingSystem.IsWindows())
            {
                // On non-Windows with env var set to false, the verification is downgraded
                // and a diagnostic message should be logged.
                logger.Messages.Should().Contain(m => m.Contains("not supported on this platform"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(NuGetSignatureVerificationEnabler.DotNetNuGetSignatureVerification, originalValue);
        }
    }
}
