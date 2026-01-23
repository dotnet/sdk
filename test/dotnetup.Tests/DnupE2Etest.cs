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
    /// <summary>
    /// SDK install channels to test.
    /// </summary>
    public static IEnumerable<object[]> SdkChannels => new List<object[]>
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
    /// Runtime install test data: (runtimeType, channel, expectedComponent).
    /// </summary>
    public static IEnumerable<object[]> RuntimeChannels => new List<object[]>
    {
        new object[] { "core", "9.0", InstallComponent.Runtime },
        new object[] { "core", "latest", InstallComponent.Runtime },
        new object[] { "core", "lts", InstallComponent.Runtime },
        new object[] { "aspnetcore", "9.0", InstallComponent.ASPNETCore },
        new object[] { "windowsdesktop", "9.0", InstallComponent.WindowsDesktop },
    };

    [Theory]
    [MemberData(nameof(SdkChannels))]
    public void SdkInstall(string channel)
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var updateChannel = new UpdateChannel(channel);
        var expectedVersion = new ChannelVersionResolver().Resolve(
            new DotnetInstallRequest(
                new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
                updateChannel,
                InstallComponent.SDK,
                new InstallRequestOptions()));

        expectedVersion.Should().NotBeNull($"Channel {channel} should resolve to a valid version");
        Console.WriteLine($"Channel '{channel}' resolved to version: {expectedVersion}");

        var args = DotnetupTestUtilities.BuildSdkArguments(channel, testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"dotnetup exited with code {exitCode}. Output:\n{output}");

        VerifyManifestContains(testEnv, InstallComponent.SDK, install =>
        {
            if (!updateChannel.IsFullySpecifiedVersion())
            {
                install.Version.ToString().Should().Be(expectedVersion!.ToString(),
                    $"Installed version should match resolved version for channel {channel}");
            }
            else
            {
                install.Version.ToString().Should().Be(channel);
            }
        });
    }

    [Theory]
    [MemberData(nameof(RuntimeChannels))]
    public void RuntimeInstall(string runtimeType, string channel, InstallComponent expectedComponent)
    {
        // Skip Windows Desktop on non-Windows
        if (expectedComponent == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
        {
            return;
        }

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        var expectedVersion = new ChannelVersionResolver().GetLatestVersionForChannel(new UpdateChannel(channel), expectedComponent);
        expectedVersion.Should().NotBeNull($"Channel {channel} should resolve to a valid {runtimeType} version");
        Console.WriteLine($"Runtime '{runtimeType}' channel '{channel}' resolved to version: {expectedVersion}");

        var args = DotnetupTestUtilities.BuildRuntimeArguments(runtimeType, channel, testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"dotnetup exited with code {exitCode}. Output:\n{output}");

        VerifyManifestContains(testEnv, expectedComponent);
    }

    private static void VerifyManifestContains(TestEnvironment testEnv, InstallComponent expectedComponent, Action<DotnetInstall>? additionalAssertions = null)
    {
        Directory.Exists(testEnv.InstallPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(testEnv.ManifestPath)).Should().BeTrue();

        List<DotnetInstall> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            installs = manifest.GetInstalledVersions().ToList();
        }

        installs.Should().NotBeEmpty();
        var matchingInstalls = installs.Where(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath)).ToList();
        matchingInstalls.Should().ContainSingle();
        matchingInstalls[0].Component.Should().Be(expectedComponent);

        additionalAssertions?.Invoke(matchingInstalls[0]);
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

/// <summary>
/// Tests that verify reuse behavior and error handling for dotnetup installations.
/// </summary>
[Collection("DotnetupReuseCollection")]
public class ReuseAndErrorTests
{
    public static IEnumerable<object?[]> ReuseTestData => new List<object?[]>
    {
        new object?[] { "sdk", "9.0.103", null },
        new object?[] { "runtime", "9.0", "core" },
    };

    [Theory]
    [MemberData(nameof(ReuseTestData))]
    public void Install_ReusesExistingInstall(string componentType, string channel, string? runtimeType)
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = componentType == "sdk"
            ? DotnetupTestUtilities.BuildSdkArguments(channel, testEnv.InstallPath, testEnv.ManifestPath)
            : DotnetupTestUtilities.BuildRuntimeArguments(runtimeType!, channel, testEnv.InstallPath, testEnv.ManifestPath);

        // First install
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"First installation failed. Output:\n{output}");

        // Second install should be skipped
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"Second installation failed. Output:\n{output}");
        output.Should().Contain("is already installed, skipping installation");
    }

    [Fact]
    public void RuntimeInstall_FeatureBand_ReturnsError()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildRuntimeArguments("core", "9.0.1xx", testEnv.InstallPath, testEnv.ManifestPath);

        (int exitCode, _) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().NotBe(0, "Feature bands should not be valid for runtime installation");
    }

    [Fact]
    public void RuntimeInstall_InvalidType_ReturnsError()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildRuntimeArguments("invalid", "9.0", testEnv.InstallPath, testEnv.ManifestPath);

        (int exitCode, _) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().NotBe(0, "Invalid runtime type should return error");
    }
}
