// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#:property TargetFramework=$(NetCurrent)
#:property RollForward=LatestMajor

// Evaluates which conditional test scopes should be skipped based on changed files and build context.
// Reads test/ConditionalTests.props and outputs a semicolon-separated list of skipped scope names.
//
// Usage:
//   dotnet run EvaluateConditionalTestScopes.cs -- --repo-root <path> [--target-branch <branch>] [--build-reason <reason>] [--output-variable <name>]
//
// When --target-branch is not provided, no scopes are skipped (safe default for local dev).
// Changed files are determined via `git diff --name-only origin/<target-branch>...HEAD`.
//
// Output variable format (set via ##vso when running in Azure Pipelines):
//   - Empty string: no scopes skipped, all tests run.
//   - "__all__": every defined scope is skipped.
//   - Semicolon-separated scope names (e.g. "TemplateEngine;ILLink"): only listed scopes are skipped.

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var targetBranch = GetArg("--target-branch");
var buildReason = GetArg("--build-reason") ?? "";
var repoRoot = GetArg("--repo-root");
var outputVariable = GetArg("--output-variable");

if (string.IsNullOrEmpty(repoRoot) || !Directory.Exists(repoRoot))
{
    Console.Error.WriteLine("Error: --repo-root is required and must point to an existing directory.");
    return 1;
}
repoRoot = Path.GetFullPath(repoRoot);

var propsFile = Path.Combine(repoRoot, "test", "ConditionalTests.props");
if (!File.Exists(propsFile))
{
    Console.Error.WriteLine($"Error: ConditionalTests.props not found at expected location: {propsFile}");
    return 1;
}

// Parse ConditionalTests.props
var doc = XDocument.Load(propsFile);
var scopes = doc.Descendants("ConditionalTestScope").ToList();

// Parse GlobalTriggerPaths
var globalTriggerPaths = (doc.Descendants("GlobalTriggerPaths").FirstOrDefault()?.Value ?? "")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// Validate configuration
ValidateConfiguration(repoRoot, scopes, globalTriggerPaths);

bool isCI = buildReason is not "" and not "PullRequest";

// Get changed files via git diff
var changedFiles = GetChangedFiles(targetBranch, repoRoot);
bool hasChangedFiles = changedFiles.Count > 0;

Console.WriteLine($"Build reason: {buildReason}");
Console.WriteLine($"Target branch: {targetBranch ?? "(none)"}");
Console.WriteLine($"Is CI (non-PR): {isCI}");
Console.WriteLine($"Changed files: {changedFiles.Count}");

// Check global triggers — if any changed file matches, no scopes are skipped
bool globalTriggered = false;
if (hasChangedFiles && globalTriggerPaths.Length > 0)
{
    foreach (var changedFile in changedFiles)
    {
        var normalized = changedFile.Replace('\\', '/');
        foreach (var pattern in globalTriggerPaths)
        {
            if (GlobMatches(normalized, pattern.Replace('\\', '/')))
            {
                globalTriggered = true;
                Console.WriteLine($"Global trigger matched: '{normalized}' against '{pattern}' — no scopes skipped");
                break;
            }
        }
        if (globalTriggered)
        {
            break;
        }
    }
}

var skippedScopes = new List<string>();

