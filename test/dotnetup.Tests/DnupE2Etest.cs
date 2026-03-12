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
                install.Version.Should().Be(expectedVersion!.ToString(),
                    $"Installed version should match resolved version for channel {channel}");
            }
            else
            {
                install.Version.Should().Be(channel);
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
        var (_, channel) = RuntimeInstallCommand.ParseComponentSpec(componentSpec);
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
        var specialChars = OperatingSystem.IsWindows() ? "dotnet with 'special chars'" : "dotnet with \"'special chars'\"";
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

            // Use a temp file to capture dotnetup output instead of process substitution.
            // Process substitution failures are silently ignored by `source`, making
            // debugging impossible when dotnetup can't find its runtime.
            string envScriptOutput = Path.Combine(tempRoot, $"dotnetup-env-{shell}.sh");

            scriptPath = Path.Combine(tempRoot, $"test-env-{shell}.sh");
            scriptContent = $@"#!/bin/{shell}
set -e
# Generate env script to a file (errors will be caught by set -e)
'{Escape(dotnetupPath)}' print-env-script --shell {shell} --dotnet-install-path '{Escape(installPath)}' > '{Escape(envScriptOutput)}'
# Source the generated env script
source '{Escape(envScriptOutput)}'
# Clear cached dotnet path to ensure we use the newly configured PATH
hash -d dotnet 2>/dev/null || true
# Capture versions into variables first to avoid nested quoting issues on macOS bash 3.2
_path_ver=$(dotnet --version)
_root_ver=""$DOTNET_ROOT/dotnet""
_root_ver_out=$(""$_root_ver"" --version)
# Output results
echo ""DOTNET_VERSION=$_path_ver""
echo ""DOTNET_ROOT_VERSION=$_root_ver_out""
echo ""PATH=$PATH""
echo ""DOTNET_ROOT=$DOTNET_ROOT""
";

        }
        else // pwsh / powershell
        {
            static string Escape(string s) => s.Replace("'", "''");

            // Prefer pwsh (PowerShell Core, cross-platform) over powershell.exe (Windows PowerShell 5.1).
            // The generated env script uses standard PowerShell syntax compatible with both.
            shellExecutable = ResolvePowerShellExecutable()!;
            if (shellExecutable is null)
            {
                Console.WriteLine("Skipping pwsh test - neither pwsh nor powershell.exe found on PATH");
                return;
            }

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

        // Make the script executable (only for bash/zsh on Unix)
        if (shell == "bash" || shell == "zsh")
        {
#pragma warning disable CA1416 // Platform compatibility - guarded by shell check above
            File.SetUnixFileMode(scriptPath, File.GetUnixFileMode(scriptPath) | UnixFileMode.UserExecute);
#pragma warning restore CA1416
        }

        // Ensure working directory exists before starting the process
        Directory.CreateDirectory(tempRoot);

        // Run the script
        using var process = new Process();
        process.StartInfo.FileName = shellExecutable;
        process.StartInfo.Arguments = shell == "pwsh" ? $"-File \"{scriptPath}\"" : $"\"{scriptPath}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.WorkingDirectory = tempRoot;

        // Suppress .NET welcome message / first-run experience in test output
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";

        // output which is a framework-dependent AppHost. Ensure DOTNET_ROOT is set so
        // the AppHost can locate the runtime. On CI each script step gets a fresh shell,
        // so DOTNET_ROOT from the restore step isn't inherited. The env script sourced
        // later in the test will overwrite DOTNET_ROOT with the test install path.
        string? currentDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? (Environment.ProcessPath is string processPath ? Path.GetDirectoryName(processPath) : null);
        if (currentDotnetRoot != null)
        {
            process.StartInfo.Environment["DOTNET_ROOT"] = currentDotnetRoot;
        }

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

    private static void VerifyManifestContains(TestEnvironment testEnv, InstallComponent expectedComponent, Action<Installation>? additionalAssertions = null)
    {
        Directory.Exists(testEnv.InstallPath).Should().BeTrue();
        Directory.Exists(Path.GetDirectoryName(testEnv.ManifestPath)).Should().BeTrue();

        List<Installation> installs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installs = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.Installations)
                .ToList();
        }

        installs.Should().NotBeEmpty();
        installs.Should().ContainSingle(i => i.Component == expectedComponent);
        var matchingInstall = installs.First(i => i.Component == expectedComponent);

        additionalAssertions?.Invoke(matchingInstall);
    }

    /// <summary>
    /// Resolves a PowerShell executable, preferring pwsh (PowerShell Core) over powershell.exe (Windows PowerShell 5.1).
    /// Returns null if neither is found.
    /// </summary>
    private static string? ResolvePowerShellExecutable()
    {
        // Try pwsh first (cross-platform PowerShell Core, matches the tool's --shell pwsh argument)
        foreach (var candidate in new[] { "pwsh", "powershell" })
        {
            try
            {
                using var probe = new Process();
                probe.StartInfo.FileName = candidate;
                probe.StartInfo.Arguments = "-NoProfile -Command \"exit 0\"";
                probe.StartInfo.UseShellExecute = false;
                probe.StartInfo.RedirectStandardOutput = true;
                probe.StartInfo.RedirectStandardError = true;
                probe.StartInfo.CreateNoWindow = true;
                probe.Start();
                probe.WaitForExit(5000);
                return candidate;
            }
            catch (Exception)
            {
                // Not found, try next candidate
            }
        }

        return null;
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

        var finalInstalls = new List<Installation>();

        using (var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            finalInstalls = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.Installations)
                .ToList();
        }

        finalInstalls.Should().ContainSingle();
        finalInstalls[0].Version.Should().Be(channel);
    }
}

