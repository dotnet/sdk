// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Integration tests that run the actual AOT binary (dn.exe / dn) end-to-end.
///  These tests require the AOT binary to be present in the SDK layout.
///  They are categorized with <c>[TestCategory("AOT")]</c> so they can be filtered in CI
///  (e.g. by the <c>AOT</c> test category).
/// </summary>
[TestCategory("AOT")]
[TestClass]
public class AotIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private ITestOutputHelper? _logBacking;
    private ITestOutputHelper _log => _logBacking ??= new TestContextOutputHelper(TestContext);

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
        int timeoutMs = 30_000,
        Dictionary<string, string>? extraEnv = null)
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

        if (extraEnv is not null)
        {
            foreach (KeyValuePair<string, string> entry in extraEnv)
            {
                psi.Environment[entry.Key] = entry.Value;
            }
        }

        _log.WriteLine($"Running: {dnPath} {string.Join(" ", args)}");
        _log.WriteLine($"  DOTNET_CLI_ENABLEAOT={enableAot}");

        using var process = Process.Start(psi)!;

        // Read streams asynchronously before WaitForExit to avoid deadlocks
        // when the child process fills the OS pipe buffer.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);

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
            Assert.Inconclusive("AOT binary (dn) not found in SDK layout. Build with NativeAOT to enable these tests.");
        }
    }

    [TestMethod]
    public void AotVersion_WithEnableAot_OutputsVersionAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--version"], enableAot: true);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "Expected version output");
    }

    [TestMethod]
    public void AotInfo_SeparatedLayout_BasePathIsResolvedSdkDirectory()
    {
        SkipIfDnUnavailable();
        RunSeparatedLayoutBasePathTest(selfLocate: false);
    }

    [TestMethod]
    public void AotInfo_SeparatedLayout_SelfLocate_BasePathIsResolvedSdkDirectory()
    {
        SkipIfDnUnavailable();
        RunSeparatedLayoutBasePathTest(selfLocate: true);
    }

    // Emulates the deployed muxer layout: dotnet-aot lives in a directory other than dn's own, so
    // AppContext.BaseDirectory is no longer the SDK directory. Verifies that --info's Base Path still
    // reports the resolved SDK directory - whether it was passed in as sdk_dir (selfLocate: false) or
    // self-located from the loaded module (selfLocate: true).
    private void RunSeparatedLayoutBasePathTest(bool selfLocate)
    {
        string dnPath = FindDnPath()!;
        string sdkLayoutDir = Path.GetDirectoryName(dnPath)!;
        string aotLib = OperatingSystem.IsWindows() ? "dotnet-aot.dll"
            : OperatingSystem.IsMacOS() ? "dotnet-aot.dylib"
            : "dotnet-aot.so";
        string aotSource = Path.Combine(sdkLayoutDir, aotLib);
        if (!File.Exists(aotSource))
        {
            Assert.Inconclusive($"{aotLib} not found next to dn; build with NativeAOT to enable this test.");
        }

        string sdkSubDir = Path.Combine(Path.GetTempPath(), "aot-sep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sdkSubDir);
        try
        {
            File.Copy(aotSource, Path.Combine(sdkSubDir, aotLib));

            var env = new Dictionary<string, string> { ["DOTNET_AOT_SDK_DIR"] = sdkSubDir };
            if (selfLocate)
            {
                env["DOTNET_AOT_BLANK_SDKDIR"] = "1";
            }

            var (exitCode, stdout, _) = RunDn(["--info"], enableAot: true, extraEnv: env);

            Assert.AreEqual(0, exitCode);

            bool basePathReferencesSdkDir = false;
            foreach (string line in stdout.Split('\n'))
            {
                if (line.Contains("Base Path:") && line.Contains(sdkSubDir))
                {
                    basePathReferencesSdkDir = true;
                    break;
                }
            }

            Assert.IsTrue(basePathReferencesSdkDir,
                $"--info Base Path did not reference the resolved SDK directory '{sdkSubDir}'. Output:\n{stdout}");
        }
        finally
        {
            Directory.Delete(sdkSubDir, recursive: true);
        }
    }

    [TestMethod]
    public void AotInfo_WithEnableAot_OutputsInfoAndExitsZero()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn(["--info"], enableAot: true);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain(".NET SDK:");
        stdout.Should().Contain("Version:");
        stdout.Should().Contain("Workload version:");
        stdout.Should().Contain("MSBuild version:");
        stdout.Should().Contain("Runtime Environment:");
    }

    [TestMethod]
    public void AotNoArgs_WithEnableAot_ShowsUsage()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, _) = RunDn([], enableAot: true);

        Assert.AreEqual(0, exitCode);
        stdout.Should().Contain("Usage:");
    }

    [TestMethod]
    public void AotBuild_WithEnableAot_FallsBackToManaged()
    {
        SkipIfDnUnavailable();

        // "build" is unsupported by AOT parser → should fall back to managed CLI
        // In a full SDK layout, this would invoke dotnet build. We just verify it doesn't crash.
        var (exitCode, stdout, stderr) = RunDn(["build", "--help"], enableAot: true);

        // If managed fallback works, it should show build help (exit 0)
        // If managed fallback is missing, it returns 1
        // Either way, it shouldn't crash or timeout
        Assert.IsTrue(exitCode == 0 || exitCode == 1,
            $"Expected exit code 0 or 1, got {exitCode}. Stderr: {stderr}");
    }

    [TestMethod]
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
            Assert.Inconclusive("Managed fallback not available (dotnet.dll not in layout)");
        }

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout));
    }

    [TestMethod]
    public void Info_WithoutEnableAot_ShowsFullInfo()
    {
        SkipIfDnUnavailable();

        var (exitCode, stdout, stderr) = RunDn(["--info"], enableAot: false);

        if (exitCode != 0 && stderr.Contains("dotnet.dll"))
        {
            Assert.Inconclusive("Managed fallback not available (dotnet.dll not in layout)");
        }

        Assert.AreEqual(0, exitCode);
        // Managed fallback should include workload and MSBuild info
        stdout.Should().Contain("Version:");
    }
}
