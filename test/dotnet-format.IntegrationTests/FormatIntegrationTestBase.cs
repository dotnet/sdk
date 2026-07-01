// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Base class for dotnet-format integration tests. Each derived class targets a specific
/// repository and gets its own clone+restore lifecycle via <see cref="TestInitializeAttribute"/>.
/// Two test methods (workspace format and folder format) are inherited automatically.
/// </summary>
public abstract class FormatIntegrationTestBase
{
    private ITestOutputHelper _output = null!;
    private string? _repoPath;

    public virtual TestContext TestContext { get; set; } = null!;

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

    /// <summary>Path to the SDK under test's dotnet host.</summary>
    private string ParentDotNetPath => SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

    [TestInitialize]
    public void TestInitialize()
    {
        _output = new TestContextOutputHelper(TestContext);

        SkipIfExcluded();

        _repoPath = Path.Combine(Path.GetTempPath(), "dotnet-format-tests", RepoName);

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
        }

        // Remove global.json so that the SDK under test is used for restore and format
        // rather than the repo's pinned SDK version.
        RemoveGlobalJson();

        Restore();
    }

    private bool IsAlreadyAtCorrectSha()
    {
        if (!Directory.Exists(_repoPath))
        {
            return false;
        }

        var result = RunProcess("git", "rev-parse HEAD", captureOutput: true, ignoreThrowingOnError: true);
        return result.ExitCode == 0 &&
               string.Equals(result.StdOut.Trim(), Sha, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void FormatWorkspace()
    {
        SkipIfExcluded();

        var solutionPath = FindSolution();
        _output.WriteLine($"Formatting workspace: {solutionPath}");

        // Capture a binlog for diagnostic purposes.
        var binlogPath = GetBinlogPath();

        var result = new DotnetCommand(_output, "format", solutionPath, "--no-restore", "--verify-no-changes", "--binarylog", binlogPath)
            .WithWorkingDirectory(_repoPath!)
            .Execute();

        // Exit code 0 = no changes needed, 2 = format differences found (expected for external repos).
        // Any other exit code is an actual failure.
        Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 2,
            $"dotnet format exited with unexpected code {result.ExitCode}. See {binlogPath} for details.\n{result.StdErr}");
    }

    [TestMethod]
    public void FormatFolder()
    {
        SkipIfExcluded();

        _output.WriteLine($"Formatting folder: {_repoPath}");

        var result = new DotnetCommand(_output, "format", "whitespace", _repoPath!, "--folder", "--verify-no-changes")
            .WithWorkingDirectory(_repoPath!)
            .Execute();

        Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 2,
            $"dotnet format exited with unexpected code {result.ExitCode}.\n{result.StdErr}");
    }

    /// <summary>
    /// Builds a path for a diagnostic MSBuild binary log whose file name ties it to this test
    /// (e.g. "format-efcore.binlog"). In Helix the log is written to the work item upload root so
    /// it is collected as a build artifact; otherwise it is written to the test artifacts directory.
    /// </summary>
    private string GetBinlogPath()
    {
        var directory = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT")
            ?? SdkTestContext.Current.TestExecutionDirectory;
        return Path.Combine(directory, $"format-{RepoName}.binlog");
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
                Assert.Inconclusive("Format integration tests only run on Linux in CI.");
            }

            var maxPriorityValue = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MAX_PRIORITY");
            if (int.TryParse(maxPriorityValue, out var maxPriority) && Priority > maxPriority)
            {
                Assert.Inconclusive($"Skipped: test priority {Priority} exceeds max priority {maxPriority} for this run.");
            }
        }
    }

    private void CloneRepo()
    {
        _output.WriteLine($"Cloning {RepoUrl} at {Sha}...");
        var sw = Stopwatch.StartNew();

        RunProcess("git", "init");
        RunProcess("git", $"remote add origin {RepoUrl}");
        RunProcess("git", $"fetch --progress --no-tags --depth=1 origin {Sha}");
        RunProcess("git", $"checkout {Sha}");

        sw.Stop();
        _output.WriteLine($"Clone completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
    }

    /// <summary>
    /// Removes the repo's global.json so that the SDK under test is used for restore
    /// and formatting rather than the repo's pinned SDK version.
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
        var sw = Stopwatch.StartNew();

        var solutionPath = FindSolution();
        RestoreWithParentSdk(solutionPath);

        sw.Stop();
        _output.WriteLine($"Restore completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
    }

    /// <summary>
    /// Restores the solution using the SDK under test's dotnet with the repo's nuget.config.
    /// Failures are logged but not fatal — some feeds may require authentication that
    /// is unavailable in public CI environments.
    /// </summary>
    private void RestoreWithParentSdk(string solutionPath)
    {
        var nugetConfig = Path.Combine(_repoPath!, "nuget.config");
        _output.WriteLine($"Restoring {Path.GetFileName(solutionPath)} with SDK under test: {ParentDotNetPath}");

        var args = File.Exists(nugetConfig)
            ? $"restore \"{solutionPath}\" --configfile \"{nugetConfig}\""
            : $"restore \"{solutionPath}\"";

        RunProcess(ParentDotNetPath, args, ignoreThrowingOnError: true);
    }

    /// <summary>Maximum time any single subprocess is allowed to run before being killed.</summary>
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(30);

    private (int ExitCode, string StdOut, string StdErr) RunProcess(string fileName, string arguments, bool ignoreThrowingOnError = false, bool captureOutput = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = _repoPath!,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;

        // Always capture stderr (it's small — errors/warnings only).
        // Only capture stdout when requested — for heavy operations (clone, restore) the
        // output can be very large and buffering it in memory is unnecessary.
        var stdoutTask = captureOutput ? process.StandardOutput.ReadToEndAsync(CancellationToken.None) : null;
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            var partialErr = stderrTask.GetAwaiter().GetResult();
            _output.WriteLine(partialErr);
            if (stdoutTask != null)
            {
                _output.WriteLine(stdoutTask.GetAwaiter().GetResult());
            }
            throw new TimeoutException(
                $"Command '{fileName} {arguments}' exceeded {ProcessTimeout.TotalMinutes} minute timeout.");
        }

        var stdout = stdoutTask?.GetAwaiter().GetResult() ?? string.Empty;
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            _output.WriteLine(stdout);
            _output.WriteLine(stderr);

            if (!ignoreThrowingOnError)
            {
                throw new InvalidOperationException(
                    $"Command '{fileName} {arguments}' exited with code {process.ExitCode}");
            }

            _output.WriteLine($"Warning: Command '{fileName} {arguments}' exited with code {process.ExitCode} (non-fatal).");
        }

        return (process.ExitCode, stdout, stderr);
    }

    private string FindSolution()
    {
        var matches = Directory.GetFiles(_repoPath!, TargetSolution, SearchOption.AllDirectories);
        Assert.IsNotEmpty(matches,
            $"Target solution '{TargetSolution}' not found under {_repoPath}");

        return matches[0];
    }
}