/// <summary>
/// Tests that verify reuse behavior and error handling for dotnetup installations.
/// </summary>
[Collection("DotnetupReuseCollection")]
public class ReuseAndErrorTests
{
    [Fact]
    public void RuntimeInstall_FeatureBand_ReturnsError()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        // Feature band is not valid for runtime install
        var args = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec("9.0.1xx", testEnv.InstallPath, testEnv.ManifestPath);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(args, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().NotBe(0, "Feature bands should not be valid for runtime installation");
        output.Should().Contain("SDK version or feature band", "should explain that feature bands are not valid for runtimes");
        output.Should().Contain("runtime installation", "should clarify this is a runtime-specific error");
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
    [InlineData("runtime@9.0", InstallComponent.Runtime)] // SDK includes core runtime (using explicit "runtime@version" syntax)
    [InlineData("aspnetcore@9.0", InstallComponent.ASPNETCore)] // SDK also includes aspnetcore runtime
    [InlineData("windowsdesktop@9.0", InstallComponent.WindowsDesktop)] // SDK does include windowsdesktop (Windows only)
    public void RuntimeInstall_AfterSdkInstall_BehavesCorrectly(string componentSpec, InstallComponent expectedComponent)
    {
        // Windows Desktop Runtime is only available on Windows - skip this test case on non-Windows
        if (componentSpec.StartsWith("windowsdesktop") && !OperatingSystem.IsWindows())
        {
            return;
        }

        // This test verifies that:
        // 1. SDK install completes and is tracked in manifest
        // 2. Runtime install for same version succeeds and is tracked separately

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Step 1: Install SDK
        var sdkArgs = DotnetupTestUtilities.BuildSdkArguments("9.0", testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(sdkArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK installation failed. Output:\n{output}");

        // Verify SDK is in manifest
        List<Installation> installsAfterSdk;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installsAfterSdk = manifestData.DotnetRoots
                .SelectMany(r => r.Installations)
                .ToList();
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
        List<Installation> finalInstalls;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            finalInstalls = manifestData.DotnetRoots
                .SelectMany(r => r.Installations)
                .ToList();
        }
        finalInstalls.Should().HaveCount(2, $"both SDK and {expectedComponent} should be tracked");
        finalInstalls.Should().Contain(i => i.Component == InstallComponent.SDK);
        finalInstalls.Should().Contain(i => i.Component == expectedComponent);
    }

    /// <summary>
}

/// <summary>
/// Tests for install/uninstall lifecycle, global.json-based installs, and manifest error handling.
/// </summary>
[Collection("DotnetupLifecycleCollection")]
public class LifecycleEndToEndTests
{
    [Fact]
    public void InstallViaGlobalJson_SdkUsesGlobalJsonSource()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Create a global.json with a pinned SDK version in the test working directory
        string globalJsonPath = Path.Combine(testEnv.TempRoot, "global.json");
        string globalJsonContent = """
            {
              "sdk": {
                "version": "9.0.103",
                "rollForward": "disable"
              }
            }
            """;
        File.WriteAllText(globalJsonPath, globalJsonContent);

        // Install SDK without specifying a version on the command line.
        // When no channel argument is provided, dotnetup should pick up global.json.
        // The sdk install command's channel argument is optional when global.json is present.
        var sdkArgs = new List<string>(["sdk", "install",
            "--install-path", testEnv.InstallPath,
            "--interactive", "false",
            "--no-progress"]);
        if (!string.IsNullOrEmpty(testEnv.ManifestPath))
        {
            sdkArgs.AddRange(["--manifest-path", testEnv.ManifestPath]);
        }

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            [.. sdkArgs], captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK install via global.json failed. Output:\n{output}");

        // Verify the manifest has an SDK install spec with GlobalJson source
        List<InstallSpec> installSpecs;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installSpecs = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.InstallSpecs)
                .ToList();
        }

