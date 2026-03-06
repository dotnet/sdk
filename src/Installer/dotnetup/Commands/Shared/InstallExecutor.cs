// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Handles the execution of .NET component installations with consistent messaging and progress handling.
/// </summary>
internal class InstallExecutor
{
    /// <summary>
    /// Result of an installation execution.
    /// </summary>
    public record InstallResult(DotnetInstall Install, bool WasAlreadyInstalled = false);

    /// <summary>
    /// Result of creating and resolving an install request.
    /// </summary>
    public record ResolvedInstallRequest(DotnetInstallRequest Request, ReleaseVersion? ResolvedVersion);

    /// <summary>
    /// Creates a DotnetInstallRequest and resolves the version using the channel version resolver.
    /// </summary>
    /// <param name="installPath">The installation path.</param>
    /// <param name="channel">The channel or version to install.</param>
    /// <param name="component">The component type (SDK, Runtime, ASPNETCore, WindowsDesktop).</param>
    /// <param name="manifestPath">Optional manifest path for tracking installations.</param>
    /// <param name="channelVersionResolver">The resolver to use for version resolution.</param>
    /// <param name="requireMuxerUpdate">If true, fail when the muxer cannot be updated.</param>
    /// <param name="installSource">The source of this install request.</param>
    /// <param name="globalJsonPath">The path to the global.json that triggered this install.</param>
    /// <param name="untracked">If true, install without recording in the manifest.</param>
    /// <returns>The resolved install request with version information.</returns>
    public static ResolvedInstallRequest CreateAndResolveRequest(
        string installPath,
        string channel,
        InstallComponent component,
        string? manifestPath,
        ChannelVersionResolver channelVersionResolver,
        bool requireMuxerUpdate = false,
        InstallRequestSource installSource = InstallRequestSource.Explicit,
        string? globalJsonPath = null,
        bool untracked = false)
    {
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(channel),
            component,
            new InstallRequestOptions
            {
                ManifestPath = manifestPath,
                RequireMuxerUpdate = requireMuxerUpdate,
                InstallSource = installSource,
                GlobalJsonPath = globalJsonPath,
                Untracked = untracked
            });

        var resolvedVersion = channelVersionResolver.Resolve(request);

