// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
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
    /// Runtime install test data: (componentSpec, expectedComponent).
    /// Uses new component@version syntax:
    /// - Plain version (e.g., "9.0") installs core runtime
    /// - component@version (e.g., "aspnetcore@9.0") installs specific runtime
    /// </summary>
    public static IEnumerable<object[]> RuntimeChannels => new List<object[]>
    {
        new object[] { "9.0", InstallComponent.Runtime },          // Plain version defaults to core runtime
        new object[] { "latest", InstallComponent.Runtime },       // Channel defaults to core runtime
        new object[] { "lts", InstallComponent.Runtime },          // Channel defaults to core runtime
        new object[] { "aspnetcore@9.0", InstallComponent.ASPNETCore },
        new object[] { "windowsdesktop@9.0", InstallComponent.WindowsDesktop },
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

        // Test that the env script works correctly
        if (!OperatingSystem.IsWindows())
        {
            // Test bash and zsh on Unix
            DotnetupTestUtilities.VerifyEnvScriptWorks("bash", testEnv.InstallPath, expectedVersion?.ToString(), testEnv.TempRoot);
            DotnetupTestUtilities.VerifyEnvScriptWorks("zsh", testEnv.InstallPath, expectedVersion?.ToString(), testEnv.TempRoot);
        }
        else
        {
            // Test PowerShell on Windows
            DotnetupTestUtilities.VerifyEnvScriptWorks("pwsh", testEnv.InstallPath, expectedVersion?.ToString(), testEnv.TempRoot);
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeChannels))]
    public void RuntimeInstall(string componentSpec, InstallComponent expectedComponent)
    {
        // Skip Windows Desktop on non-Windows
        if (expectedComponent == InstallComponent.WindowsDesktop && !OperatingSystem.IsWindows())
        {
            return;
        }

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Parse the component spec to get the channel for version resolution (using production code)
        var (_, channel, _) = RuntimeInstallCommand.ParseComponentSpec(componentSpec);
        var expectedVersion = new ChannelVersionResolver().GetLatestVersionForChannel(new UpdateChannel(channel ?? "latest"), expectedComponent);
        expectedVersion.Should().NotBeNull($"Channel {channel} should resolve to a valid {expectedComponent} version");
        Console.WriteLine($"Component spec '{componentSpec}' resolved to version: {expectedVersion}");

        var args = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec(componentSpec, testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"dotnetup exited with code {exitCode}. Output:\n{output}");

        VerifyManifestContains(testEnv, expectedComponent);
    }

    [Fact]
    public void EnvScript_WorksWithSpecialCharactersInPath()
    {
        // Test that env scripts work correctly when the install path contains special characters
        // such as spaces and single quotes

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Create an install path with special characters (spaces and single quotes)
        string specialCharsPath = Path.Combine(testEnv.TempRoot, "dotnet with 'special chars'");
        Directory.CreateDirectory(specialCharsPath);

        // Install SDK to this special path
        var args = DotnetupTestUtilities.BuildSdkArguments("9.0", specialCharsPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK installation failed with special characters in path. Output:\n{output}");

        // Test the env script with each shell
        if (!OperatingSystem.IsWindows())
        {
            // Test bash and zsh on Unix
            DotnetupTestUtilities.VerifyEnvScriptWorks("bash", specialCharsPath, null, testEnv.TempRoot);
            DotnetupTestUtilities.VerifyEnvScriptWorks("zsh", specialCharsPath, null, testEnv.TempRoot);
        }
        else
        {
            // Test PowerShell on Windows
            DotnetupTestUtilities.VerifyEnvScriptWorks("pwsh", specialCharsPath, null, testEnv.TempRoot);
        }
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
        new object?[] { "sdk", "9.0.103" },
        new object?[] { "runtime", "9.0" },  // Uses new component@version syntax (plain version = core runtime)
    };

    [Theory]
    [MemberData(nameof(ReuseTestData))]
    public void Install_ReusesExistingInstall(string componentType, string channelOrSpec)
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = componentType == "sdk"
            ? DotnetupTestUtilities.BuildSdkArguments(channelOrSpec, testEnv.InstallPath, testEnv.ManifestPath)
            : DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec(channelOrSpec, testEnv.InstallPath, testEnv.ManifestPath);

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
        // Feature band is not valid for runtime install
        var args = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec("9.0.1xx", testEnv.InstallPath, testEnv.ManifestPath);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().NotBe(0, "Feature bands should not be valid for runtime installation");
        output.Should().Contain("SDK version or feature band", "should explain that feature bands are not valid for runtimes");
        output.Should().Contain("not valid for runtime", "should clarify this is a runtime-specific error");
    }

    [Fact]
    public void RuntimeInstall_InvalidComponent_ReturnsError()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        // Invalid component name in component@version syntax
        var args = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec("invalid@9.0", testEnv.InstallPath, testEnv.ManifestPath);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().NotBe(0, "Invalid component type should return error");
        output.Should().Contain("Unknown component type", "should indicate invalid component type");
    }

    [Fact]
    public void RuntimeInstall_WindowsDesktop_OnNonWindows_ReturnsError()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // This test only applies to non-Windows platforms
        }

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec("windowsdesktop@9.0", testEnv.InstallPath, testEnv.ManifestPath);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().NotBe(0, "Windows Desktop Runtime should not be installable on non-Windows");
        output.Should().Contain("Windows Desktop Runtime is only available on Windows", "should explain Windows Desktop is Windows-only");
    }

    [Theory]
    [InlineData("runtime@9.0", InstallComponent.Runtime, true)] // SDK includes core runtime (using explicit "runtime@version" syntax)
    [InlineData("aspnetcore@9.0", InstallComponent.ASPNETCore, true)] // SDK also includes aspnetcore runtime
    [InlineData("windowsdesktop@9.0", InstallComponent.WindowsDesktop, true)] // SDK does include windowsdesktop (Windows only)
    public void RuntimeInstall_AfterSdkInstall_BehavesCorrectly(string componentSpec, InstallComponent expectedComponent, bool shouldSkipDownload)
    {
        // Windows Desktop Runtime is only available on Windows - skip this test case on non-Windows
        if (componentSpec.StartsWith("windowsdesktop") && !OperatingSystem.IsWindows())
        {
            return;
        }

        // This test verifies that:
        // 1. SDK install completes and is tracked in manifest
        // 2. Runtime install for same version succeeds and is tracked separately
        // 3. Runtime reuses files from SDK if already present (no download)

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Step 1: Install SDK
        var sdkArgs = DotnetupTestUtilities.BuildSdkArguments("9.0", testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(sdkArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK installation failed. Output:\n{output}");

        // Verify SDK is in manifest
        List<DotnetInstall> installsAfterSdk;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            installsAfterSdk = manifest.GetInstalledVersions().ToList();
        }
        installsAfterSdk.Should().ContainSingle(i => i.Component == InstallComponent.SDK);
        // Verify target runtime is NOT in manifest yet
        installsAfterSdk.Should().NotContain(i => i.Component == expectedComponent,
            $"{expectedComponent} should not be in manifest before explicit runtime install");

        // Step 2: Install runtime for same major.minor using component@version syntax
        var runtimeArgs = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec(componentSpec, testEnv.InstallPath, testEnv.ManifestPath);
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(runtimeArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"Runtime installation failed. Output:\n{output}");

        // Step 3: Verify both SDK and Runtime are in manifest
        List<DotnetInstall> finalInstalls;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            finalInstalls = manifest.GetInstalledVersions().ToList();
        }
        finalInstalls.Should().HaveCount(2, $"both SDK and {expectedComponent} should be tracked");
        finalInstalls.Should().Contain(i => i.Component == InstallComponent.SDK);
        finalInstalls.Should().Contain(i => i.Component == expectedComponent);

        // Step 4: Verify correct behavior based on whether SDK included this runtime
        if (shouldSkipDownload)
        {
            output.Should().Contain("files already exist", "Should detect files already exist from SDK");
            output.Should().NotContain("Downloading", "Should not download when files already exist from SDK");
        }
    }
}
