// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
///   <item>On Windows, <c>All</c> additionally writes user-scope env-var PATH/DOTNET_ROOT, and
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
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentException.ThrowIfNullOrEmpty(dotnetRoot);

        if (targetEnv == DotnetAccessMode.Full && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                $"{nameof(DotnetAccessMode)}.{nameof(DotnetAccessMode.Full)} is only supported on Windows.");
        }

        // Windows user-scope dotnet env vars (PATH + DOTNET_ROOT) are wired only by All mode.
        bool nowWritesDotnetEnvVars = targetEnv == DotnetAccessMode.Full;

        // The managed profile block wires dotnet for Shell/All, and dotnetup when dotnetupOnPath.
        bool nowProfileDotnet = targetEnv is DotnetAccessMode.Shell or DotnetAccessMode.Full;
        bool nowProfileDotnetup = targetDotnetupOnPath;
        bool nowHasProfileBlock = nowProfileDotnet || nowProfileDotnetup;

        shellProvider ??= ShellDetection.GetCurrentShellProvider();

        // 1. Remove the Windows dotnet env-var wiring if it is present and we no longer want it.
        //    ApplyEnvironmentModifications(InstallType.System) is the inverse of
        //    ApplyEnvironmentModifications(InstallType.User): it removes the user dotnet from
        //    user PATH, restores the Program Files dotnet to system PATH, and unsets the
        //    user-scope DOTNET_ROOT.
        if (observed.DotnetUserEnvVarsPresent && !nowWritesDotnetEnvVars)
        {
            environment.ApplyEnvironmentModifications(InstallType.System);
        }

        // 2. Apply the Windows dotnet env-var wiring if we now want it.
        if (nowWritesDotnetEnvVars)
        {
            environment.ApplyEnvironmentModifications(InstallType.User, dotnetRoot);
        }

        // 3. Windows user-scope dotnetup PATH entry (idempotent add/remove; no-op off Windows).
        environment.ApplyDotnetupOnUserPath(targetDotnetupOnPath);

        // 4. Profile block: write it when something needs wiring, remove an observed block otherwise.
        if (nowHasProfileBlock)
        {
            RequireShellProvider(shellProvider);
            environment.ApplyTerminalProfileModifications(
                dotnetRoot,
                includeDotnet: nowProfileDotnet,
                includeDotnetup: nowProfileDotnetup,
                shellProvider: shellProvider);
        }
        else if (observed.ProfileBlockPresent == true)
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
                $"Could not detect the current shell, which is required to update the dotnetup profile entry. Re-run with --shell <{supportedShells}> to specify it explicitly.");
        }
    }
}
