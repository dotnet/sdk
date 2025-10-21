// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dnup.Tests.Utilities;
using Xunit;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dnup.Tests;

/// <summary>
/// Tests for installing different .NET SDK versions using dnup.
/// Each test run can happen in parallel with other tests in different collections.
/// </summary>
[Collection("DnupInstallCollection")]
public class InstallEndToEndTests
{
    public static IEnumerable<object[]> InstallChannels => new List<object[]>
    {
        new object[] { "9" },
        new object[] { "9.0" },
        new object[] { "9.0.103" },
        new object[] { "9.0.1xx" },
        new object[] { "latest" },
        new object[] { "preview" },
        new object[] { "sts" },
        new object[] { "lts" },
    };

    /// <summary>
    /// End-to-end test for installing different .NET SDK versions using dnup.
    /// This test creates a temporary directory and sets the current directory to it
    /// to avoid conflicts with the global.json in the repository root.
    /// </summary>
    [Theory]
    [MemberData(nameof(InstallChannels))]
    public void Test(string channel)
    {
        using var testEnv = DnupTestUtilities.CreateTestEnvironment();

        // First verify what version dnup should resolve this channel to
        var updateChannel = new UpdateChannel(channel);
        var expectedVersion = new ManifestChannelVersionResolver().Resolve(
            new DotnetInstallRequest(
                new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
                updateChannel,
                InstallComponent.SDK,
                new InstallRequestOptions()));

        expectedVersion.Should().NotBeNull($"Channel {channel} should resolve to a valid version");

        Console.WriteLine($"Channel '{channel}' resolved to version: {expectedVersion}");

        // Execute the command with explicit manifest path as a separate process
        var args = DnupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);

        DnupProcessResult result = DnupTestUtilities.RunDnupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        result.ExitCode.Should().Be(0,
            $"dnup exited with code {result.ExitCode}. Output:\n{DnupTestUtilities.FormatOutputForAssertions(result)}");

        Directory.Exists(testEnv.InstallPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(testEnv.ManifestPath)).Should().BeTrue();

        // Verify the installation was properly recorded in the manifest
        List<DotnetInstall> installs = [];
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DnupSharedManifest(testEnv.ManifestPath);
            installs = manifest.GetInstalledVersions().ToList();
        }

        installs.Should().NotBeEmpty();

        var matchingInstalls = installs.Where(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
        matchingInstalls.Should().ContainSingle();

        var install = matchingInstalls[0];
        install.Component.Should().Be(InstallComponent.SDK);

        DnupTestUtilities.ValidateInstall(install).Should().BeTrue(
            $"ArchiveInstallationValidator failed for installed version {install.Version} at {testEnv.InstallPath}");

        // Verify the installed version matches what the resolver predicted
        if (!updateChannel.IsFullySpecifiedVersion())
        {
            // For channels that are not fully specified versions (like "9", "preview", "lts"),
            // verify that the installed version matches what the resolver predicted
            install.Version.ToString().Should().Be(expectedVersion!.ToString(),
                $"Installed version should match resolved version for channel {channel}");
        }
        else
        {
            // For fully specified versions (like "9.0.103"), the installed version should be exactly what was requested
            install.Version.ToString().Should().Be(channel);
        }
    }
}

/// <summary>
/// Tests that verify reuse behavior of dnup installations.
/// Each test run can happen in parallel with other tests in different collections.
/// </summary>
[Collection("DnupReuseCollection")]
public class ReuseEndToEndTests
{
    /// <summary>
    /// Test that verifies that installing the same SDK version twice doesn't require
    /// dnup to download and install it again.
    /// </summary>
    [Fact]
    public void TestReusesExistingInstall()
    {
        // We'll use a specific version for this test to ensure consistent results
        const string channel = "9.0.103";

        using var testEnv = DnupTestUtilities.CreateTestEnvironment();
        var args = DnupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);

        // Execute dnup to install the SDK the first time with explicit manifest path as a separate process
        Console.WriteLine($"First installation of {channel}");
        DnupProcessResult firstInstall = DnupTestUtilities.RunDnupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        firstInstall.ExitCode.Should().Be(0,
            $"First installation failed with exit code {firstInstall.ExitCode}. Output:\n{DnupTestUtilities.FormatOutputForAssertions(firstInstall)}");

