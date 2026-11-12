// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Handles the execution of .NET component installations with consistent messaging and progress handling.
/// </summary>
internal class InstallExecutor
{
    /// <summary>
    /// Executes a batch of resolved install requests concurrently via <see cref="InstallerOrchestratorSingleton.InstallMany"/>,
    /// then displays a unified result summary including any per-request failures.
    /// </summary>
    /// <param name="requests">The resolved install requests to execute.</param>
    /// <param name="progress">How to display progress.</param>
    /// <returns>The batch result containing successes and failures.</returns>
    public static InstallBatchResult ExecuteInstalls(
        List<ResolvedInstallRequest> requests,
        ProgressMode progress)
    {
        if (requests.Count == 0)
        {
            return new InstallBatchResult(Array.Empty<InstallResult>(), Array.Empty<InstallFailure>());
        }

        DotnetInstallRoot installRoot = requests[0].Request.InstallRoot;
        string escapedPath = installRoot.Path.EscapeMarkup();
        bool summaryOnly = progress == ProgressMode.None;

        if (summaryOnly)
        {
            SpectreAnsiConsole.WriteLine($"Installing {FormatGroupedRequests(requests)} to {installRoot.Path}...");
        }
        else
        {
            // Print "Installing X, Y, Z to <path>..."
            var descriptions = requests.Select(r =>
                $"{r.Request.Component.GetDisplayName()} {DotnetupTheme.Accent($"{r.ResolvedVersion}")}").ToList();
            SpectreAnsiConsole.MarkupLine(
                $"Installing {string.Join(", ", descriptions)} to {DotnetupTheme.Accent(escapedPath)}...");
        }

        if (requests.Any(r => UnsignedSourcePolicy.MayDownloadUnsigned(r.Request)))
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Warning(Microsoft.Dotnet.Installation.Strings.UnsignedBlobFeedWarning.EscapeMarkup()));
        }

        InstallBatchResult batchResult;
        {
            IProgressTarget progressTarget = ProgressTargets.Create(progress);
            using var sharedReporter = new LazyProgressReporter(progressTarget);
            batchResult = InstallerOrchestratorSingleton.Instance.InstallMany(requests, sharedReporter);
        }

        if (summaryOnly)
        {
            DisplaySummaryInRequestOrder(requests, batchResult, installRoot.Path);
        }
        else
        {
            DisplayBatchResults(batchResult);
        }

        return batchResult;
    }

    /// <summary>Formats requests as "dotnet (runtime) 8.0.27, 9.0.16, .NET SDK 10.0.100", grouping versions by component.</summary>
    internal static string FormatGroupedRequests(IReadOnlyList<ResolvedInstallRequest> requests) =>
        string.Join(", ", requests
            .GroupBy(r => r.Request.Component)
            .Select(group => $"{group.Key.GetDisplayName()} {string.Join(", ", group.Select(r => r.ResolvedVersion.ToString()))}"));

    /// <summary>
    /// Writes one plain, aligned line per request in request order, so concurrent downloads cannot change the
    /// output; used for <c>--no-progress</c>.
    /// </summary>
    internal static void DisplaySummaryInRequestOrder(IReadOnlyList<ResolvedInstallRequest> requests, InstallBatchResult batchResult, string installRootPath)
    {
        var labels = requests.Select(r => $"{r.Request.Component.GetDisplayName()} {r.ResolvedVersion}").ToList();
        int labelWidth = labels.Max(label => label.Length);
        var results = MatchResultsToRequests(requests, batchResult);

        for (int i = 0; i < requests.Count; i++)
        {
            SpectreAnsiConsole.WriteLine($"  {labels[i].PadRight(labelWidth)}  {DescribeOutcome(requests[i], results[i], batchResult)}");
        }

        if (batchResult.Successes.Count > 0)
        {
            SpectreAnsiConsole.WriteLine($"Installed at {installRootPath}.");
        }
    }

    /// <summary>
    /// Pairs each request with its success, preferring an exact version match. A request satisfied by an existing
    /// build of another version (for example a daily build for a global.json pin) takes a remaining result of its component.
    /// </summary>
    private static InstallResult?[] MatchResultsToRequests(IReadOnlyList<ResolvedInstallRequest> requests, InstallBatchResult batchResult)
    {
        var unclaimed = batchResult.Successes.ToList();
        var matches = new InstallResult?[requests.Count];
        InstallResult? Claim(Func<InstallResult, bool> predicate)
        {
            InstallResult? match = unclaimed.FirstOrDefault(predicate);
            if (match is not null)
            {
                unclaimed.Remove(match);
            }

            return match;
        }

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            matches[i] = Claim(r => r.Install.Component == request.Request.Component && r.Install.Version.Equals(request.ResolvedVersion));
        }

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (matches[i] is null && !batchResult.Failures.Any(f => ReferenceEquals(f.Request, request)))
            {
                matches[i] = Claim(r => r.Install.Component == request.Request.Component);
            }
        }

        return matches;
    }

    private static string DescribeOutcome(ResolvedInstallRequest request, InstallResult? result, InstallBatchResult batchResult)
    {
        if (batchResult.Failures.FirstOrDefault(f => ReferenceEquals(f.Request, request)) is { } failure)
        {
            return $"failed: {failure.Exception.Message}";
        }

        if (result is null)
        {
            return "not installed";
        }

        if (result.WasAlreadyInstalled)
        {
            return result.Install.Version.Equals(request.ResolvedVersion)
                ? "already present, skipped"
                : $"already present as {result.Install.Version}, skipped";
        }

        var details = new List<string>();
        if (result.DownloadedBytes is long bytes)
        {
            details.Add(ProgressFormatting.FormatMB(bytes).Trim());
        }

        if (result.Repaired)
        {
            details.Add("repaired");
        }

        return details.Count > 0 ? $"installed ({string.Join(", ", details)})" : "installed";
    }

    /// <summary>
    /// Executes a batch of resolved install requests, records install-result telemetry and the completed installs on
    /// <paramref name="command"/>, and throws if any request failed. This is the standard
    /// entry point for production code paths that need failures to surface as exceptions
    /// rather than be silently swallowed.
    /// </summary>
    public static void ExecuteInstallsAndThrowOnFailure(
        List<ResolvedInstallRequest> requests,
        ProgressMode progress,
        InstallCommand command)
    {
        var batchResult = ExecuteInstalls(requests, progress);
        command.CompletedInstalls.AddRange(batchResult.Successes);

        int newlyInstalled = batchResult.Successes.Count(r => !r.WasAlreadyInstalled);
        int alreadyInstalled = batchResult.Successes.Count(r => r.WasAlreadyInstalled);
        command.SetCommandTag(TelemetryTagNames.InstallResult, $"installed:{newlyInstalled},already_installed:{alreadyInstalled}");
        if (GetRepairTagValue(batchResult) is string repairTag)
        {
            command.SetCommandTag(TelemetryTagNames.InstallRepair, repairTag);
        }

        if (batchResult.Failures.Count > 0)
        {
            // Attach failures [1..] to the primary so RecordException stamps them as
            // error.additional_failures on the command row when CommandBase catches the
            // rethrown primary. Without this only the first failure was visible in
            // telemetry even though all were printed to the user.
            var primary = batchResult.Failures[0].Exception;
            for (int i = 1; i < batchResult.Failures.Count; i++)
            {
                primary.AttachAdditionalFailure(batchResult.Failures[i].Exception);
            }

            // DisplayBatchResults already printed every failure message.
            primary.MarkReportedToUser();

            ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    /// <summary>
    /// The <see cref="TelemetryTagNames.InstallRepair"/> value for a batch, or <c>null</c> when no install
    /// needed a repair.
    /// </summary>
    internal static string? GetRepairTagValue(InstallBatchResult batchResult)
    {
        int succeeded = batchResult.Successes.Count(r => r.Repaired);
        int failed = batchResult.Failures.Count(f => f.Exception.IsRepairFailed());
        return succeeded + failed == 0 ? null : $"succeeded:{succeeded},failed:{failed}";
    }

    /// <summary>
    /// Displays a summary of batch install results, grouping by newly installed, already installed, and failed.
    /// </summary>
    private static void DisplayBatchResults(InstallBatchResult batchResult)
    {
        var installed = new List<string>();
        var alreadyInstalled = new List<string>();
        string? sharedPath = null;

        foreach (var result in batchResult.Successes)
        {
            sharedPath ??= result.Install.InstallRoot.Path;
            string installDetailLine = $"{result.Install.Component.GetDisplayName()} {DotnetupTheme.SuccessAccent(result.Install.Version.ToString().EscapeMarkup())}";
            if (result.WasAlreadyInstalled)
            {
                alreadyInstalled.Add(installDetailLine);
            }
            else
            {
                installed.Add(installDetailLine);
            }
        }

        EmitBatchSummaryLines(installed, alreadyInstalled, sharedPath);

        if (batchResult.Failures.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Error("The following installs failed:"));
            foreach (var failure in batchResult.Failures)
            {
                SpectreAnsiConsole.MarkupLine(
                    $"  {DotnetupTheme.Error($"{failure.Request.Request.Component.GetDisplayName()} {failure.Request.ResolvedVersion.ToString().EscapeMarkup()}: {failure.Exception.Message.EscapeMarkup()}")}");
            }
        }
    }

    private static void EmitBatchSummaryLines(List<string> installed, List<string> alreadyInstalled, string? sharedPath)
    {
        string escapedPath = sharedPath?.EscapeMarkup() ?? string.Empty;

        if (installed.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine($"Installed at {DotnetupTheme.SuccessAccent(escapedPath)}:");
            foreach (var item in installed)
            {
                SpectreAnsiConsole.MarkupLine($"  {item}");
            }
        }

        if (alreadyInstalled.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine($"Already installed at {DotnetupTheme.SuccessAccent(escapedPath)}:");
            foreach (var item in alreadyInstalled)
            {
                SpectreAnsiConsole.MarkupLine($"  {item}");
            }
        }
    }
}
