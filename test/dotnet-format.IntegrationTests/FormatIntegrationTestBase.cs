// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Base class for dotnet-format integration tests. Each derived class targets a specific
/// repository and gets its own clone+restore lifecycle via <see cref="IAsyncLifetime"/>.
/// Two test methods (workspace format and folder format) are inherited automatically.
/// </summary>
public abstract class FormatIntegrationTestBase : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private string? _repoPath;

    /// <summary>The full GitHub URL of the repository (e.g. "https://github.com/dotnet/sdk").</summary>
    protected abstract string RepoUrl { get; }

    /// <summary>The commit SHA to test against.</summary>
    protected abstract string Sha { get; }

    /// <summary>The solution/slnf/slnx file name to format (e.g. "sdk.slnx").</summary>
    protected abstract string TargetSolution { get; }

    /// <summary>Short display name for the repo (e.g. "sdk"). Used for filtering and logging.</summary>
    protected abstract string RepoName { get; }

    /// <summary>
    /// Test priority. Lower values indicate higher importance and run in more contexts.
    /// P0 tests run in all builds (including PR validation).
    /// P1 tests run only in CI (rolling/nightly builds).
    /// Override in derived classes to elevate a repo to PR validation.
    /// </summary>
    protected virtual int Priority => 1;

    /// <summary>
    /// When true (default), restore uses the repo's own build script (eng/build.sh or eng/Build.ps1)
    /// which installs the correct SDK version and performs an Arcade-level restore, then uses the
    /// repo-local dotnet to run "dotnet restore" on the solution.
    /// When false, restore uses the SDK under test's "dotnet restore" directly with
    /// the repo's nuget.config.
    /// Override to false for repos that don't use Arcade (e.g. project-system).
    /// </summary>
    protected virtual bool UseRepoBuildScript => true;

    /// <summary>Path to the SDK under test's dotnet host.</summary>
    private string ParentDotNetPath => SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

    protected FormatIntegrationTestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    public ValueTask InitializeAsync()
    {
        SkipIfExcluded();

        _repoPath = Path.Combine(Path.GetTempPath(), "dotnet-format-tests", RepoName);

        // Write to Console directly so Helix captures progress even if the test host crashes.
        Console.WriteLine($"[{RepoName}] InitializeAsync starting — {_repoPath}");
        Console.Out.Flush();

        try
        {
            LogEnvironmentInfo();

            if (IsAlreadyAtCorrectSha())
            {
                _output.WriteLine($"Reusing existing clone at {Sha}");
            }
            else
            {
                if (Directory.Exists(_repoPath))
                {
                    Directory.Delete(_repoPath, recursive: true);
                }

                Directory.CreateDirectory(_repoPath);

                CloneRepo();
                Restore();
            }

            RemoveGlobalJson();
            Console.WriteLine($"[{RepoName}] InitializeAsync completed successfully.");
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{RepoName}] InitializeAsync FAILED: {ex.Message}");
            Console.Out.Flush();
            _output.WriteLine($"[FATAL] InitializeAsync failed for {RepoName}: {ex}");
            LogDiskSpace();
            throw;
        }

        return default;
    }

    private void LogEnvironmentInfo()
    {
        _output.WriteLine($"[Diagnostics] Repo: {RepoName}, SHA: {Sha}");
        _output.WriteLine($"[Diagnostics] RepoPath: {_repoPath}");
        _output.WriteLine($"[Diagnostics] TempPath: {Path.GetTempPath()}");
        LogDiskSpace();
    }

    private void LogDiskSpace()
    {
        try
        {
            var root = Path.GetPathRoot(_repoPath!) ?? _repoPath!;
            var drive = new DriveInfo(root);
            _output.WriteLine($"[Diagnostics] Disk: {drive.Name} — " +
                $"Available: {drive.AvailableFreeSpace / (1024 * 1024)}MB, " +
                $"Total: {drive.TotalSize / (1024 * 1024)}MB");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[Diagnostics] Could not read disk space: {ex.Message}");
        }
    }

    private bool IsAlreadyAtCorrectSha()
    {
        if (!Directory.Exists(_repoPath))
        {
            return false;
        }

        var result = RunProcess("git", "rev-parse HEAD", throwOnError: false);
        return result.ExitCode == 0 &&
               string.Equals(result.StdOut.Trim(), Sha, StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask DisposeAsync()
    {
        // Clone is cleaned up at the start of the next run in InitializeAsync.
        return default;
    }

    [Fact]
    public void FormatWorkspace()
    {
        SkipIfExcluded();

        var solutionPath = FindSolution();
        _output.WriteLine($"Formatting workspace: {solutionPath}");

        var result = new DotnetCommand(_output, "format", solutionPath, "--no-restore", "--verify-no-changes", "-v", "diag")
            .WithWorkingDirectory(_repoPath!)
            .Execute();

        // Exit code 0 = no changes needed, 2 = format differences found (expected for external repos).
        // Any other exit code is an actual failure.
        Assert.True(result.ExitCode == 0 || result.ExitCode == 2,
            $"dotnet format exited with unexpected code {result.ExitCode}.\n{result.StdErr}");
    }

    [Fact]
    public void FormatFolder()
    {
        SkipIfExcluded();

        _output.WriteLine($"Formatting folder: {_repoPath}");

        var result = new DotnetCommand(_output, "format", "whitespace", _repoPath!, "--folder", "--verify-no-changes", "-v", "diag")
            .WithWorkingDirectory(_repoPath!)
            .Execute();

        Assert.True(result.ExitCode == 0 || result.ExitCode == 2,
            $"dotnet format exited with unexpected code {result.ExitCode}.\n{result.StdErr}");
    }

    /// <summary>
    /// Skips the test if it should not run in the current environment.
    /// Rules:
    ///   - In Helix (HELIX_WORKITEM_UPLOAD_ROOT is set):
    ///       - Only run on Linux.
    ///       - If DOTNET_SDK_TEST_MAX_PRIORITY is set, only run tests at or below that priority.
    ///   - Locally: always run (all repos, all platforms).
    /// </summary>
    private void SkipIfExcluded()
    {
        var isHelix = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT"));

        if (isHelix)
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Skip("Format integration tests only run on Linux in CI.");
            }

            var maxPriorityValue = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MAX_PRIORITY");
            if (int.TryParse(maxPriorityValue, out var maxPriority) && Priority > maxPriority)
            {
                Assert.Skip($"Skipped: test priority {Priority} exceeds max priority {maxPriority} for this run.");
            }
        }
    }

    private void CloneRepo()
    {
        _output.WriteLine($"[Stage: Clone] Cloning {RepoUrl} at {Sha}...");
        var sw = Stopwatch.StartNew();

        RunProcess("git", "init");
        RunProcess("git", $"remote add origin {RepoUrl}");
        RunProcess("git", $"fetch --progress --no-tags --depth=1 origin {Sha}");
        RunProcess("git", $"checkout {Sha}");

        sw.Stop();
        _output.WriteLine($"[Stage: Clone] Completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
        LogDiskSpace();
    }

    /// <summary>
    /// Removes the repo's global.json so that the parent SDK (the SDK under test) is
    /// used for "dotnet format" rather than the repo's pinned SDK version.
    /// This must be called after restore (which needs global.json to install the right SDK).
    /// </summary>
    private void RemoveGlobalJson()
    {
        var globalJson = Path.Combine(_repoPath!, "global.json");
        if (File.Exists(globalJson))
        {
            File.Delete(globalJson);
            _output.WriteLine("Removed global.json to use parent SDK for formatting.");
        }
    }

    private void Restore()
    {
        _output.WriteLine($"[Stage: Restore] Starting restore (UseRepoBuildScript={UseRepoBuildScript})...");
        var sw = Stopwatch.StartNew();

        if (UseRepoBuildScript)
        {
            RunBuildScriptRestore();
        }
        else
        {
            var solutionPath = FindSolution();
            RestoreWithParentSdk(solutionPath);
        }

        sw.Stop();
        _output.WriteLine($"[Stage: Restore] Completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
        LogDiskSpace();
    }

    /// <summary>
    /// Runs the repo's Arcade build script (eng/build.sh or eng/Build.ps1) with --restore.
    /// This installs the correct SDK version from global.json and performs an Arcade-level
    /// restore. Skipped silently if no build script is found.
    /// The process runs with a clean SDK environment to avoid conflicts with the parent SDK.
    /// Restore failures are logged but not fatal — FormatFolder tests don't require restore,
    /// and FormatWorkspace will fail at format time with a clear error if restore is incomplete.
    /// </summary>
    private void RunBuildScriptRestore()
    {
        string scriptPath;
        string fileName;
        string arguments;

        if (OperatingSystem.IsWindows())
        {
            var engBuild = Path.Combine(_repoPath!, "eng", "Build.ps1");
            var commonBuild = Path.Combine(_repoPath!, "eng", "common", "Build.ps1");

            scriptPath = File.Exists(engBuild) ? engBuild : commonBuild;
            fileName = "powershell";
            arguments = $"-ExecutionPolicy ByPass -NoProfile -File \"{scriptPath}\" -restore";
        }
        else
        {
            var engBuild = Path.Combine(_repoPath!, "eng", "build.sh");
            var commonBuild = Path.Combine(_repoPath!, "eng", "common", "build.sh");

            scriptPath = File.Exists(engBuild) ? engBuild : commonBuild;
            fileName = "bash";
            arguments = $"{scriptPath} --restore";
        }

        if (!File.Exists(scriptPath))
        {
            _output.WriteLine($"[Stage: Restore] No build script found, skipping Arcade restore.");
            return;
        }

        _output.WriteLine($"[Stage: Restore] Running build script: {scriptPath}");
        var result = RunProcess(fileName, arguments, cleanSdkEnvironment: true, throwOnError: false);
        _output.WriteLine($"[Stage: Restore] Build script exited with code {result.ExitCode}");
    }

    /// <summary>
    /// Restores the solution using the SDK under test's dotnet with the repo's nuget.config.
    /// Used for repos that don't use Arcade infrastructure (e.g. project-system).
    /// This matches the original pipeline's "useParentSdk" behavior.
    /// Failures are logged but not fatal — some feeds may require authentication that
    /// is unavailable in public CI environments.
    /// </summary>
    private void RestoreWithParentSdk(string solutionPath)
    {
        var nugetConfig = Path.Combine(_repoPath!, "nuget.config");
        _output.WriteLine($"Restoring {Path.GetFileName(solutionPath)} with parent SDK: {ParentDotNetPath}");

        var args = File.Exists(nugetConfig)
            ? $"restore \"{solutionPath}\" --configfile \"{nugetConfig}\""
            : $"restore \"{solutionPath}\"";

        RunProcess(ParentDotNetPath, args, throwOnError: false);
    }

    /// <summary>Maximum time any single subprocess is allowed to run before being killed.</summary>
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(30);

    private (int ExitCode, string StdOut, string StdErr) RunProcess(string fileName, string arguments, bool cleanSdkEnvironment = false, bool throwOnError = true)
    {
        // When isolating from the parent SDK, use 'env -i' on Unix to start with
        // a minimal environment. This prevents any inherited DOTNET_*, MSBUILD_*, or
        // PATH entries from causing SDK resolver conflicts between the parent SDK
        // and the repo's own SDK.
        if (cleanSdkEnvironment && !OperatingSystem.IsWindows())
        {
            var envArgs = $"-i HOME={Environment.GetEnvironmentVariable("HOME")} " +
                          $"PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin " +
                          $"DOTNET_MULTILEVEL_LOOKUP=0 " +
                          $"{fileName} {arguments}";
            fileName = "env";
            arguments = envArgs;
        }

        _output.WriteLine($"[RunProcess] {fileName} {arguments}");

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = _repoPath!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;

        // Read stdout and stderr asynchronously to avoid deadlocks when the process
        // writes more than the OS pipe buffer to both streams simultaneously.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            _output.WriteLine($"[RunProcess] TIMEOUT after {ProcessTimeout.TotalMinutes} minutes — killing process tree.");
            process.Kill(entireProcessTree: true);
            var partialOut = stdoutTask.GetAwaiter().GetResult();
            var partialErr = stderrTask.GetAwaiter().GetResult();
            _output.WriteLine(partialOut);
            _output.WriteLine(partialErr);
            throw new TimeoutException(
                $"Command '{fileName} {arguments}' exceeded {ProcessTimeout.TotalMinutes} minute timeout.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            _output.WriteLine(stdout);
            _output.WriteLine(stderr);

            if (throwOnError)
            {
                throw new InvalidOperationException(
                    $"Command '{fileName} {arguments}' exited with code {process.ExitCode}");
            }

            _output.WriteLine($"[RunProcess] Warning: exited with code {process.ExitCode} (non-fatal).");
        }

        return (process.ExitCode, stdout, stderr);
    }

    private string FindSolution()
    {
        var matches = Directory.GetFiles(_repoPath!, TargetSolution, SearchOption.AllDirectories);
        Assert.True(matches.Length > 0,
            $"Target solution '{TargetSolution}' not found under {_repoPath}");

        return matches[0];
    }
}
