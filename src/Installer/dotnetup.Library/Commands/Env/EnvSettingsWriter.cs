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
        ObservedEnvironmentState observedBefore = inspector.Inspect(resolvedShellProvider);

        EnvSettingsApplier.Apply(
            targetEnv,
            targetDotnetupOnPath,
            observedBefore,
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

        // Whether the apply actually wrote anything to the persisted environment (profile block,
        // Windows user/system PATH, DOTNET_ROOT). Used to decide whether other terminals / cmd.exe
        // / GUI apps need to be relaunched — and to avoid a spurious reminder on an idempotent re-run.
        bool changedPersistedEnvironment = inspector.Inspect(resolvedShellProvider) != observedBefore;

        EnvTerminalState terminalState = EnvActivationStatus.EvaluateCurrentProcess(newConfig, environment);

        // `eval`-ing the script won't remove existing paths, so don't suggest it when removals are needed.
        string? activationCommand = !terminalState.IsActive && !terminalState.NeedsRemovals
            ? EnvActivationCommandBuilder.TryBuild(resolvedShellProvider, targetEnv, targetDotnetupOnPath)
            : null;

        string? effectMessage = BuildEffectMessage(terminalState, changedPersistedEnvironment, activationCommand);
        if (effectMessage is not null)
        {
            Console.WriteLine(effectMessage);
        }
    }

    /// <summary>
    /// Decides which "how to make this effective" message to print after applying env settings, or
    /// <c>null</c> when none is needed. Kept pure (no IO) so the branching is unit-testable:
    /// <list type="bullet">
    ///   <item>Current terminal not active: offer the activation command if we have one, otherwise
    ///     tell the user to open a new terminal.</item>
    ///   <item>Current terminal already active but the apply changed persisted state: the change is
    ///     live here, but other terminals / cmd.exe / GUI apps won't see it until they relaunch.</item>
    ///   <item>Current terminal active and nothing changed (idempotent re-run): no message.</item>
    /// </list>
    /// </summary>
    internal static string? BuildEffectMessage(EnvTerminalState terminalState, bool changedPersistedEnvironment, string? activationCommand)
    {
        if (!terminalState.IsActive)
        {
            return activationCommand is not null
                ? string.Format(CultureInfo.InvariantCulture, Strings.EnvApplyToTerminalPrompt, activationCommand)
                : Strings.EnvOpenNewTerminalToTakeEffect;
        }

        return changedPersistedEnvironment
            ? Strings.EnvOpenNewTerminalForOtherSurfaces
            : null;
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