        List<DotnetInstall> firstDnupInstalls = new();
        // Verify the installation was successful
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DnupSharedManifest(testEnv.ManifestPath);
            firstDnupInstalls = manifest.GetInstalledVersions().ToList();
        }

        var firstInstallRecord = firstDnupInstalls.Single(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath));
        DnupTestUtilities.ValidateInstall(firstInstallRecord).Should().BeTrue(
            $"ArchiveInstallationValidator failed for initial install of {channel} at {testEnv.InstallPath}");

        // Now install the same SDK again and capture the console output
        Console.WriteLine($"Installing .NET SDK {channel} again (should be skipped)");
        DnupProcessResult secondInstall = DnupTestUtilities.RunDnupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        secondInstall.ExitCode.Should().Be(0,
            $"Second installation failed with exit code {secondInstall.ExitCode}. Output:\n{DnupTestUtilities.FormatOutputForAssertions(secondInstall)}");

        DnupTestUtilities.AssertOutput(secondInstall, output =>
        {
            output.Should().Contain("is already installed, skipping installation",
                "dnup should detect that the SDK is already installed and skip the installation");

            output.Should().NotContain("Downloading .NET SDK",
                "dnup should not attempt to download the SDK again");
        });

        List<DotnetInstall> matchingInstalls = new();
        // Verify the installation record in the manifest hasn't changed
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DnupSharedManifest(testEnv.ManifestPath);
            var installs = manifest.GetInstalledVersions();
            matchingInstalls = installs.Where(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
        }

        // Should still only have one installation
        matchingInstalls.Should().ContainSingle();
        // And it should be for the specified version
        matchingInstalls[0].Version.ToString().Should().Be(channel);
        DnupTestUtilities.ValidateInstall(matchingInstalls[0]).Should().BeTrue(
            $"ArchiveInstallationValidator failed after reinstall attempt for {channel} at {testEnv.InstallPath}");
    }
}

/// <summary>
/// Tests that cover concurrent installs targeting the same install root and manifest.
/// </summary>
[Collection("DnupConcurrencyCollection")]
public class ConcurrentInstallationTests
{
    public static IEnumerable<object[]> ConcurrentInstallChannels => new List<object[]>
    {
        new object[] { "9.0.103", "9.0.103", false },
        new object[] { "9.0.103", "preview", true }
    };

    [Theory]
    [MemberData(nameof(ConcurrentInstallChannels))]
    public async Task ConcurrentInstallsSerializeViaGlobalMutex(string firstChannel, string secondChannel, bool expectDistinct)
    {
        using var testEnv = DnupTestUtilities.CreateTestEnvironment();

        var resolver = new ManifestChannelVersionResolver();
        ReleaseVersion? firstResolved = resolver.Resolve(
            new DotnetInstallRequest(
                new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
                new UpdateChannel(firstChannel),
                InstallComponent.SDK,
                new InstallRequestOptions()));
        ReleaseVersion? secondResolved = resolver.Resolve(
            new DotnetInstallRequest(
                new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
                new UpdateChannel(secondChannel),
                InstallComponent.SDK,
                new InstallRequestOptions()));

        firstResolved.Should().NotBeNull($"Channel {firstChannel} should resolve to a version");
        secondResolved.Should().NotBeNull($"Channel {secondChannel} should resolve to a version");

        if (expectDistinct && string.Equals(firstResolved!.ToString(), secondResolved!.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skipping concurrent distinct-version scenario because both channels resolved to {firstResolved}");
            return;
        }
        var args1 = DnupTestUtilities.BuildArguments(firstChannel, testEnv.InstallPath, testEnv.ManifestPath);
        var args2 = DnupTestUtilities.BuildArguments(secondChannel, testEnv.InstallPath, testEnv.ManifestPath);

        var installTask1 = Task.Run(() => DnupTestUtilities.RunDnupProcess(args1, captureOutput: true, workingDirectory: testEnv.TempRoot));
        var installTask2 = Task.Run(() => DnupTestUtilities.RunDnupProcess(args2, captureOutput: true, workingDirectory: testEnv.TempRoot));

        DnupProcessResult[] results = await Task.WhenAll(installTask1, installTask2);

        results[0].ExitCode.Should().Be(0,
            $"First concurrent install failed with exit code {results[0].ExitCode}. Output:\n{DnupTestUtilities.FormatOutputForAssertions(results[0])}");
        results[1].ExitCode.Should().Be(0,
            $"Second concurrent install failed with exit code {results[1].ExitCode}. Output:\n{DnupTestUtilities.FormatOutputForAssertions(results[1])}");

        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DnupSharedManifest(testEnv.ManifestPath);
            var installs = manifest.GetInstalledVersions()
                .Where(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath))
                .ToList();

            int expectedInstallCount = string.Equals(firstResolved!.ToString(), secondResolved!.ToString(), StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            installs.Should().HaveCount(expectedInstallCount);

            var expectedVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                firstResolved.ToString()!,
                secondResolved!.ToString()!
            };

            foreach (var install in installs)
            {
                install.Component.Should().Be(InstallComponent.SDK);
                expectedVersions.Should().Contain(install.Version.ToString());
                DnupTestUtilities.ValidateInstall(install).Should().BeTrue(
                    $"ArchiveInstallationValidator failed for concurrent install {install.Version} at {testEnv.InstallPath}");
            }

            var actualVersions = installs.Select(i => i.Version.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            actualVersions.Should().BeEquivalentTo(expectedVersions);
        }
    }
}
