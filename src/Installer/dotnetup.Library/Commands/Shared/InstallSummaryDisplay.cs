// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Writes the lines that <see cref="InstallExecutor"/> prints before and after a batch install.
/// <c>--no-progress</c> uses plain lines in request order; other progress modes use styled lines grouped by outcome.
/// </summary>
internal static class InstallSummaryDisplay
{
    /// <summary>Writes "Installing X, Y, Z to &lt;path&gt;...".</summary>
    internal static void DisplayInstallingLine(IReadOnlyList<ResolvedInstallRequest> requests, string installRootPath, bool plain)
    {
        if (plain)
        {
            SpectreAnsiConsole.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.InstallingComponentsTo, FormatGroupedRequests(requests), installRootPath));
            return;
        }

        var descriptions = requests.Select(r =>
            $"{r.Request.Component.GetDisplayName()} {DotnetupTheme.Accent($"{r.ResolvedVersion}")}");
        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.CurrentCulture,
            Strings.InstallingComponentsTo.EscapeMarkup(),
            string.Join(", ", descriptions),
            DotnetupTheme.Accent(installRootPath.EscapeMarkup())));
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

        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            InstallResult? result = batchResult.Successes.FirstOrDefault(r => ReferenceEquals(r.Request, request));
            SpectreAnsiConsole.WriteLine($"  {labels[i].PadRight(labelWidth)}  {DescribeOutcome(request, result, batchResult)}");
        }

        if (batchResult.Successes.Count > 0)
        {
            SpectreAnsiConsole.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.InstallSummaryInstalledAt, installRootPath));
        }
    }

    private static string DescribeOutcome(ResolvedInstallRequest request, InstallResult? result, InstallBatchResult batchResult)
    {
        if (batchResult.Failures.FirstOrDefault(f => ReferenceEquals(f.Request, request)) is { } failure)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.InstallSummaryFailed, failure.Exception.Message);
        }

        if (result is null)
        {
            return Strings.InstallSummaryNotInstalled;
        }

        if (result.WasAlreadyInstalled)
        {
            return result.Install.Version.Equals(request.ResolvedVersion)
                ? Strings.InstallSummaryAlreadyInstalled
                : string.Format(CultureInfo.CurrentCulture, Strings.InstallSummaryAlreadyInstalledAsVersion, result.Install.Version);
        }

        var details = new List<string>();
        if (result.DownloadedBytes is long bytes)
        {
            details.Add(ProgressFormatting.FormatMB(bytes).Trim());
        }

        if (result.Repaired)
        {
            details.Add(Strings.InstallSummaryRepaired);
        }

        return details.Count > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.InstallSummaryInstalledWithDetails, string.Join(", ", details))
            : Strings.InstallSummaryInstalled;
    }

    /// <summary>
    /// Displays a summary of batch install results, grouping by newly installed, already installed, and failed.
    /// </summary>
    internal static void DisplayBatchResults(InstallBatchResult batchResult)
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