// If global triggered, nothing is skipped — done
if (globalTriggered)
{
    Console.WriteLine("All scopes will run (global trigger).");
}
else
{
    foreach (var scope in scopes)
    {
        var name = scope.Attribute("Include")?.Value ?? "";
        var runAlways = (scope.Element("RunAlways")?.Value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var triggerPaths = (scope.Element("TriggerPaths")?.Value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool shouldRun = false;
        string reason = "no trigger match";

        // Check RunAlways conditions
        if (runAlways.Contains("CI", StringComparer.OrdinalIgnoreCase) && isCI)
        {
            shouldRun = true;
            reason = "RunAlways=CI";
        }
        // If no changed files info available (local dev), run everything
        else if (!hasChangedFiles)
        {
            shouldRun = true;
            reason = "no changed files (safe fallback)";
        }
        // Check if any changed file matches trigger paths
        else
        {
            foreach (var changedFile in changedFiles)
            {
                var normalized = changedFile.Replace('\\', '/');
                foreach (var pattern in triggerPaths)
                {
                    if (GlobMatches(normalized, pattern.Replace('\\', '/')))
                    {
                        shouldRun = true;
                        reason = $"matched '{normalized}' against '{pattern}'";
                        break;
                    }
                }
                if (shouldRun)
                {
                    break;
                }
            }
        }

        if (shouldRun)
        {
            Console.WriteLine($"Scope '{name}': RUN ({reason})");
        }
        else
        {
            skippedScopes.Add(name);
            Console.WriteLine($"Scope '{name}': SKIP ({reason})");
        }
    }
}

var result = skippedScopes.Count > 0 && skippedScopes.Count == scopes.Count ? "__all__"
    : skippedScopes.Count > 0 ? string.Join(";", skippedScopes)
    : "";

// Set Azure DevOps pipeline variable if running in CI and output variable was specified
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")) && !string.IsNullOrEmpty(outputVariable))
{
    Console.WriteLine($"##vso[task.setvariable variable={outputVariable}]{result}");
}

Console.WriteLine($"Skipped test scopes: {(result == "" ? "(none)" : result)}");
return 0;

// --- Helpers ---

static List<string> GetChangedFiles(string? targetBranch, string repoRoot)
{
    if (string.IsNullOrEmpty(targetBranch))
    {
        return [];
    }

    try
    {
        // Strip refs/heads/ prefix if present (AzDO may provide full ref)
        if (targetBranch.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
        {
            targetBranch = targetBranch["refs/heads/".Length..];
        }

        var psi = new ProcessStartInfo("git", ["diff", "--name-only", $"origin/{targetBranch}...HEAD"])
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var result = Process.RunAndCaptureText(psi);

        if (result.ExitStatus.ExitCode != 0)
        {
            return [];
        }

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
    // Convert glob to regex: ** = any path, * = any segment chars, ? = single char
    var regexPattern = "^" +
        Regex.Escape(pattern)
            .Replace("\\*\\*", "@@GLOBSTAR@@")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", "[^/]")
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
        {
            return args[i + 1];
        }
    }
    return null;
}

static void ValidateConfiguration(string repoRoot, List<XElement> scopes, string[] globalTriggerPaths)
{
    var warnings = new List<string>();

    foreach (var scope in scopes)
    {
        var scopeName = scope.Attribute("Include")?.Value;
        if (string.IsNullOrWhiteSpace(scopeName))
        {
            warnings.Add("ConditionalTestScope is missing a name (Include attribute).");
            continue;
        }

        var triggerPaths = (scope.Element("TriggerPaths")?.Value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var testProjects = (scope.Element("TestProjects")?.Value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Validate required metadata elements exist
        var mechanism = scope.Element("Mechanism")?.Value;
        if (string.IsNullOrWhiteSpace(mechanism))
        {
            warnings.Add($"Scope '{scopeName}' is missing required <Mechanism> element.");
        }
        if (triggerPaths.Length == 0)
        {
            warnings.Add($"Scope '{scopeName}' is missing required <TriggerPaths> element or has no paths defined.");
        }
        if (string.Equals(mechanism, "project", StringComparison.OrdinalIgnoreCase) && testProjects.Length == 0)
        {
            warnings.Add($"Scope '{scopeName}' uses Mechanism=project but is missing <TestProjects> or has no paths defined.");
        }

        // Warn on unrecognized child elements (catches typos like <TriggerPath> instead of <TriggerPaths>)
        var knownElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Mechanism", "TestProjects", "TriggerPaths", "RunAlways" };
        foreach (var element in scope.Elements())
        {
            if (!knownElements.Contains(element.Name.LocalName))
            {
                warnings.Add($"Scope '{scopeName}' has unrecognized element <{element.Name.LocalName}>. Known elements: {string.Join(", ", knownElements)}.");
            }
        }

        foreach (var pattern in triggerPaths)
        {
            ValidatePatternBaseDir(repoRoot, pattern, $"scope '{scopeName}' TriggerPaths", warnings);
        }
        foreach (var pattern in testProjects)
        {
            ValidatePatternBaseDir(repoRoot, pattern, $"scope '{scopeName}' TestProjects", warnings);
        }
    }

    foreach (var pattern in globalTriggerPaths)
    {
        ValidatePatternBaseDir(repoRoot, pattern, "GlobalTriggerPaths", warnings);
    }

    foreach (var warning in warnings)
    {
        Console.WriteLine($"##[warning]{warning}");
    }
}

static void ValidatePatternBaseDir(string repoRoot, string pattern, string context, List<string> warnings)
{
    var normalized = pattern.Replace('\\', '/');
    var firstWildcard = normalized.IndexOfAny(['*', '?']);
    if (firstWildcard < 0)
    {
        // No glob — treat as literal path
        var fullPath = Path.Combine(repoRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            warnings.Add($"{context}: '{pattern}' does not exist in the repo.");
        }
        return;
    }

    // Get the directory portion before the first wildcard
    var baseDir = normalized[..firstWildcard].TrimEnd('/');
    if (string.IsNullOrEmpty(baseDir))
    {
        return;
    }

    var fullBaseDir = Path.Combine(repoRoot, baseDir.Replace('/', Path.DirectorySeparatorChar));
    if (!Directory.Exists(fullBaseDir))
    {
        warnings.Add($"{context}: '{pattern}' — base directory '{baseDir}' does not exist in the repo.");
    }
}
