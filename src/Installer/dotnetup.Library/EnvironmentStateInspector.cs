// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Inspects the live environment and reports what dotnetup has actually wired as an
/// <see cref="ObservedEnvironmentState"/>. This is the single place that reads machine reality for
/// the env-axis model, so both <see cref="PathPreferenceApplier"/> (for reality-based unwinding)
/// and <see cref="Commands.Env.EnvShowCommand"/> (for drift reporting) make decisions from the
/// same observation instead of trusting the stored config. Mirrors the inspect-then-act split of
/// <see cref="InstallRootManager"/>.
/// </summary>
internal sealed class EnvironmentStateInspector : IEnvironmentStateInspector
{
    private readonly IDotnetEnvironmentManager _environment;

    public EnvironmentStateInspector(IDotnetEnvironmentManager? environment = null)
    {
        _environment = environment ?? new DotnetEnvironmentManager();
    }

    /// <summary>
    /// Reads the current environment state. The shell profile is only inspected when a provider is
    /// supplied (or can be detected); when it is <c>null</c>, <see cref="ObservedEnvironmentState.ProfileBlockPresent"/>
    /// is left <c>null</c> to signal "unknown" rather than asserting absence.
    /// </summary>
    public ObservedEnvironmentState Inspect(IEnvShellProvider? shellProvider)
    {
        bool dotnetUserEnvVarsPresent = false;
        bool dotnetUserEnvVarsComplete = false;
        bool dotnetupOnUserPath = false;

        if (OperatingSystem.IsWindows())
        {
            var installRootManager = new InstallRootManager(_environment);

            // Residual user-scope dotnet wiring exists exactly when switching to the admin/system
            // state would still need to change something (remove the user PATH entry, unset
            // DOTNET_ROOT, restore the Program Files dotnet to system PATH).
            dotnetUserEnvVarsPresent = installRootManager.GetAdminInstallRootChanges().NeedsChange();

            // Fully wired as 'all' when configuring the user install root would be a no-op.
            dotnetUserEnvVarsComplete = !installRootManager.GetUserInstallRootChanges().NeedsChange();

            dotnetupOnUserPath = UserPathContainsDotnetupDir();
        }

        bool? profileBlockPresent = shellProvider is null
            ? null
            : ShellProfileManager.GetProfilePathsWithEntries(shellProvider).Count > 0;

        return new ObservedEnvironmentState(
            dotnetUserEnvVarsPresent,
            dotnetUserEnvVarsComplete,
            profileBlockPresent,
            dotnetupOnUserPath);
    }

    [SupportedOSPlatform("windows")]
    private static bool UserPathContainsDotnetupDir()
    {
        string dotnetupDir = ShellProviderHelpers.GetDotnetupDirectoryOrThrow();
        return WindowsPathHelper.SplitPath(WindowsPathHelper.ReadUserPath(expand: true))
            .Any(entry => DotnetupUtilities.PathsEqual(entry, dotnetupDir));
    }
}
