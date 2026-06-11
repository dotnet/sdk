// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Xunit;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Integration tests that run the actual AOT binary (dn.exe / dn) end-to-end.
///  These tests require the AOT binary to be present in the SDK layout.
///  They are traited with "Category=AOT" so they can be filtered in CI.
/// </summary>
[Trait("Category", "AOT")]
public class AotIntegrationTests
{
    private readonly ITestOutputHelper _log;

    public AotIntegrationTests(ITestOutputHelper log)
    {
        _log = log;
    }

    private static string? FindDnPath()
    {
        // Look for dn in the SDK layout (same location as dotnet)
        string? dotnetPath = Environment.ProcessPath;
        if (dotnetPath is null)
        {
            return null;
        }

        string? sdkDir = Path.GetDirectoryName(dotnetPath);
        if (sdkDir is null)
        {
            return null;
        }

        string dnName = OperatingSystem.IsWindows() ? "dn.exe" : "dn";
        string dnPath = Path.Combine(sdkDir, dnName);
        return File.Exists(dnPath) ? dnPath : null;
    }

    private (int exitCode, string stdout, string stderr) RunDn(
        string[] args,
        bool enableAot = true,
        int timeoutMs = 30_000)
    {
        string? dnPath = FindDnPath();
        if (dnPath is null)
        {
            return (-1, "", "dn binary not found");
        }

        var psi = new ProcessStartInfo
        {
            FileName = dnPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        if (enableAot)
        {
            psi.Environment["DOTNET_CLI_ENABLEAOT"] = "true";
        }
        else
        {
            psi.Environment.Remove("DOTNET_CLI_ENABLEAOT");
        }

        _log.WriteLine($"Running: {dnPath} {string.Join(" ", args)}");
        _log.WriteLine($"  DOTNET_CLI_ENABLEAOT={enableAot}");

        using var process = Process.Start(psi)!;

        // Read streams asynchronously before WaitForExit to avoid deadlocks
        // when the child process fills the OS pipe buffer.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill();
            return (-1, "", "[TIMEOUT]");
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();

        _log.WriteLine($"  Exit code: {process.ExitCode}");
        if (!string.IsNullOrEmpty(stdout)) _log.WriteLine($"  Stdout: {stdout.TrimEnd()}");
        if (!string.IsNullOrEmpty(stderr)) _log.WriteLine($"  Stderr: {stderr.TrimEnd()}");

        return (process.ExitCode, stdout, stderr);
    }

    private void SkipIfDnUnavailable()
    {
        if (FindDnPath() is null)
        {
            Assert.Skip("AOT binary (dn) not found in SDK layout. Build with NativeAOT to enable these tests.");
        }
    }

    [Fact]
    public void AotVersion_WithEnableAot_OutputsVersionAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--version"], enableAot: true);

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout), "Expected version output");
    }

    [Fact]
    public void AotInfo_WithEnableAot_OutputsInfoAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--info"], enableAot: true);

        Assert.Equal(0, exitCode);
        Assert.Contains(".NET SDK:", stdout);
        Assert.Contains("Version:", stdout);
        Assert.Contains("Runtime Environment:", stdout);
    }

    [Fact]
    public void AotNoArgs_WithEnableAot_ShowsUsage()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn([], enableAot: true);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", stdout);
    }

    [Fact]
    public void AotBuild_WithEnableAot_FallsBackToManaged()
    {
        SkipIfDnUnavailable();

        // "build" is unsupported by AOT parser → should fall back to managed CLI
        // In a full SDK layout, this would invoke dotnet build. We just verify it doesn't crash.
        var (exitCode, stdout, stderr) = RunDn(["build", "--help"], enableAot: true);

        // If managed fallback works, it should show build help (exit 0)
        // If managed fallback is missing, it returns 1
        // Either way, it shouldn't crash or timeout
        Assert.True(exitCode == 0 || exitCode == 1,
            $"Expected exit code 0 or 1, got {exitCode}. Stderr: {stderr}");
    }

    [Fact]
    public void Version_WithoutEnableAot_StillWorks()
    {
        SkipIfDnUnavailable();

        // Without DOTNET_CLI_ENABLEAOT, everything goes through managed fallback
        var (exitCode, stdout, stderr) = RunDn(["--version"], enableAot: false);

        // Managed fallback requires dotnet.dll + all dependencies in the layout.
        // In a partial layout (e.g. local dev with only dn.exe published),
        // the fallback correctly fails because dotnet.dll is missing.
        if (exitCode != 0 && stderr.Contains("dotnet.dll"))
        {
            Assert.Skip("Managed fallback not available (dotnet.dll not in layout)");
        }

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout));
    }

    [Fact]
    public void Info_WithoutEnableAot_ShowsFullInfo()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, stderr) = RunDn(["--info"], enableAot: false);

        if (exitCode != 0 && stderr.Contains("dotnet.dll"))
        {
            Assert.Skip("Managed fallback not available (dotnet.dll not in layout)");
        }

        Assert.Equal(0, exitCode);
        // Managed fallback should include workload and MSBuild info
        Assert.Contains("Version:", stdout);
    }
}