        installSpecs.Should().ContainSingle(s => s.Component == InstallComponent.SDK,
            "SDK should be tracked in manifest");
        var sdkSpec = installSpecs.First(s => s.Component == InstallComponent.SDK);
        sdkSpec.InstallSource.Should().Be(InstallSource.GlobalJson,
            "SDK installed via global.json should have GlobalJson source");
        sdkSpec.GlobalJsonPath.Should().NotBeNullOrEmpty(
            "SDK install spec should record the global.json path");

        // Now install a runtime explicitly (global.json doesn't specify runtimes)
        var runtimeArgs = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec(
            "9.0", testEnv.InstallPath, testEnv.ManifestPath);
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            runtimeArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"Runtime install failed. Output:\n{output}");

        // Verify install sources differ between SDK (global.json) and runtime (explicit)
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installSpecs = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.InstallSpecs)
                .ToList();
        }

        installSpecs.Should().HaveCount(2, "should have both SDK and runtime specs");
        var sdkSpecAfter = installSpecs.First(s => s.Component == InstallComponent.SDK);
        var runtimeSpec = installSpecs.First(s => s.Component == InstallComponent.Runtime);

        sdkSpecAfter.InstallSource.Should().Be(InstallSource.GlobalJson,
            "SDK source should still be GlobalJson");
        runtimeSpec.InstallSource.Should().Be(InstallSource.Explicit,
            "Runtime source should be Explicit since it was installed on the command line");

        // Verify via machine-readable list output (JSON format)
        var listArgs = DotnetupTestUtilities.BuildListArguments(
            testEnv.InstallPath, testEnv.ManifestPath, format: "Json");
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            listArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"List command failed. Output:\n{output}");
        output.Should().Contain("GlobalJson", "JSON list output should include GlobalJson source for SDK");
        output.Should().Contain("Explicit", "JSON list output should include Explicit source for runtime");
    }

    [Fact]
    public void InstallThenUninstall_FolderIsCleanedUp()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Verify install directory is initially empty (just created by TestEnvironment)
        Directory.GetFileSystemEntries(testEnv.InstallPath).Should().BeEmpty(
            "Install directory should be empty before installation");

        // Step 1: Install SDK
        var installArgs = DotnetupTestUtilities.BuildSdkArguments(
            "9.0.103", testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            installArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK installation failed. Output:\n{output}");

        // Verify the SDK was installed (directory is no longer empty)
        Directory.GetFileSystemEntries(testEnv.InstallPath).Should().NotBeEmpty(
            "Install directory should contain SDK files after installation");

        // Verify manifest has the SDK
        List<Installation> installsBefore;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installsBefore = manifestData.DotnetRoots
                .SelectMany(r => r.Installations)
                .ToList();
        }
        installsBefore.Should().ContainSingle(i => i.Component == InstallComponent.SDK);

        // Step 2: Uninstall SDK
        var uninstallArgs = DotnetupTestUtilities.BuildSdkUninstallArguments(
            "9.0.103", testEnv.InstallPath, testEnv.ManifestPath);
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            uninstallArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK uninstall failed. Output:\n{output}");

        // Verify manifest no longer has the SDK
        List<Installation> installsAfter;
        List<InstallSpec> specsAfter;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installsAfter = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.Installations)
                .ToList();
            specsAfter = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.InstallSpecs)
                .ToList();
        }

        installsAfter.Should().BeEmpty("No installations should remain after uninstall");
        specsAfter.Should().BeEmpty("No install specs should remain after uninstall");

        // Verify that SDK artifacts were cleaned up from the install directory.
        // The directory may still contain the dotnet host executable, which is shared infrastructure.
        // But SDK-specific directories (like sdk/) should be cleaned up.
        var sdkDir = Path.Combine(testEnv.InstallPath, "sdk");
        Directory.Exists(sdkDir).Should().BeFalse(
            "sdk/ directory should be removed after uninstalling the only SDK");
    }

    [Fact]
    public void OutOfSupportManifestVersion_ReturnsError()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Write a legacy-format manifest (JSON array, which is the old format)
        string legacyManifest = """
            [
              {
                "Component": "SDK",
                "Version": "9.0.100",
                "Channel": "9.0"
              }
            ]
            """;
        File.WriteAllText(testEnv.ManifestPath, legacyManifest);

        // Try to install — should fail with a legacy format error
        var args = DotnetupTestUtilities.BuildSdkArguments(
            "9.0.103", testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot);

        exitCode.Should().NotBe(0, "Should fail when manifest uses legacy format");
        output.Should().Contain("legacy format",
            "Error message should mention the legacy format");
        output.Should().Contain("no longer supported",
            "Error message should indicate the format is no longer supported");
    }

    [Fact]
    public void SdkUninstall_DoesNotRemoveExplicitlyInstalledRuntime()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Step 1: Install SDK
        var sdkInstallArgs = DotnetupTestUtilities.BuildSdkArguments(
            "9.0", testEnv.InstallPath, testEnv.ManifestPath);
        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            sdkInstallArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK installation failed. Output:\n{output}");

        // Verify SDK is in manifest
        List<Installation> installsAfterSdk;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            installsAfterSdk = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .SelectMany(r => r.Installations)
                .ToList();
        }
        installsAfterSdk.Should().ContainSingle(i => i.Component == InstallComponent.SDK);

        // Step 2: Install the core runtime separately (explicitly)
        var runtimeInstallArgs = DotnetupTestUtilities.BuildRuntimeArgumentsWithSpec(
            "9.0", testEnv.InstallPath, testEnv.ManifestPath);
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            runtimeInstallArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"Runtime installation failed. Output:\n{output}");

        // Verify both SDK and Runtime are tracked in the manifest
        List<InstallSpec> specsAfterBoth;
        List<Installation> installsAfterBoth;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            var root = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .ToList();
            specsAfterBoth = root.SelectMany(r => r.InstallSpecs).ToList();
            installsAfterBoth = root.SelectMany(r => r.Installations).ToList();
        }

        specsAfterBoth.Should().HaveCount(2, "should have both SDK and runtime install specs");
        specsAfterBoth.Should().Contain(s => s.Component == InstallComponent.SDK);
        specsAfterBoth.Should().Contain(s => s.Component == InstallComponent.Runtime);

        // Step 3: Uninstall the SDK
        var sdkUninstallArgs = DotnetupTestUtilities.BuildSdkUninstallArguments(
            "9.0", testEnv.InstallPath, testEnv.ManifestPath);
        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            sdkUninstallArgs, captureOutput: true, workingDirectory: testEnv.TempRoot);
        exitCode.Should().Be(0, $"SDK uninstall failed. Output:\n{output}");

        // Step 4: Verify the runtime is still tracked but SDK is removed
        List<InstallSpec> specsAfterUninstall;
        List<Installation> installsAfterUninstall;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
            var manifestData = manifest.ReadManifest();
            var root = manifestData.DotnetRoots
                .Where(r => DotnetupUtilities.PathsEqual(r.Path, testEnv.InstallPath))
                .ToList();
            specsAfterUninstall = root.SelectMany(r => r.InstallSpecs).ToList();
            installsAfterUninstall = root.SelectMany(r => r.Installations).ToList();
        }

        specsAfterUninstall.Should().NotContain(s => s.Component == InstallComponent.SDK,
            "SDK install spec should be removed after uninstall");
        specsAfterUninstall.Should().ContainSingle(s => s.Component == InstallComponent.Runtime,
            "Runtime install spec should still be tracked since it was explicitly installed");

        // The runtime installation itself should still be present
        installsAfterUninstall.Should().ContainSingle(i => i.Component == InstallComponent.Runtime,
            "Runtime installation should not be removed when uninstalling the SDK, because it has its own explicit spec");

        // The sdk/ directory should be cleaned up since no SDK spec references it
        var sdkDir = Path.Combine(testEnv.InstallPath, "sdk");
        Directory.Exists(sdkDir).Should().BeFalse(
            "sdk/ directory should be removed after SDK uninstall");

        // But the shared/Microsoft.NETCore.App directory should still exist for the runtime
        var runtimeDir = Path.Combine(testEnv.InstallPath, "shared", "Microsoft.NETCore.App");
        Directory.Exists(runtimeDir).Should().BeTrue(
            "shared/Microsoft.NETCore.App directory should remain for explicitly installed runtime");
    }
}
