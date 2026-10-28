// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Proposes the next free CA diagnostic ID for a category and reports in-flight collisions.
//
//   dotnet NextDiagnosticId.cs <category>
//
// DiagnosticCategoryAndIdRanges.txt records only *merged* work, so the "next" ID is
// frequently already claimed by an open PR or a concurrent branch. This scans forward from
// the end of the category's range until it finds one unclaimed in the working tree, on any
// local branch, and in any open dotnet/sdk PR, then prints the range edit that covers it.
//
// The open-PR check searches PR titles and bodies, not diffs, so an ID used only in changed
// source can slip through. It is a strong heuristic, not a guarantee.
//
// Exit codes:
//   0  all checks ran
//   1  ID proposed, but the open-PR check did not run or did not complete
//   2  usage error, unknown category, git failure, or no free ID in the scan window

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

const string AnalyzerRoot = "src/Microsoft.CodeAnalysis.NetAnalyzers";
const int ScanLimit = 25;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet NextDiagnosticId.cs <category>");
    return 2;
}

string category = args[0];

(int gitExit, string repoRootOutput) = Exec("git", "rev-parse", "--show-toplevel");

if (gitExit != 0)
{
    Console.Error.WriteLine("error: not inside a git repository.");
    return 2;
}

string repoRoot = repoRootOutput.Trim();
string rangesFile = Path.Combine(repoRoot, AnalyzerRoot, "src", "Utilities", "Compiler", "DiagnosticCategoryAndIdRanges.txt");
string[] lines = File.ReadAllLines(rangesFile);
string? line = Array.Find(lines, l => Regex.IsMatch(l, $@"^\s*{Regex.Escape(category)}\s*:"));

if (line is null)
{
    // Only categories with a CA range can be allocated from; the rest carry RS ranges.
    string known = string.Join(", ", lines
        .Where(l => Regex.IsMatch(l, @"^\w+\s*:.*\bCA\d+"))
        .Select(l => l.Split(':')[0]));

    Console.Error.WriteLine($"error: category '{category}' not found. Allocatable categories: {known}");
    return 2;
}

// The range to extend is the last CA segment on the line; earlier segments are legacy or
// prefix entries (e.g. 'Performance: HA, CA1800-CA1877').
string? lastRange = line.Split(':', 2)[1]
    .Split(',')
    .Select(segment => segment.Trim())
    .LastOrDefault(segment => Regex.IsMatch(segment, @"^CA\d+(-CA\d+)?$"));

if (lastRange is null)
{
    Console.Error.WriteLine($"error: no CA range found on line: {line}");
    return 2;
}

string[] bounds = lastRange.Split('-');
string rangeStart = bounds[0];
int rangeEnd = int.Parse(bounds[^1]["CA".Length..], CultureInfo.InvariantCulture);

bool ghAvailable = Exec("gh", "--version").ExitCode == 0;
bool prCheckComplete = ghAvailable;
string? gitFailure = null;

// Concurrent branches, including those checked out in other worktrees.
string[] branches = Exec("git", "-C", repoRoot, "for-each-ref", "--format=%(refname)", "refs/heads")
    .Output
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

List<(string Id, string Reason)> skipped = [];
string? proposed = null;

for (int candidate = rangeEnd + 1; candidate <= rangeEnd + ScanLimit; candidate++)
{
    string id = $"CA{candidate}";
    string? reason = ClaimedBy(id);

    if (gitFailure is not null)
    {
        Console.Error.WriteLine($"error: {gitFailure}");
        return 2;
    }

    if (reason is null)
    {
        proposed = id;
        break;
    }

    skipped.Add((id, reason));
}

if (proposed is null)
{
    Console.Error.WriteLine($"error: no free ID in CA{rangeEnd + 1}..CA{rangeEnd + ScanLimit} for '{category}'; every candidate is claimed.");
    return 2;
}

int rangeAt = line.LastIndexOf(lastRange, StringComparison.Ordinal);
string updatedLine = $"{line[..rangeAt]}{rangeStart}-{proposed}{line[(rangeAt + lastRange.Length)..]}";

Console.WriteLine($"Category      : {category}");
Console.WriteLine($"Current range : {lastRange}");
Console.WriteLine($"Proposed ID   : {proposed}");

if (skipped.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Skipped (already claimed):");

    foreach ((string id, string reason) in skipped)
    {
        Console.WriteLine($"  {id} - {reason}");
    }
}

Console.WriteLine();
Console.WriteLine("Apply to DiagnosticCategoryAndIdRanges.txt:");
Console.WriteLine($"  - {line}");
Console.WriteLine($"  + {updatedLine}");
Console.WriteLine();

if (ghAvailable && prCheckComplete)
{
    Console.WriteLine($"{proposed} is unclaimed in the working tree, on local branches, and in open dotnet/sdk PR titles and bodies.");
    return 0;
}

Console.WriteLine($"{proposed} is unclaimed in the working tree and on local branches.");
Console.Error.WriteLine(ghAvailable
    ? "warning: a dotnet/sdk PR query failed; open PRs were not fully checked."
    : "warning: gh is not on PATH; open PRs were not checked.");

return 1;

string? ClaimedBy(string id)
{
    // No revision, so this searches the working tree: the current branch plus uncommitted
    // work. --untracked also covers files created but not yet staged.
    if (GitGrep(["-C", repoRoot, "grep", "-l", "--untracked", "--fixed-strings", id, "--", AnalyzerRoot]) is string inTree)
    {
        return $"working tree ({inTree})";
    }

    if (branches.Length > 0 &&
        GitGrep(["-C", repoRoot, "grep", "-l", "--fixed-strings", id, .. branches, "--", AnalyzerRoot]) is string onBranch)
    {
        return $"branch ({onBranch})";
    }

    if (gitFailure is not null || !ghAvailable)
    {
        return null;
    }

    (int exitCode, string output) = Exec("gh", "pr", "list", "--repo", "dotnet/sdk", "--state", "open", "--search", id, "--json", "number,title");

    if (exitCode != 0)
    {
        prCheckComplete = false;
        return null;
    }

    return FirstOpenPr(output) is string pr ? $"open PR ({pr})" : null;
}

string? GitGrep(string[] arguments)
{
    if (gitFailure is not null)
    {
        return null;
    }

    (int exitCode, string output) = Exec("git", arguments);

    // git grep exits 0 on a match and 1 on none; anything else is a real failure, and
    // silently reading it as "no match" would hand back an ID that is already taken.
    if (exitCode is not (0 or 1))
    {
        gitFailure = $"git grep exited with {exitCode}. Arguments: {string.Join(' ', arguments)}";
        return null;
    }

    return exitCode == 0 ? FirstLine(output) : null;
}

string? FirstOpenPr(string json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        prCheckComplete = false;
        return null;
    }

    try
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            prCheckComplete = false;
            return null;
        }

        if (document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement pr = document.RootElement[0];
        return $"#{pr.GetProperty("number")} {pr.GetProperty("title").GetString()}";
    }
    catch (JsonException)
    {
        prCheckComplete = false;
        return null;
    }
}

static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

static (int ExitCode, string Output) Exec(string fileName, params string[] arguments)
{
    ProcessStartInfo startInfo = new()
    {
        FileName = fileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    foreach (string argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    try
    {
        using Process process = Process.Start(startInfo)!;

        // Drain stderr concurrently so a chatty child can't fill its pipe and deadlock.
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        stderr.Wait();

        return (process.ExitCode, output);
    }
    catch (Win32Exception)
    {
        return (-1, "");
    }
}
