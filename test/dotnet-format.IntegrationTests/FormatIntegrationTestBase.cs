// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Format.IntegrationTests;

/// <summary>
/// Base class for dotnet-format integration tests. Each derived class targets a specific
/// repository and gets its own clone+restore lifecycle via <see cref="TestInitializeAttribute"/>.
/// Two test methods (workspace format and folder format) are inherited automatically.
/// </summary>
public abstract class FormatIntegrationTestBase : SdkTest
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(30);

    // Keyed by concrete type so each derived class gets its own entry.
    // MSTest creates a new instance per test method, so clone+restore would otherwise
    // run once per test method rather than once per class per process.
    private static readonly Dictionary<Type, string> s_initializedRepoPaths = new();

    private string? _repoPath;

    private string ParentDotNetPath => SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

    /// <summary>The full GitHub URL of the repository (e.g. "https://github.com/dotnet/sdk").</summary>
    protected abstract string RepoUrl { get; }

    /// <summary>The commit SHA to test against.</summary>
    protected abstract string Sha { get; }

    /// <summary>The solution/slnf/slnx file name to format (e.g. "sdk.slnx").</summary>
    protected abstract string TargetSolution { get; }

    /// <summary>Short display name for the repo (e.g. "sdk"). Used for filtering and logging.</summary>
    protected abstract string RepoName { get; }

    [TestInitialize]
    public void TestInitialize()
    {
        if (s_initializedRepoPaths.TryGetValue(GetType(), out var cachedPath))
        {
            _repoPath = cachedPath;
            Log.WriteLine($"Reusing initialized repo for {RepoName} at {_repoPath}");
            return;
        }

        _repoPath = Path.Combine(Path.GetTempPath(), "dotnet-format-tests", RepoName);

        if (IsAlreadyAtCorrectSha())
        {
            Log.WriteLine($"Reusing existing clone at {Sha}");
        }
        else
        {
            if (Directory.Exists(_repoPath))
            {
                Directory.Delete(_repoPath, recursive: true);
            }

            Directory.CreateDirectory(_repoPath);

            CloneRepo();

            // Run the repo's Arcade build script restore while global.json is still present.
            // This installs the Arcade SDK tooling that project files import (e.g.,
            // $(ArcadeSdkBuildTasksAssembly)). Without this, repos like Roslyn fail to restore
            // because their project files have hard imports of Arcade targets.
            RunArcadeBuildScriptRestore();
        }

        // Strip the "sdk" section from global.json so that the SDK under test is used
        // for solution restore and formatting. The "msbuild-sdks" section is preserved
        // so that NuGet SDK resolver can still find Arcade and other MSBuild SDKs.
        RemoveGlobalJsonSdkSection();

        Restore();

        s_initializedRepoPaths[GetType()] = _repoPath;
    }

    [TestMethod]
    public void FormatWorkspace()
    {
        var solutionPath = FindSolution();
        Log.WriteLine($"Formatting workspace: {solutionPath}");

        var result = new DotnetCommand(Log, "format", solutionPath, "--no-restore", "--verify-no-changes", "--verbosity", "detailed")
            .WithWorkingDirectory(Path.GetDirectoryName(solutionPath)!)
            .Execute();

        // Exit code 0 = no changes needed, 2 = format differences found (expected for external repos).
        // Any other exit code is an actual failure.
        Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 2,
            $"dotnet format exited with unexpected code {result.ExitCode}.\n{result.StdErr}");

        // Validate that dotnet format actually processed files. Without this check, a broken
        // restore can cause format to silently exit 0 having loaded zero projects.
        var match = Regex.Match(result.StdOut ?? "", @"Formatted \d+ of (\d+) files");
        Assert.IsTrue(match.Success && int.Parse(match.Groups[1].Value) > 0,
            $"dotnet format did not report processing any files. Restore may have failed.\nStdOut: {result.StdOut}");
    }

    [TestMethod]
    public void FormatFolder()
    {
        Log.WriteLine($"Formatting folder: {_repoPath}");

        var result = new DotnetCommand(Log, "format", "whitespace", _repoPath!, "--folder", "--verify-no-changes", "--verbosity", "detailed")
            .WithWorkingDirectory(_repoPath!)
            .Execute();

        Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 2,
            $"dotnet format exited with unexpected code {result.ExitCode}.\n{result.StdErr}");

        var match = Regex.Match(result.StdOut ?? "", @"Formatted \d+ of (\d+) files");
        Assert.IsTrue(match.Success && int.Parse(match.Groups[1].Value) > 0,
            $"dotnet format did not report processing any files.\nStdOut: {result.StdOut}");
    }

    private bool IsAlreadyAtCorrectSha()
    {
        if (!Directory.Exists(_repoPath))
        {
            return false;
        }

        var result = RunProcess("git", "rev-parse HEAD", ignoreThrowingOnError: true);
        return result.ExitStatus.ExitCode == 0 &&
               string.Equals(result.StandardOutput.Trim(), Sha, StringComparison.OrdinalIgnoreCase);
    }

    private void CloneRepo()
    {
        Log.WriteLine($"Cloning {RepoUrl} at {Sha}...");
        var sw = Stopwatch.StartNew();

        RunProcess("git", "init");
        RunProcess("git", $"remote add origin {RepoUrl}");
        RunProcess("git", $"fetch --progress --no-tags --depth=1 origin {Sha}");
        RunProcess("git", $"checkout {Sha}");

        sw.Stop();
        Log.WriteLine($"Clone completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
    }

    /// <summary>
    /// Runs the repo's Arcade build script with -restore to install Arcade SDK tooling.
    /// This must run while global.json is still present so that Arcade's install scripts
    /// can acquire the correct SDK version. The installed tooling (targets, tasks) is then
    /// available for the subsequent solution-level restore.
    /// Failures are non-fatal — repos without Arcade (or with inaccessible feeds) still
    /// proceed to the direct solution restore.
    /// </summary>
    private void RunArcadeBuildScriptRestore()
    {
        string scriptPath;
        string arguments;

        if (OperatingSystem.IsWindows())
        {
            var engBuild = Path.Combine(_repoPath!, "eng", "Build.ps1");
            var engCommonBuild = Path.Combine(_repoPath!, "eng", "common", "Build.ps1");

            if (File.Exists(engBuild))
            {
                scriptPath = "powershell";
                arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{engBuild}\" -restore";
            }
            else if (File.Exists(engCommonBuild))
            {
                scriptPath = "powershell";
                arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{engCommonBuild}\" -restore";
            }
            else
            {
                Log.WriteLine("No Arcade build script found; skipping Arcade restore.");
                return;
            }
        }
        else
        {
            var engBuildSh = Path.Combine(_repoPath!, "eng", "build.sh");
            var engCommonBuildSh = Path.Combine(_repoPath!, "eng", "common", "build.sh");

            if (File.Exists(engBuildSh))
            {
                scriptPath = "bash";
                arguments = $"\"{engBuildSh}\" --restore";
            }
            else if (File.Exists(engCommonBuildSh))
            {
                scriptPath = "bash";
                arguments = $"\"{engCommonBuildSh}\" --restore";
            }
            else
            {
                Log.WriteLine("No Arcade build script found; skipping Arcade restore.");
                return;
            }
        }

        Log.WriteLine($"Running Arcade build script restore: {scriptPath} {arguments}");
        var sw = Stopwatch.StartNew();

        RunProcess(scriptPath, arguments, ignoreThrowingOnError: true);

        sw.Stop();
        Log.WriteLine($"Arcade restore completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
    }

    /// <summary>
    /// Strips the "sdk" section from the repo's global.json so that the SDK under test
    /// is used for restore and formatting, while preserving the "msbuild-sdks" section
    /// that tells the NuGet SDK resolver which versions of MSBuild SDKs
    /// (e.g., Microsoft.DotNet.Arcade.Sdk) to use.
    /// </summary>
    private void RemoveGlobalJsonSdkSection()
    {
        var globalJsonPath = Path.Combine(_repoPath!, "global.json");
        if (!File.Exists(globalJsonPath))
        {
            return;
        }

        var json = JsonNode.Parse(File.ReadAllText(globalJsonPath));
        if (json is JsonObject obj && obj.ContainsKey("sdk"))
        {
            obj.Remove("sdk");
            File.WriteAllText(globalJsonPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Log.WriteLine("Removed 'sdk' section from global.json to use parent SDK.");
        }
    }

    private void Restore()
    {
        var sw = Stopwatch.StartNew();

        var solutionPath = FindSolution();
        RestoreWithParentSdk(solutionPath);

        sw.Stop();
        Log.WriteLine($"Restore completed in {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
    }

    /// <summary>
    /// Restores the solution using the SDK under test's dotnet.
    /// NuGet auto-discovers NuGet.config by walking up from the solution file's directory,
    /// so no explicit --configfile is needed.
    /// Failures are logged but not fatal — some feeds may require authentication that
    /// is unavailable in public CI environments.
    /// </summary>
    private void RestoreWithParentSdk(string solutionPath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        Log.WriteLine($"Restoring {Path.GetFileName(solutionPath)} with SDK under test: {ParentDotNetPath}");

        RunProcess(ParentDotNetPath, $"restore \"{solutionPath}\"", ignoreThrowingOnError: true, workingDirectory: solutionDir);
    }

    /// <summary>
    /// Runs a process and captures its stdout and stderr. Throws on timeout.
    /// When <paramref name="ignoreThrowingOnError"/> is false (default), also throws on non-zero exit code.
    /// </summary>
    private ProcessTextOutput RunProcess(string fileName, string arguments, bool ignoreThrowingOnError = false, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? _repoPath!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var result = Process.RunAndCaptureText(psi, ProcessTimeout);

        if (result.ExitStatus.ExitCode != 0)
        {
            Log.WriteLine(result.StandardOutput);
            Log.WriteLine(result.StandardError);

            if (!ignoreThrowingOnError)
            {
                throw new InvalidOperationException($"Command '{fileName} {arguments}' exited with code {result.ExitStatus.ExitCode}");
            }

            Log.WriteLine($"Warning: Command '{fileName} {arguments}' exited with code {result.ExitStatus.ExitCode} (non-fatal).");
        }

        return result;
    }

    private string FindSolution()
    {
        // Match the old script's -Recurse -Depth 2 behavior to avoid unnecessary deep traversal.
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2 };
        var matches = Directory.GetFiles(_repoPath!, TargetSolution, options);
        Assert.IsNotEmpty(matches, $"Target solution '{TargetSolution}' not found under {_repoPath}");

        if (matches.Length > 1)
        {
            Log.WriteLine($"Warning: multiple matches for '{TargetSolution}' under {_repoPath}: {string.Join(", ", matches)}. Using root-most match.");
        }

        return matches.MinBy(m => m.Length)!;
    }
}
