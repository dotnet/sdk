// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Applies the two orthogonal dotnetup environment settings — dotnet exposure
/// (<see cref="PathPreference"/>) and whether dotnetup itself is on PATH — by writing and
/// unwinding the shell-profile block, the Windows user-scope dotnet env vars, and the Windows
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
/// The previous settings (when supplied) let the applier unwind anything the prior state set up
/// that the new state no longer needs.
/// </summary>
internal static class PathPreferenceApplier
{
    public static void Apply(
        PathPreference targetEnv,
        bool targetDotnetupOnPath,
        PathPreference? previousEnv,
        bool? previousDotnetupOnPath,
        IDotnetEnvironmentManager environment,
        string dotnetRoot,
        IEnvShellProvider? shellProvider = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrEmpty(dotnetRoot);

        if (targetEnv == PathPreference.All && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                $"{nameof(PathPreference)}.{nameof(PathPreference.All)} is only supported on Windows.");
        }

        // Windows user-scope dotnet env vars (PATH + DOTNET_ROOT) are wired only by All mode.
        bool prevWroteDotnetEnvVars = previousEnv == PathPreference.All;
        bool nowWritesDotnetEnvVars = targetEnv == PathPreference.All;

        // The managed profile block wires dotnet for Shell/All, and dotnetup when dotnetupOnPath.
        bool nowProfileDotnet = targetEnv is PathPreference.Shell or PathPreference.All;
        bool nowProfileDotnetup = targetDotnetupOnPath;
        bool nowHasProfileBlock = nowProfileDotnet || nowProfileDotnetup;

        bool prevProfileDotnet = previousEnv is PathPreference.Shell or PathPreference.All;
        // A null previousDotnetupOnPath means "no prior config"; treat as not-yet-written.
        bool prevProfileDotnetup = previousDotnetupOnPath == true;
        bool prevHadProfileBlock = prevProfileDotnet || prevProfileDotnetup;

        shellProvider ??= ShellDetection.GetCurrentShellProvider();

        // 1. Unwind the Windows dotnet env-var wiring if we no longer want it.
        //    ApplyEnvironmentModifications(InstallType.System) is the inverse of
        //    ApplyEnvironmentModifications(InstallType.User): it removes the user dotnet from
        //    user PATH, restores the Program Files dotnet to system PATH, and unsets the
        //    user-scope DOTNET_ROOT.
        if (prevWroteDotnetEnvVars && !nowWritesDotnetEnvVars)
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

        // 4. Profile block: write it when something needs wiring, remove it otherwise.
        if (nowHasProfileBlock)
        {
            RequireShellProvider(shellProvider);
            environment.ApplyTerminalProfileModifications(
                dotnetRoot,
                includeDotnet: nowProfileDotnet,
                includeDotnetup: nowProfileDotnetup,
                shellProvider: shellProvider);
        }
        else if (prevHadProfileBlock)
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
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                "Could not detect the current shell, which is required to update the dotnetup profile entry. Re-run with --shell <bash|zsh|fish|pwsh> to specify it explicitly.");
        }
    }
}
