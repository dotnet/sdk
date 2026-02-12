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
            VerifyEnvScriptWorks("bash", testEnv.InstallPath, expectedVersion?.ToString(), testEnv.TempRoot);
            VerifyEnvScriptWorks("zsh", testEnv.InstallPath, expectedVersion?.ToString(), testEnv.TempRoot);
        }
        else
        {
            // Test PowerShell on Windows
            VerifyEnvScriptWorks("pwsh", testEnv.InstallPath, expectedVersion?.ToString(), testEnv.TempRoot);
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
        var specialChars = OperatingSystem.IsWindows() ? "dotnet with `'special chars'" : "dotnet with `\"'special chars\\'";
        string specialCharsPath = Path.Combine(testEnv.TempRoot, specialChars);
        Directory.CreateDirectory(specialCharsPath);

        // Install SDK to this special path
        var args = DotnetupTestUtilities.BuildSdkArguments("latest", specialCharsPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK installation failed with special characters in path. Output:\n{output}");

        // Test the env script with each shell
        if (!OperatingSystem.IsWindows())
        {
            // Test bash and zsh on Unix
            VerifyEnvScriptWorks("bash", specialCharsPath, null, testEnv.TempRoot);
            VerifyEnvScriptWorks("zsh", specialCharsPath, null, testEnv.TempRoot);
        }
        else
        {
            // Test PowerShell on Windows
            VerifyEnvScriptWorks("pwsh", specialCharsPath, null, testEnv.TempRoot);
        }
    }

    /// <summary>
    /// Tests that the env script works correctly by sourcing it and verifying environment variables
    /// </summary>
    /// <param name="shell">Shell to test (bash, zsh, or pwsh)</param>
    /// <param name="installPath">Path where dotnet is installed</param>
    /// <param name="expectedVersion">Expected dotnet version to verify</param>
    /// <param name="tempRoot">Temporary directory for test scripts</param>
    private static void VerifyEnvScriptWorks(string shell, string installPath, string? expectedVersion, string tempRoot)
    {
        string dotnetupPath = DotnetupTestUtilities.GetDotnetupExecutablePath();

        // Determine shell-specific settings
        string shellExecutable;
        string scriptPath;
        string scriptContent;

        if (shell == "bash" || shell == "zsh")
        {
            shellExecutable = shell == "bash" ? "/bin/bash" : "/bin/zsh";

            // Skip if shell is not available
            if (!File.Exists(shellExecutable))
            {
                Console.WriteLine($"Skipping {shell} test - shell not available at {shellExecutable}");
                return;
            }

            static string Escape(string s) => s.Replace("'", "'\\''");

            scriptPath = Path.Combine(tempRoot, $"test-env-{shell}.sh");
            scriptContent = $@"#!/bin/{shell}
set -e
source <('{Escape(dotnetupPath)}' print-env-script --shell {shell} --dotnet-install-path '{Escape(installPath)}')
# Clear cached dotnet path to ensure we use the newly configured PATH
hash -d dotnet 2>/dev/null || true
# Capture versions into variables first to avoid nested quoting issues on macOS bash 3.2
_path_ver=$(dotnet --version)
_root_ver=""$DOTNET_ROOT/dotnet""
_root_ver_out=$($_root_ver --version)
# Output results
echo ""DOTNET_VERSION=$_path_ver""
echo ""DOTNET_ROOT_VERSION=$_root_ver_out""
echo ""PATH=$PATH""
echo ""DOTNET_ROOT=$DOTNET_ROOT""
";

            // Make the script executable
            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{scriptPath}\"",
                UseShellExecute = false
            });
            chmod?.WaitForExit();
        }
        else // pwsh
        {
            static string Escape(string s) => s.Replace("'", "''");

            shellExecutable = "pwsh";
            scriptPath = Path.Combine(tempRoot, "test-env.ps1");
            scriptContent = $@"
$ErrorActionPreference = 'Stop'
iex (& '{Escape(dotnetupPath)}' print-env-script --shell pwsh --dotnet-install-path '{Escape(installPath)}' | Out-String)
# Verify both dotnet and DOTNET_ROOT/dotnet return the same version
Write-Output ""DOTNET_VERSION=$(dotnet --version)""
Write-Output ""DOTNET_ROOT_VERSION=$(& ""$env:DOTNET_ROOT/dotnet"" --version)""
Write-Output ""PATH=$env:PATH""
Write-Output ""DOTNET_ROOT=$env:DOTNET_ROOT""
";
        }

        File.WriteAllText(scriptPath, scriptContent);

        // Run the script
        using var process = new Process();
        process.StartInfo.FileName = shellExecutable;
        process.StartInfo.Arguments = shell == "pwsh" ? $"-File \"{scriptPath}\"" : $"\"{scriptPath}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.WorkingDirectory = tempRoot;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        string scriptOutput = outputBuilder.ToString();
        string scriptError = errorBuilder.ToString();

        // Verify the script succeeded
        process.ExitCode.Should().Be(0, $"Script execution failed for {shell}. Output:\n{scriptOutput}\nError:\n{scriptError}");

        // Parse the output lines
        var outputLines = scriptOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        outputLines.Should().HaveCountGreaterThanOrEqualTo(4, $"Should have DOTNET_VERSION, DOTNET_ROOT_VERSION, PATH, and DOTNET_ROOT output for {shell}");

        // Find version lines
        var dotnetVersionLine = outputLines.FirstOrDefault(l => l.StartsWith("DOTNET_VERSION="));
        var dotnetRootVersionLine = outputLines.FirstOrDefault(l => l.StartsWith("DOTNET_ROOT_VERSION="));

        dotnetVersionLine.Should().NotBeNull($"DOTNET_VERSION should be printed for {shell}");
        dotnetRootVersionLine.Should().NotBeNull($"DOTNET_ROOT_VERSION should be printed for {shell}");

        var dotnetVersion = dotnetVersionLine!.Substring("DOTNET_VERSION=".Length).Trim();
        var dotnetRootVersion = dotnetRootVersionLine!.Substring("DOTNET_ROOT_VERSION=".Length).Trim();

        dotnetVersion.Should().NotBeNullOrEmpty($"dotnet --version should produce output for {shell}");
        dotnetRootVersion.Should().NotBeNullOrEmpty($"$DOTNET_ROOT/dotnet --version should produce output for {shell}");

        // Both versions should match (verifies DOTNET_ROOT points to same install as PATH)
        dotnetVersion.Should().Be(dotnetRootVersion, $"dotnet --version and $DOTNET_ROOT/dotnet --version should return the same version for {shell}");

        if (expectedVersion != null)
        {
            dotnetVersion.Should().Be(expectedVersion, $"dotnet version should match expected version for {shell}");
        }
        else
        {
            dotnetVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+", $"dotnet version should be in format x.y.z for {shell}");
        }

        // Find PATH and DOTNET_ROOT lines
        var pathLine = outputLines.FirstOrDefault(l => l.StartsWith("PATH="));
        var dotnetRootLine = outputLines.FirstOrDefault(l => l.StartsWith("DOTNET_ROOT="));

        pathLine.Should().NotBeNull($"PATH should be printed for {shell}");
        dotnetRootLine.Should().NotBeNull($"DOTNET_ROOT should be printed for {shell}");

        // Verify PATH contains the install path (find first entry with 'dotnet' to handle shell startup files that may prepend entries)
        var pathValue = pathLine!.Substring("PATH=".Length);
        var pathSeparator = OperatingSystem.IsWindows() ? ';' : ':';
        var pathEntries = pathValue.Split(pathSeparator);
        var dotnetPathEntries = pathEntries.Where(p => p.Contains("dotnet", StringComparison.OrdinalIgnoreCase)).ToList();
        var firstDotnetPathEntry = dotnetPathEntries.FirstOrDefault();
        firstDotnetPathEntry.Should().Be(installPath, $"First PATH entry containing 'dotnet' should be the dotnet install path for {shell}. Found dotnet entries: [{string.Join(", ", dotnetPathEntries)}]");

        // Verify DOTNET_ROOT matches install path
        var dotnetRootValue = dotnetRootLine!.Substring("DOTNET_ROOT=".Length);
        dotnetRootValue.Should().Be(installPath, $"DOTNET_ROOT should be set to the install path for {shell}");
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
