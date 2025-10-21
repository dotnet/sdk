// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;

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
    public static (int exitCode, string output) RunDnupProcess(string[] args, bool captureOutput = false, string? workingDirectory = null)
    {
        string repoRoot = GetRepositoryRoot();
        string dnupPath = LocateDnupAssembly(repoRoot);

        using var process = new Process();
        string repoDotnet = Path.Combine(repoRoot, ".dotnet", DnupUtilities.GetDotnetExeName());
        process.StartInfo.FileName = File.Exists(repoDotnet) ? repoDotnet : DnupUtilities.GetDotnetExeName();
        process.StartInfo.Arguments = $"\"{dnupPath}\" {string.Join(" ", args.Select(a => $"\"{a}\""))}";
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

    public static bool ValidateInstall(DotnetInstall install)
    {
        var validator = new ArchiveInstallationValidator();
        return validator.Validate(install);
    }

    private static string LocateDnupAssembly(string repoRoot)
    {
        string artifactsRoot = Path.Combine(repoRoot, "artifacts", "bin", "dnup");
        if (!Directory.Exists(artifactsRoot))
        {
            throw new FileNotFoundException($"dnup build output not found. Expected directory: {artifactsRoot}");
        }

        var testAssemblyDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? tfm = testAssemblyDirectory.Name;
        string? configuration = testAssemblyDirectory.Parent?.Name;

        if (!string.IsNullOrEmpty(tfm))
        {
            IEnumerable<string> configurationCandidates = BuildConfigurationCandidates(configuration);
            foreach (string candidateConfig in configurationCandidates)
            {
                string candidate = Path.Combine(artifactsRoot, candidateConfig, tfm, "dnup.dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        string? fallback = Directory.EnumerateFiles(artifactsRoot, "dnup.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (fallback != null)
        {
            return fallback;
        }

        throw new FileNotFoundException($"dnup executable not found under {artifactsRoot}. Ensure the dnup project is built before running tests.");
    }

    private static IEnumerable<string> BuildConfigurationCandidates(string? configuration)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(configuration))
        {
            candidates.Add(configuration);
        }

        candidates.Add("Debug");
        candidates.Add("Release");

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps System.Runtime.InteropServices.Architecture to Microsoft.Dotnet.Installation.InstallArchitecture
    /// </summary>
    public static InstallArchitecture MapArchitecture(Architecture architecture) =>
        Microsoft.DotNet.Tools.Bootstrapper.DnupUtilities.GetInstallArchitecture(architecture);
}