        return new ResolvedInstallRequest(request, resolvedVersion);
    }

    /// <summary>
    /// Executes the installation of a .NET component and displays appropriate status messages.
    /// </summary>
    /// <param name="installRequest">The installation request to execute.</param>
    /// <param name="resolvedVersion">The resolved version string for display purposes.</param>
    /// <param name="componentDescription">Description of the component (e.g., ".NET SDK", ".NET Runtime").</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <returns>The installation result, or null if installation failed.</returns>
    public static InstallResult? ExecuteInstall(
        DotnetInstallRequest installRequest,
        string? resolvedVersion,
        string componentDescription,
        bool noProgress)
    {
#pragma warning disable CA1305 // Spectre.Console API does not accept IFormatProvider
        SpectreAnsiConsole.MarkupLineInterpolated($"Installing {componentDescription} [blue]{resolvedVersion}[/] to [blue]{installRequest.InstallRoot.Path}[/]...");
#pragma warning restore CA1305

        Microsoft.DotNet.Tools.Bootstrapper.InstallResult orchestratorResult;
        try
        {
            orchestratorResult = InstallerOrchestratorSingleton.Instance.Install(installRequest, noProgress);
        }
        catch (DotnetInstallException ex)
        {
            SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Failed to install {componentDescription} {resolvedVersion}: {ex.Message}[/]");
            return null;
        }

        if (orchestratorResult.WasAlreadyInstalled)
        {
            SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]{componentDescription} {orchestratorResult.Install.Version} is already installed at {orchestratorResult.Install.InstallRoot}[/]");
        }
        else
        {
            SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]Installed {componentDescription} {orchestratorResult.Install.Version}, available via {orchestratorResult.Install.InstallRoot}[/]");
        }

        return new InstallResult(orchestratorResult.Install, orchestratorResult.WasAlreadyInstalled);
    }

    /// <summary>
    /// Executes the installation of additional versions of a .NET component.
    /// </summary>
    /// <param name="additionalVersions">The list of additional versions to install.</param>
    /// <param name="installRoot">The installation root path.</param>
    /// <param name="component">The component type to install.</param>
    /// <param name="componentDescription">Description of the component for display.</param>
    /// <param name="manifestPath">Optional manifest path.</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <param name="requireMuxerUpdate">If true, fail when the muxer cannot be updated.</param>
    /// <returns>True if all installations succeeded, false if any failed.</returns>
    public static bool ExecuteAdditionalInstalls(
        IEnumerable<string> additionalVersions,
        DotnetInstallRoot installRoot,
        InstallComponent component,
        string componentDescription,
        string? manifestPath,
        bool noProgress,
        bool requireMuxerUpdate = false)
    {
        bool allSucceeded = true;

        foreach (var additionalVersion in additionalVersions)
        {
            var additionalRequest = new DotnetInstallRequest(
                installRoot,
                new UpdateChannel(additionalVersion),
                component,
                new InstallRequestOptions
                {
                    ManifestPath = manifestPath,
                    RequireMuxerUpdate = requireMuxerUpdate
                });

            try
            {
                var additionalResult = InstallerOrchestratorSingleton.Instance.Install(additionalRequest, noProgress);
                SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]Installed additional {componentDescription} {additionalResult.Install.Version}, available via {additionalResult.Install.InstallRoot}[/]");
            }
            catch (DotnetInstallException)
            {
                SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Failed to install additional {componentDescription} {additionalVersion}[/]");
                allSucceeded = false;
            }
        }

        return allSucceeded;
    }

    /// <summary>
    /// Configures the default .NET installation if requested.
    /// </summary>
    /// <param name="dotnetInstaller">The install manager.</param>
    /// <param name="setDefaultInstall">Whether to set as default install.</param>
    /// <param name="installPath">The installation path.</param>
    public static void ConfigureDefaultInstallIfRequested(
        IDotnetInstallManager dotnetInstaller,
        bool setDefaultInstall,
        string installPath)
    {
        if (setDefaultInstall)
        {
            dotnetInstaller.ConfigureInstallType(InstallType.User, installPath);
        }
    }

    /// <summary>
    /// Displays completion message.
    /// </summary>
    public static void DisplayComplete()
    {
        SpectreAnsiConsole.WriteLine("Complete!");
    }

    /// <summary>
    /// Determines whether the given path is an admin/system-managed .NET install location.
    /// These locations are managed by system package managers or OS installers and should not
    /// be used by dotnetup for user-level installations.
    /// </summary>
    public static bool IsAdminInstallPath(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if ((!string.IsNullOrEmpty(programFiles) && IsOrIsUnder(fullPath, Path.Combine(programFiles, "dotnet"), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(programFilesX86) && IsOrIsUnder(fullPath, Path.Combine(programFilesX86, "dotnet"), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        else
        {
            // Standard admin/package-manager locations on Linux and macOS
            if (IsOrIsUnder(fullPath, "/usr/share/dotnet", StringComparison.Ordinal) ||
                IsOrIsUnder(fullPath, "/usr/lib/dotnet", StringComparison.Ordinal) ||
                IsOrIsUnder(fullPath, "/usr/local/share/dotnet", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOrIsUnder(string fullPath, string adminPath, StringComparison comparison)
    {
        return string.Equals(fullPath, adminPath, comparison) ||
               fullPath.StartsWith(adminPath + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Classifies the install path for telemetry (no PII - just the type of location).
    /// When pathSource is provided, global_json paths are distinguished from other path types.
    /// </summary>
    /// <param name="path">The install path to classify.</param>
    /// <param name="pathSource">How the path was determined (e.g., "global_json", "explicit"). Null to skip source-based classification.</param>
    public static string ClassifyInstallPath(string path, PathSource? pathSource = null)
    {
        var fullPath = Path.GetFullPath(path);

        // Check for admin/system .NET paths first — these are the most important to distinguish
        if (IsAdminInstallPath(path))
        {
            return "admin";
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrEmpty(programFiles) && fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
            {
                return "system_programfiles";
            }
            if (!string.IsNullOrEmpty(programFilesX86) && fullPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
            {
                return "system_programfiles_x86";
            }

            // Check more-specific paths before less-specific ones:
            // LocalApplicationData (e.g., C:\Users\x\AppData\Local) is under UserProfile (C:\Users\x)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData) && fullPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
            {
                return "local_appdata";
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                return "user_profile";
            }
        }
        else
        {
            if (fullPath.StartsWith("/usr/", StringComparison.Ordinal) ||
                fullPath.StartsWith("/opt/", StringComparison.Ordinal))
            {
                return "system_path";
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && fullPath.StartsWith(home, StringComparison.Ordinal))
            {
                return "user_home";
            }
        }

        // If the path was specified by global.json and doesn't match a well-known location,
        // classify it as global_json rather than generic "other"
        if (pathSource == PathSource.GlobalJson)
        {
            return "global_json";
        }

        return "other";
    }
}
