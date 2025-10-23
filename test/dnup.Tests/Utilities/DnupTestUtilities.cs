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
using Microsoft.Dotnet.Installation.Internal;
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
            channel,
            "--install-path",
            installPath
        };

        if (!ShouldForceDebug())
        {
            args.Add("--interactive");
            args.Add("false");
        }

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
    /// Runs the dnup executable as a separate process.
    /// </summary>
    /// <param name="args">Command line arguments for dnup.</param>
    /// <param name="captureOutput">Whether to capture and return the output.</param>
    /// <returns>Process result including exit code and output (if captured).</returns>
    public static DnupProcessResult RunDnupProcess(string[] args, bool captureOutput = false, string? workingDirectory = null)
    {
        if (ShouldForceDebug())
        {
            args = EnsureDebugFlag(args);
        }

        string repoRoot = GetRepositoryRoot();
        string dnupPath = LocateDnupAssembly(repoRoot);

        using var process = new Process();
        string repoDotnet = Path.Combine(repoRoot, ".dotnet", DnupUtilities.GetDotnetExeName());
        process.StartInfo.FileName = File.Exists(repoDotnet) ? repoDotnet : DnupUtilities.GetDotnetExeName();
        process.StartInfo.Arguments = $"\"{dnupPath}\" {string.Join(" ", args.Select(a => $"\"{a}\""))}";

        bool useShellExecute = ShouldForceDebug();
        process.StartInfo.UseShellExecute = useShellExecute;
        process.StartInfo.CreateNoWindow = !useShellExecute;
        process.StartInfo.WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;

        bool shouldCaptureOutput = captureOutput && !useShellExecute;

        StringBuilder outputBuilder = new();
        if (shouldCaptureOutput)
        {
            process.StartInfo.RedirectStandardOutput = shouldCaptureOutput;
            process.StartInfo.RedirectStandardError = shouldCaptureOutput;
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };
        }

        process.Start();

        if (shouldCaptureOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (ShouldForceDebug())
        {
            Console.WriteLine($"Started dnup process with PID: {process.Id}");
            Console.WriteLine(useShellExecute
                ? "Interactive console window launched for debugger attachment."
                : "Process is sharing the current console for debugger attachment.");
            Console.WriteLine("To attach debugger: Debug -> Attach to Process -> Select the dotnet.exe process");
        }

        process.WaitForExit();

        string output = shouldCaptureOutput ? outputBuilder.ToString() : string.Empty;
        return new DnupProcessResult(process.ExitCode, output, shouldCaptureOutput);
    }

    private static string[] EnsureDebugFlag(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase)))
        {
            return args;
        }

        string[] updated = new string[args.Length + 1];
        updated[0] = "--debug";
        Array.Copy(args, 0, updated, 1, args.Length);
        return updated;
    }

    private static bool ShouldForceDebug()
    {
        string? value = Environment.GetEnvironmentVariable("DNUP_TEST_DEBUG");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes output assertions only when dnup output was captured.
    /// </summary>
    public static void AssertOutput(DnupProcessResult result, Action<string> assertion)
    {
        if (!result.OutputCaptured)
        {
            Console.WriteLine("Skipping output assertions because dnup output was not captured (debug mode with ShellExecute).");
            return;
        }

        assertion(result.Output);
    }

    /// <summary>
    /// Formats dnup output for inclusion in assertion messages.
    /// </summary>
    public static string FormatOutputForAssertions(DnupProcessResult result) =>
        result.OutputCaptured ? result.Output : "[dnup output not captured; run without --debug to capture output]";

    private static string GetRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
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

        if (fallback is not null)
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

        if (!candidates.Contains("Release", StringComparer.OrdinalIgnoreCase))
        {
            candidates.Insert(0, "Release");
        }

        if (!candidates.Contains("Debug", StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add("Debug");
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps System.Runtime.InteropServices.Architecture to Microsoft.Dotnet.Installation.InstallArchitecture
    /// </summary>
    public static InstallArchitecture MapArchitecture(Architecture architecture) =>
        InstallerUtilities.GetInstallArchitecture(architecture);
}

internal readonly record struct DnupProcessResult(int ExitCode, string Output, bool OutputCaptured)
{
    public void Deconstruct(out int exitCode, out string output)
    {
        exitCode = ExitCode;
        output = Output;
    }

    public void Deconstruct(out int exitCode, out string output, out bool outputCaptured)
    {
        exitCode = ExitCode;
        output = Output;
        outputCaptured = OutputCaptured;
    }
}
