// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:property TargetFramework=$(NetCurrent)
#:property RollForward=LatestMajor

// Evaluates which conditional test scopes should run based on changed files and build context.
// Reads test/ConditionalTests.props and outputs a semicolon-separated list of active scope names.
//
// Usage:
//   dotnet run EvaluateConditionalTestScopes.cs -- --props-file <path> [--target-branch <branch>] [--build-reason <reason>]
//
// When --target-branch is not provided, all scopes are active (safe default for local dev).
// Changed files are determined via `git diff --name-only origin/<target-branch>...HEAD`.

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var targetBranch = GetArg("--target-branch");
var buildReason = GetArg("--build-reason") ?? "";
var propsFile = GetArg("--props-file");

if (string.IsNullOrEmpty(propsFile) || !File.Exists(propsFile))
{
    Console.Error.WriteLine("Error: --props-file is required and must point to an existing ConditionalTests.props.");
    return 1;
}
propsFile = Path.GetFullPath(propsFile);
var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(propsFile)!, ".."));

// Parse ConditionalTests.props
var doc = XDocument.Load(propsFile);
var scopes = doc.Descendants("ConditionalTestScope").ToList();

bool isCI = buildReason is not "" and not "PullRequest";

// Get changed files via git diff
var changedFiles = GetChangedFiles(targetBranch, repoRoot);
bool hasChangedFiles = changedFiles.Count > 0;

var activeScopes = new List<string>();

foreach (var scope in scopes)
{
    var name = scope.Attribute("Include")?.Value ?? "";
    var runAlways = (scope.Element("RunAlways")?.Value ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var triggerPaths = (scope.Element("TriggerPaths")?.Value ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    bool shouldRun = false;

    // Check RunAlways conditions
    if (runAlways.Contains("CI", StringComparer.OrdinalIgnoreCase) && isCI)
        shouldRun = true;

    // If no changed files info available (local dev), run everything
    if (!hasChangedFiles)
        shouldRun = true;

    // Check if any changed file matches trigger paths
    if (!shouldRun && hasChangedFiles)
    {
        foreach (var changedFile in changedFiles)
        {
            var normalized = changedFile.Replace('\\', '/');
            foreach (var pattern in triggerPaths)
            {
                if (GlobMatches(normalized, pattern.Replace('\\', '/')))
                {
                    shouldRun = true;
                    break;
                }
            }
            if (shouldRun) break;
        }
    }

    if (shouldRun)
        activeScopes.Add(name);
}

var result = activeScopes.Count > 0 ? string.Join(";", activeScopes) : "__none__";

// Set Azure DevOps pipeline variable if running in CI
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER")))
{
    Console.WriteLine($"##vso[task.setvariable variable=ActiveConditionalTestScopes]{result}");
}

Console.WriteLine($"Active conditional test scopes: {result}");
return 0;

// --- Helpers ---

static List<string> GetChangedFiles(string? targetBranch, string repoRoot)
{
    if (string.IsNullOrEmpty(targetBranch))
        return [];

    try
    {
        var psi = new ProcessStartInfo("git", ["diff", "--name-only", $"origin/{targetBranch}...HEAD"])
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var result = Process.RunAndCaptureText(psi);

        if (result.ExitStatus.ExitCode != 0) return [];

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
    catch
    {
        return [];
    }
}

static bool GlobMatches(string path, string pattern)
{
    // Convert glob to regex: ** = any path, * = any segment chars
    var regexPattern = "^" +
        Regex.Escape(pattern)
            .Replace("\\*\\*", "@@GLOBSTAR@@")
            .Replace("\\*", "[^/]*")
            .Replace("@@GLOBSTAR@@", ".*") +
        "$";
    return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
}

static string? GetArg(string name)
{
    var args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}
