// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Applies a <see cref="PathPreference"/> by writing/unwinding the environment-variable
/// and shell-profile changes implied by that preference.
///
/// Maps preferences to operations:
/// <list type="bullet">
///   <item><see cref="PathPreference.None"/>: neither env vars nor profile.</item>
///   <item><see cref="PathPreference.Shell"/>: profile only.</item>
///   <item><see cref="PathPreference.All"/> (Windows only): profile <em>and</em> user env vars
///     (PATH + DOTNET_ROOT), and removes the Program Files dotnet from system PATH.</item>
/// </list>
///
/// When a `previous` preference is supplied, the applier unwinds anything the previous
/// preference set up that the new one no longer needs (e.g. removing the managed profile block
/// on a transition into <see cref="PathPreference.None"/>, or restoring the admin-dominates
/// system PATH on a transition out of <see cref="PathPreference.All"/>).
/// </summary>
internal static class PathPreferenceApplier
{
    public static void Apply(
        PathPreference target,
        PathPreference? previous,
        IDotnetEnvironmentManager environment,
        string dotnetRoot,
        IEnvShellProvider? shellProvider = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrEmpty(dotnetRoot);

        if (target == PathPreference.All && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                $"{nameof(PathPreference)}.{nameof(PathPreference.All)} is only supported on Windows.");
        }

        bool prevWroteEnvVars = previous == PathPreference.All;
        bool prevWroteProfile = previous is PathPreference.Shell or PathPreference.All;
        bool nowWritesEnvVars = target == PathPreference.All;
        bool nowWritesProfile = target is PathPreference.Shell or PathPreference.All;

        shellProvider ??= ShellDetection.GetCurrentShellProvider();

        // 1. Unwind: remove the previous env-var changes if we no longer want them.
        if (prevWroteEnvVars && !nowWritesEnvVars)
        {
            // ApplyEnvironmentModifications(InstallType.System) is the inverse of
            // ApplyEnvironmentModifications(InstallType.User): it removes the user dotnet from
            // user PATH, restores the Program Files dotnet to system PATH, and unsets the
            // user-scope DOTNET_ROOT.
            environment.ApplyEnvironmentModifications(InstallType.System);
        }

        // 2. Unwind: remove the managed profile block if we no longer want a profile entry.
        if (prevWroteProfile && !nowWritesProfile)
        {
            if (shellProvider is null)
            {
                // We must remove the managed block we previously wrote, but we can't
                // detect the active shell. Silently skipping would leave the entry behind
                // and the env vars exported on every shell startup, contradicting the new
                // mode. Fail loudly with a hint so the user can re-run with --shell.
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    "Could not detect the current shell, which is required to remove the dotnetup profile entry written by the previous mode. Re-run with --shell <bash|zsh|fish|pwsh> to specify it explicitly.");
            }

            ShellProfileManager.RemoveProfileEntries(shellProvider);
        }

        // 3. Apply: write the env-var changes.
        if (nowWritesEnvVars)
        {
            environment.ApplyEnvironmentModifications(InstallType.User, dotnetRoot);
        }

        // 4. Apply: write the profile entries.
        if (nowWritesProfile)
        {
            environment.ApplyTerminalProfileModifications(dotnetRoot, shellProvider: shellProvider);
        }
    }
}
