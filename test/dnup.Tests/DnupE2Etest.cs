// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dnup.Tests.Utilities;
using Microsoft.Dotnet.Installation;
using Xunit;

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
                new DotnetInstallRoot(testEnv.InstallPath, DnupUtilities.GetDefaultInstallArchitecture()),
                updateChannel,
                InstallComponent.SDK,
                new InstallRequestOptions()));

        expectedVersion.Should().NotBeNull($"Channel {channel} should resolve to a valid version");

        Console.WriteLine($"Channel '{channel}' resolved to version: {expectedVersion}");

        // Execute the command with explicit manifest path as a separate process
    var args = DnupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);
    (int exitCode, string output) = DnupTestUtilities.RunDnupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
    exitCode.Should().Be(0, $"dnup exited with code {exitCode}. Output:\n{output}");

        Directory.Exists(testEnv.InstallPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(testEnv.ManifestPath)).Should().BeTrue();

        // Verify the installation was properly recorded in the manifest
        List<DotnetInstall> installs = new();
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DnupSharedManifest(testEnv.ManifestPath);
            installs = manifest.GetInstalledVersions().ToList();
        }

        installs.Should().NotBeEmpty();

        var matchingInstalls = installs.Where(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
        matchingInstalls.Should().ContainSingle();

        var install = matchingInstalls[0];
        install.Component.Should().Be(Microsoft.Dotnet.Installation.InstallComponent.SDK);

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
    (int exitCode, string firstInstallOutput) = DnupTestUtilities.RunDnupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
    exitCode.Should().Be(0, $"First installation failed with exit code {exitCode}. Output:\n{firstInstallOutput}");

        List<DotnetInstall> firstDnupInstalls = new();
        // Verify the installation was successful
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DnupSharedManifest(testEnv.ManifestPath);
            firstDnupInstalls = manifest.GetInstalledVersions().ToList();
        }

        firstDnupInstalls.Where(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).Should().ContainSingle();

        // Now install the same SDK again and capture the console output
        Console.WriteLine($"Installing .NET SDK {channel} again (should be skipped)");
    (exitCode, string output) = DnupTestUtilities.RunDnupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
    exitCode.Should().Be(0, $"Second installation failed with exit code {exitCode}. Output:\n{output}");

        // Verify the output contains a message indicating the SDK is already installed
        output.Should().Contain("is already installed, skipping installation",
            "dnup should detect that the SDK is already installed and skip the installation");

        // The output should not contain download progress
        output.Should().NotContain("Downloading .NET SDK",
            "dnup should not attempt to download the SDK again");

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
    }
}
