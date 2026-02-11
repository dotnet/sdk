// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Common utilities for dotnetup tests
/// </summary>
internal static class DotnetupTestUtilities
{
    /// <summary>
    /// Creates a test environment with proper temporary directories
    /// </summary>
    public static TestEnvironment CreateTestEnvironment()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "dotnetup-e2e", Guid.NewGuid().ToString("N"));
        string installPath = Path.Combine(tempRoot, "dotnet-root");
        string manifestPath = Path.Combine(tempRoot, "dotnetup_manifest.json");

        // Create necessary directories
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        return new TestEnvironment(tempRoot, installPath, manifestPath);
    }

    /// <summary>
    /// Builds command line arguments for SDK install
    /// </summary>
    public static string[] BuildSdkArguments(string channel, string installPath, string? manifestPath = null, bool disableProgress = true)
        => BuildArguments(InstallComponent.SDK, channel, installPath, manifestPath, disableProgress, runtimeType: null);

    /// <summary>
    /// Builds command line arguments for runtime install using the new component@version syntax.
    /// This delegates to BuildArguments with the componentSpec pre-formatted.
    /// </summary>
    public static string[] BuildRuntimeArgumentsWithSpec(string componentSpec, string installPath, string? manifestPath = null, bool disableProgress = true)
        => BuildArgumentsCore(["runtime", "install", componentSpec], installPath, manifestPath, disableProgress);

    /// <summary>
    /// Builds command line arguments for runtime install
    /// </summary>
    public static string[] BuildRuntimeArguments(string runtimeType, string channel, string installPath, string? manifestPath = null, bool disableProgress = true)
        => BuildArguments(InstallComponent.Runtime, channel, installPath, manifestPath, disableProgress, runtimeType);

    /// <summary>
    /// Builds command line arguments for dotnetup (legacy - defaults to SDK)
    /// </summary>
    public static string[] BuildArguments(string channel, string installPath, string? manifestPath = null, bool disableProgress = true)
        => BuildSdkArguments(channel, installPath, manifestPath, disableProgress);

    /// <summary>
    /// Builds command line arguments for dotnetup
    /// </summary>
    public static string[] BuildArguments(InstallComponent component, string channel, string installPath, string? manifestPath = null, bool disableProgress = true, string? runtimeType = null)
    {
        var commandArgs = new List<string>();

        if (component == InstallComponent.SDK)
        {
            commandArgs.AddRange(["sdk", "install", channel]);
        }
        else
        {
            // Runtime install: dotnetup runtime install <component@version> or dotnetup runtime install <version>
            // Format: "runtime" defaults to core runtime, "aspnetcore@9.0" for ASP.NET Core, etc.
            string componentSpec = runtimeType is null or "core" or "runtime"
                ? channel  // Just version for core runtime (e.g., "9.0")
                : $"{runtimeType}@{channel}";  // component@version for others (e.g., "aspnetcore@9.0")
            commandArgs.AddRange(["runtime", "install", componentSpec]);
        }

        return BuildArgumentsCore(commandArgs, installPath, manifestPath, disableProgress);
    }

    /// <summary>
    /// Core method that appends common options to command arguments.
    /// </summary>
    private static string[] BuildArgumentsCore(List<string> commandArgs, string installPath, string? manifestPath, bool disableProgress)
    {
        commandArgs.AddRange(["--install-path", installPath, "--interactive", "false"]);

        if (!string.IsNullOrEmpty(manifestPath))
        {
            commandArgs.AddRange(["--manifest-path", manifestPath]);
        }

        // Add no-progress option when running tests in parallel to avoid Spectre.Console exclusivity issues
        if (disableProgress)
        {
            commandArgs.Add("--no-progress");
        }

        return [.. commandArgs];
    }

    /// <summary>
    /// Gets the path to the dotnetup executable for the current build configuration
    /// </summary>
    /// <returns>Full path to dotnetup executable</returns>
    public static string GetDotnetupExecutablePath()
    {
#if DEBUG
        string configuration = "Debug";
#else
        string configuration = "Release";
#endif

        string repoRoot = GetRepositoryRoot();
        string executableName = OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup";
        string dotnetupPath = Path.Combine(
            repoRoot,
            "artifacts", "bin", "dotnetup", configuration, "net10.0", executableName);

        // Ensure path is normalized and exists
        dotnetupPath = Path.GetFullPath(dotnetupPath);
        if (!File.Exists(dotnetupPath))
        {
            throw new FileNotFoundException($"dotnetup executable not found at: {dotnetupPath}");
        }

        return dotnetupPath;
    }

    /// <summary>
    /// Tests that the env script works correctly by sourcing it and verifying environment variables
    /// </summary>
    /// <param name="shell">Shell to test (bash, zsh, or pwsh)</param>
    /// <param name="installPath">Path where dotnet is installed</param>
    /// <param name="expectedVersion">Expected dotnet version to verify</param>
    public static void VerifyEnvScriptWorks(string shell, string installPath, string? expectedVersion, string tempRoot)
    {
        string dotnetupPath = GetDotnetupExecutablePath();

        if (shell == "bash" || shell == "zsh")
        {
            string shellExecutable = shell == "bash" ? "/bin/bash" : "/bin/zsh";

            // Skip if shell is not available
            if (!File.Exists(shellExecutable))
            {
                Console.WriteLine($"Skipping {shell} test - shell not available at {shellExecutable}");
                return;
            }

            // Create a shell script that sources the env script and prints environment info
            string scriptPath = Path.Combine(tempRoot, $"test-env-{shell}.sh");
            string scriptContent = $@"#!/bin/{shell}
set -e
source <(""{dotnetupPath}"" print-env-script --shell {shell} --dotnet-install-path ""{installPath}"")
dotnet --version
echo ""PATH=$PATH""
echo ""DOTNET_ROOT=$DOTNET_ROOT""
";
            File.WriteAllText(scriptPath, scriptContent);

            // Make the script executable
            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{scriptPath}\"",
                UseShellExecute = false
            });
            chmod?.WaitForExit();

            // Run the script
            using var process = new Process();
            process.StartInfo.FileName = shellExecutable;
            process.StartInfo.Arguments = $"\"{scriptPath}\"";
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
            var outputLines = scriptOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            outputLines.Should().HaveCountGreaterThanOrEqualTo(3, $"Should have dotnet version, PATH, and DOTNET_ROOT output for {shell}");

            // First line should be dotnet version
            var dotnetVersion = outputLines[0].Trim();
            dotnetVersion.Should().NotBeNullOrEmpty($"dotnet --version should produce output for {shell}");

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

            // Verify PATH starts with the install path
            var pathValue = pathLine!.Substring("PATH=".Length);
            var firstPathEntry = pathValue.Split(':')[0];
            firstPathEntry.Should().Be(installPath, $"First PATH entry should be the dotnet install path for {shell}");

            // Verify DOTNET_ROOT matches install path
            var dotnetRootValue = dotnetRootLine!.Substring("DOTNET_ROOT=".Length);
            dotnetRootValue.Should().Be(installPath, $"DOTNET_ROOT should be set to the install path for {shell}");
        }
        else if (shell == "pwsh")
        {
            // Create a PowerShell script that dot-sources the env script and prints environment info
            string scriptPath = Path.Combine(tempRoot, "test-env.ps1");
            string scriptContent = $@"
$ErrorActionPreference = 'Stop'
. (""{dotnetupPath}"" print-env-script --shell pwsh --dotnet-install-path ""{installPath}"")
dotnet --version
Write-Output ""PATH=$env:PATH""
Write-Output ""DOTNET_ROOT=$env:DOTNET_ROOT""
";
            File.WriteAllText(scriptPath, scriptContent);

            // Run the PowerShell script
            using var process = new Process();
            process.StartInfo.FileName = "pwsh";
            process.StartInfo.Arguments = $"-File \"{scriptPath}\"";
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
            process.ExitCode.Should().Be(0, $"PowerShell script execution failed. Output:\n{scriptOutput}\nError:\n{scriptError}");

            // Parse the output lines
            var outputLines = scriptOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            outputLines.Should().HaveCountGreaterThanOrEqualTo(3, "Should have dotnet version, PATH, and DOTNET_ROOT output for pwsh");

            // First line should be dotnet version
            var dotnetVersion = outputLines[0].Trim();
            dotnetVersion.Should().NotBeNullOrEmpty("dotnet --version should produce output for pwsh");

            if (expectedVersion != null)
            {
                dotnetVersion.Should().Be(expectedVersion, "dotnet version should match expected version for pwsh");
            }
            else
            {
                dotnetVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+", "dotnet version should be in format x.y.z for pwsh");
            }

            // Find PATH and DOTNET_ROOT lines
            var pathLine = outputLines.FirstOrDefault(l => l.StartsWith("PATH="));
            var dotnetRootLine = outputLines.FirstOrDefault(l => l.StartsWith("DOTNET_ROOT="));

            pathLine.Should().NotBeNull("PATH should be printed for pwsh");
            dotnetRootLine.Should().NotBeNull("DOTNET_ROOT should be printed for pwsh");

            // Verify PATH starts with the install path (on Windows, use semicolon separator)
            var pathValue = pathLine!.Substring("PATH=".Length);
            var pathSeparator = OperatingSystem.IsWindows() ? ';' : ':';
            var firstPathEntry = pathValue.Split(pathSeparator)[0];
            firstPathEntry.Should().Be(installPath, "First PATH entry should be the dotnet install path for pwsh");

            // Verify DOTNET_ROOT matches install path
            var dotnetRootValue = dotnetRootLine!.Substring("DOTNET_ROOT=".Length);
            dotnetRootValue.Should().Be(installPath, "DOTNET_ROOT should be set to the install path for pwsh");
        }
    }

    /// <summary>
    /// Runs the dotnetup executable as a separate process
    /// </summary>
    /// <param name="args">Command line arguments for dotnetup</param>
    /// <param name="captureOutput">Whether to capture and return the output</param>
    /// <returns>A tuple with exit code and captured output (if requested)</returns>
    public static (int exitCode, string output) RunDotnetupProcess(string[] args, bool captureOutput = false, string? workingDirectory = null)
    {
        string dotnetupPath = GetDotnetupExecutablePath();

        using var process = new Process();
        process.StartInfo.FileName = dotnetupPath;
        process.StartInfo.Arguments = string.Join(" ", args.Select(a => $"\"{a}\""));
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = captureOutput;
        process.StartInfo.RedirectStandardError = captureOutput;
        process.StartInfo.WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;

        StringBuilder outputBuilder = new();
        if (captureOutput)
        {
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
                    outputBuilder.AppendLine(e.Data);
                }
            };
        }

        process.Start();

        if (captureOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        process.WaitForExit();
        return (process.ExitCode, outputBuilder.ToString());
    }

    private static string GetRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory != null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "sdk.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Unable to locate repository root from base directory '{AppContext.BaseDirectory}'.");
    }

}
