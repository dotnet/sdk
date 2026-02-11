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
