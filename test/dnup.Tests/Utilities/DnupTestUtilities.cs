// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Dnup.Tests.Utilities;

/// <summary>
/// Common utilities for dnup tests
/// </summary>
internal static class DnupTestUtilities
{
    /// <summary>
    /// Creates a test environment with proper temporary directories
    /// </summary>
    public static TestEnvironment CreateTestEnvironment()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "dnup-e2e", Guid.NewGuid().ToString("N"));
        string installPath = Path.Combine(tempRoot, "dotnet-root");
        string manifestPath = Path.Combine(tempRoot, "dnup_manifest.json");

        // Create necessary directories
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        return new TestEnvironment(tempRoot, installPath, manifestPath);
    }

    /// <summary>
    /// Builds command line arguments for dnup
    /// </summary>
    public static string[] BuildArguments(string channel, string installPath, string? manifestPath = null, bool disableProgress = true)
    {
        var args = new List<string>
        {
            "sdk",
            "install",
            channel
        };

        args.Add("--install-path");
        args.Add(installPath);
        args.Add("--interactive");
        args.Add("false");

        // Add manifest path option if specified for test isolation
        if (!string.IsNullOrEmpty(manifestPath))
        {
            args.Add("--manifest-path");
            args.Add(manifestPath);
        }

        // Add no-progress option when running tests in parallel to avoid Spectre.Console exclusivity issues
        if (disableProgress)
        {
            args.Add("--no-progress");
        }

        return [.. args];
    }

    /// <summary>
    /// Runs the dnup executable as a separate process
    /// </summary>
    /// <param name="args">Command line arguments for dnup</param>
    /// <param name="captureOutput">Whether to capture and return the output</param>
    /// <returns>A tuple with exit code and captured output (if requested)</returns>
    public static (int exitCode, string output) RunDnupProcess(string[] args, bool captureOutput = false)
    {
        string dnupPath = Path.Combine(
            AppContext.BaseDirectory, // Test assembly directory
            "..", "..", "..", "..", "..", // Navigate up to artifacts directory
            "artifacts", "bin", "dnup", "Debug", "net10.0", "dnup.dll");
        
        // Ensure path is normalized and exists
        dnupPath = Path.GetFullPath(dnupPath);
        if (!File.Exists(dnupPath))
        {
            throw new FileNotFoundException($"dnup executable not found at: {dnupPath}");
        }

        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = $"\"{dnupPath}\" {string.Join(" ", args.Select(a => $"\"{a}\""))}";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = captureOutput;
        process.StartInfo.RedirectStandardError = captureOutput;

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

    /// <summary>
    /// Maps System.Runtime.InteropServices.Architecture to Microsoft.Dotnet.Installation.InstallArchitecture
    /// </summary>
    public static InstallArchitecture MapArchitecture(Architecture architecture) =>
        Microsoft.DotNet.Tools.Bootstrapper.DnupUtilities.GetInstallArchitecture(architecture);
}
