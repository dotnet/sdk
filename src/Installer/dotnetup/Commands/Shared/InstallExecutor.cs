// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
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
    /// then displays a unified result summary.
    /// </summary>
    /// <param name="requests">The resolved install requests to execute.</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <returns>The installation results, one per request.</returns>
    public static InstallResult[] ExecuteInstalls(
        List<ResolvedInstallRequest> requests,
        bool noProgress)
    {
        if (requests.Count == 0)
        {
            return Array.Empty<InstallResult>();
        }

        DotnetInstallRoot installRoot = requests[0].Request.InstallRoot;
        string escapedPath = installRoot.Path.EscapeMarkup();
        string accent = DotnetupTheme.Current.Accent;

        // Print "Installing X, Y, Z to <path>..."
        var descriptions = requests.Select(r =>
            string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]",
                r.Request.Component.GetDisplayName(), accent, r.ResolvedVersion)).ToList();
        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "Installing {0} to [{1}]{2}[/]...",
            string.Join(", ", descriptions),
            accent,
            escapedPath));

        IReadOnlyList<InstallResult> results;
        {
            IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
            using var sharedReporter = new LazyProgressReporter(progressTarget);
            results = InstallerOrchestratorSingleton.Instance.InstallMany(requests, sharedReporter);
        }

        DisplayBatchResults(results);
        return results.ToArray();
    }

    /// <summary>
    /// Displays a summary of batch install results, grouping by newly installed vs already installed.
    /// </summary>
    private static void DisplayBatchResults(IReadOnlyList<InstallResult> results)
    {
        var installed = new List<string>();
        var alreadyInstalled = new List<string>();
        string? sharedPath = null;

        foreach (var result in results)
        {
            sharedPath ??= result.Install.InstallRoot.Path;
            string successAccent = DotnetupTheme.Current.SuccessAccent;
            string installDetailLine = string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", result.Install.Component.GetDisplayName(), successAccent, result.Install.Version.ToString().EscapeMarkup());
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
    }

    private static void EmitBatchSummaryLines(List<string> installed, List<string> alreadyInstalled, string? sharedPath)
    {
        string successAccent = DotnetupTheme.Current.SuccessAccent;
        string escapedPath = sharedPath?.EscapeMarkup() ?? string.Empty;

        if (installed.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "Installed at [{0}]{1}[/]:", successAccent, escapedPath));
            foreach (var item in installed)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}", item));
            }
        }

        if (alreadyInstalled.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "Already installed at [{0}]{1}[/]:", successAccent, escapedPath));
            foreach (var item in alreadyInstalled)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}", item));
            }
        }
    }
}
