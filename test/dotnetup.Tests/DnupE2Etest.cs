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
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for installing different .NET SDK versions using dotnetup.
/// Each test run can happen in parallel with other tests in different collections.
/// </summary>
[Collection("DotnetupInstallCollection")]
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
    /// End-to-end test for installing different .NET SDK versions using dotnetup.
    /// This test creates a temporary directory and sets the current directory to it
    /// to avoid conflicts with the global.json in the repository root.
    /// </summary>
    [Theory]
    [MemberData(nameof(InstallChannels))]
    public void Test(string channel)
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // First verify what version dotnetup should resolve this channel to
        var updateChannel = new UpdateChannel(channel);
        var expectedVersion = new ChannelVersionResolver().Resolve(
            new DotnetInstallRequest(
                new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
                updateChannel,
                InstallComponent.SDK,
                new InstallRequestOptions()));

        expectedVersion.Should().NotBeNull($"Channel {channel} should resolve to a valid version");

        Console.WriteLine($"Channel '{channel}' resolved to version: {expectedVersion}");

        // Execute the command with explicit manifest path as a separate process
        var args = DotnetupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"dotnetup exited with code {exitCode}. Output:\n{output}");

        Directory.Exists(testEnv.InstallPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(testEnv.ManifestPath)).Should().BeTrue();

        // Verify the installation was properly recorded in the manifest
        List<DotnetInstall> installs = new();
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            installs = manifest.GetInstalledVersions().ToList();
        }

        installs.Should().NotBeEmpty();

        var matchingInstalls = installs.Where(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
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
/// Tests that verify reuse behavior of dotnetup installations.
/// Each test run can happen in parallel with other tests in different collections.
/// </summary>
[Collection("DotnetupReuseCollection")]
public class ReuseEndToEndTests
{
    /// <summary>
    /// Test that verifies that installing the same SDK version twice doesn't require
    /// dotnetup to download and install it again.
    /// </summary>
    [Fact]
    public void TestReusesExistingInstall()
    {
        // We'll use a specific version for this test to ensure consistent results
        const string channel = "9.0.103";

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);

        // Execute dotnetup to install the SDK the first time with explicit manifest path as a separate process
        Console.WriteLine($"First installation of {channel}");
        (int exitCode, string firstInstallOutput) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"First installation failed with exit code {exitCode}. Output:\n{firstInstallOutput}");

        List<DotnetInstall> firstDotnetupInstalls = new();
        // Verify the installation was successful
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            firstDotnetupInstalls = manifest.GetInstalledVersions().ToList();
        }

        firstDotnetupInstalls.Where(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).Should().ContainSingle();

        // Now install the same SDK again and capture the console output
        Console.WriteLine($"Installing .NET SDK {channel} again (should be skipped)");
        (exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"Second installation failed with exit code {exitCode}. Output:\n{output}");

        // Verify the output contains a message indicating the SDK is already installed
        output.Should().Contain("is already installed, skipping installation",
            "dotnetup should detect that the SDK is already installed and skip the installation");

        // The output should not contain download progress
        output.Should().NotContain("Downloading .NET SDK",
            "dotnetup should not attempt to download the SDK again");

        List<DotnetInstall> matchingInstalls = [];
        // Verify the installation record in the manifest hasn't changed
        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var installs = manifest.GetInstalledVersions();
            matchingInstalls = installs.Where(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
        }

        // Should still only have one installation
        matchingInstalls.Should().ContainSingle();
        // And it should be for the specified version
        matchingInstalls[0].Version.ToString().Should().Be(channel);
    }
}

/// <summary>
/// Tests that cover concurrent installs targeting the same install root and manifest.
/// </summary>
[Collection("DotnetupConcurrencyCollection")]
public class ConcurrentInstallationTests
{
    [Fact]
    public async Task ConcurrentInstallsSerializeViaGlobalMutex()
    {
        const string channel = "9.0.103";

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args1 = DotnetupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);
        var args2 = DotnetupTestUtilities.BuildArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);

        var installTask1 = Task.Run(() => DotnetupTestUtilities.RunDotnetupProcess(args1, captureOutput: true, workingDirectory: testEnv.TempRoot));
        var installTask2 = Task.Run(() => DotnetupTestUtilities.RunDotnetupProcess(args2, captureOutput: true, workingDirectory: testEnv.TempRoot));

        var results = await Task.WhenAll(installTask1, installTask2);

        results[0].exitCode.Should().Be(0,
            $"First concurrent install failed with exit code {results[0].exitCode}. Output:\n{results[0].output}");
        results[1].exitCode.Should().Be(0,
            $"Second concurrent install failed with exit code {results[1].exitCode}. Output:\n{results[1].output}");

        var finalInstalls = new List<DotnetInstall>();

        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            finalInstalls = manifest.GetInstalledVersions().Where(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
        }

        finalInstalls.Should().ContainSingle();
        finalInstalls[0].Version.ToString().Should().Be(channel);
    }
}
