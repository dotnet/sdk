// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        bool summaryOnly = progress == ProgressMode.None;

        InstallSummaryDisplay.DisplayInstallingLine(requests, installRoot.Path, plain: summaryOnly);

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
            InstallSummaryDisplay.DisplaySummaryInRequestOrder(requests, batchResult, installRoot.Path);
        }
        else
        {
            InstallSummaryDisplay.DisplayBatchResults(batchResult);
        }

        return batchResult;
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

            // The install summary already printed every failure message.
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
}
