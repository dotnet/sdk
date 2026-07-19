// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Applies the two orthogonal dotnetup environment settings — dotnet access
/// (<see cref="DotnetAccessMode"/>) and whether dotnetup itself is on PATH — by writing and
/// removing the shell-profile block, the Windows user-scope dotnet env vars, and the Windows
/// user-scope dotnetup PATH entry. See
/// documentation/general/dotnetup/designs/dotnetup-env.md for the full composition model.
///
/// Composition:
/// <list type="bullet">
///   <item>The managed profile block wires dotnet iff <c>env ∈ {Shell, All}</c> and adds the
///     dotnetup directory iff <c>dotnetupOnPath</c>. When it would wire neither, the block is
///     removed.</item>
///   <item>On Windows, <c>All</c> additionally writes the user dotnet directory to the system
///     PATH (ahead of the machine-wide install) and sets user-scope DOTNET_ROOT, and
///     <c>dotnetupOnPath</c> additionally writes the dotnetup directory to the user PATH (so
///     cmd.exe and GUI apps see it).</item>
/// </list>
///
/// Removal decisions are driven by the <see cref="ObservedEnvironmentState"/> read from the live
/// environment, not by the stored config. This lets the applier clean up wiring that is actually
/// present even when the config never recorded it (or drifted), and avoids running the elevating
/// env-var removal when nothing is wired.
/// </summary>
internal static class EnvSettingsApplier
{
    public static void Apply(
        DotnetAccessMode targetEnv,
        bool targetDotnetupOnPath,
        ObservedEnvironmentState observed,
        IDotnetEnvironmentManager environment,
        string dotnetRoot,
        IEnvShellProvider? shellProvider = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(dotnetRoot);

        if (!DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(targetEnv))
        {
            throw new PlatformNotSupportedException(
                $"{nameof(DotnetAccessMode)}.{nameof(DotnetAccessMode.Everywhere)} is only supported on Windows.");
        }

        // Windows user-scope dotnet env vars (PATH + DOTNET_ROOT) are wired only by All mode.
        bool nowWritesDotnetEnvVars = targetEnv == DotnetAccessMode.Everywhere;

        // The managed profile block wires dotnet for Shell/All, and dotnetup when dotnetupOnPath.
        bool nowProfileDotnet = targetEnv is DotnetAccessMode.Shell or DotnetAccessMode.Everywhere;
        bool nowProfileDotnetup = targetDotnetupOnPath;
        bool nowHasProfileBlock = nowProfileDotnet || nowProfileDotnetup;

        shellProvider ??= ShellDetection.GetCurrentShellProvider();

        // 1. Windows dotnet env-var wiring (PATH + DOTNET_ROOT): apply it when we now want it,
        //    otherwise remove an observed wiring when we no longer do. These are the two mutually
        //    exclusive branches of the same decision. ApplyEnvironmentModifications(InstallType.System)
        //    is the inverse of (InstallType.User): it removes the user dotnet directory from the system
        //    PATH and unsets the user-scope DOTNET_ROOT. The apply
        //    path is idempotent (each change is gated), so re-applying an already-correct state — e.g.
        //    re-syncing after a system installer clobbered PATH — is safe.
        if (nowWritesDotnetEnvVars)
        {
            environment.ApplyEnvironmentModifications(InstallType.User, dotnetRoot);
        }
        else if (observed.DotnetUserEnvVarsPresent)
        {
            environment.ApplyEnvironmentModifications(InstallType.System);
        }

        // 2. Windows user-scope dotnetup PATH entry (idempotent add/remove; no-op off Windows).
        environment.ApplyDotnetupOnUserPath(targetDotnetupOnPath);

        // 3. Profile block: write it when something needs wiring, remove an observed block otherwise.
        if (nowHasProfileBlock)
        {
            RequireShellProvider(shellProvider);
            environment.ApplyTerminalProfileModifications(
                dotnetRoot,
                includeDotnet: nowProfileDotnet,
                includeDotnetup: nowProfileDotnetup,
                shellProvider: shellProvider);
        }
        else if (observed.ProfileBlock is ProfileBlockState.Present)
        {
            RequireShellProvider(shellProvider);
            ShellProfileManager.RemoveProfileEntries(shellProvider!);
        }
    }

    /// <summary>
    /// Throws a clear, telemetry-recorded error when a profile operation is required but the
    /// active shell could not be detected. Silently skipping would leave the managed block in a
    /// state that contradicts the new settings.
    /// </summary>
    private static void RequireShellProvider(IEnvShellProvider? shellProvider)
    {
        if (shellProvider is null)
        {
            string supportedShells = string.Join("|", ShellDetection.s_supportedShells.Select(s => s.ArgumentName));
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                string.Format(CultureInfo.InvariantCulture, Strings.EnvShellRequiredForProfile, supportedShells));
        }
    }
}
