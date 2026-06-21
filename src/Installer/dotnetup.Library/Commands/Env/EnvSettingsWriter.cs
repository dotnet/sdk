// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Shared apply path for the <c>env set</c> and <c>env clear</c> commands: inspects the live
/// environment, applies the target settings via <see cref="EnvSettingsApplier"/>, persists the
/// new config, and prints a short summary. Keeping this in one place ensures <c>env clear</c> and
/// <c>env set none --dotnetup-on-path false</c> behave identically.
/// </summary>
internal static class EnvSettingsWriter
{
    public static void ApplyAndPersist(
        DotnetAccessMode targetEnv,
        bool targetDotnetupOnPath,
        IDotnetEnvironmentManager environment,
        IEnvShellProvider? shellProvider,
        IEnvironmentStateInspector inspector)
    {
        IEnvShellProvider? resolvedShellProvider = shellProvider ?? ShellDetection.GetCurrentShellProvider();

        string dotnetRoot = environment.GetDefaultDotnetInstallPath();

        // Removal decisions come from the live environment, not the stored config, so drift is
        // corrected on re-sync.
        ObservedEnvironmentState observed = inspector.Inspect(resolvedShellProvider);

        EnvSettingsApplier.Apply(
            targetEnv,
            targetDotnetupOnPath,
            observed,
            environment,
            dotnetRoot,
            resolvedShellProvider);

        DotnetupConfigData newConfig = new()
        {
            AccessMode = targetEnv,
            DotnetupOnPath = targetDotnetupOnPath,
        };
        DotnetupConfig.Write(newConfig);

        Console.WriteLine(DescribeOutcome(targetEnv, targetDotnetupOnPath));

        EnvTerminalState terminalState = EnvActivationStatus.EvaluateCurrentProcess(newConfig, environment);
        if (!terminalState.IsActive)
        {
            // `eval`-ing the script won't remove existing paths, so don't suggest it in that case
            string? activationCommand = terminalState.NeedsRemovals
                ? null
                : EnvActivationCommandBuilder.TryBuild(resolvedShellProvider, targetEnv, targetDotnetupOnPath);

            if (activationCommand is not null)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.EnvApplyToTerminalPrompt, activationCommand));
            }
            else
            {
                Console.WriteLine(Strings.EnvOpenNewTerminalToTakeEffect);
            }
        }
    }

    /// <summary>
    /// Describes the resulting environment in outcome terms (what's on PATH) rather than the
    /// internal setting names, composed from both axes. <c>full</c> shares <c>shell</c>'s wording —
    /// from the terminal's perspective the available commands are the same; the difference is only
    /// that <c>full</c> also reaches cmd / GUI apps, which is not worth a distinct line here.
    /// </summary>
    private static string DescribeOutcome(DotnetAccessMode accessMode, bool dotnetupOnPath) =>
        (accessMode, dotnetupOnPath) switch
        {
            (DotnetAccessMode.None, true) => Strings.EnvOutcomeDotnetupOnlyOnPath,
            (DotnetAccessMode.None, false) => Strings.EnvOutcomeNeitherOnPath,
            (_, true) => Strings.EnvOutcomeBothOnPath,
            (_, false) => Strings.EnvOutcomeDotnetOnlyOnPath,
        };
}
