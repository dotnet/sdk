// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
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
    /// <returns>The installation result.</returns>
    public static InstallResult ExecuteInstall(
        DotnetInstallRequest installRequest,
        string? resolvedVersion,
        string componentDescription,
        bool noProgress)
    {
#pragma warning disable CA1305 // Spectre.Console API does not accept IFormatProvider
        SpectreAnsiConsole.MarkupLineInterpolated($"Installing {componentDescription} [{DotnetupTheme.Current.Accent}]{resolvedVersion}[/] to [{DotnetupTheme.Current.Accent}]{installRequest.InstallRoot.Path}[/]...");
#pragma warning restore CA1305

        var orchestratorResult = InstallerOrchestratorSingleton.Instance.Install(installRequest, noProgress);

        if (orchestratorResult.WasAlreadyInstalled)
        {
            SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"{componentDescription} [{DotnetupTheme.Current.Accent}]{orchestratorResult.Install.Version}[/] is already installed at [{DotnetupTheme.Current.Accent}]{orchestratorResult.Install.InstallRoot.Path}[/]");
        }
        else
        {
            SpectreAnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"Installed {componentDescription} [{DotnetupTheme.Current.Accent}]{orchestratorResult.Install.Version}[/] at [{DotnetupTheme.Current.Accent}]{orchestratorResult.Install.InstallRoot.Path}[/]");
        }

        return new InstallResult(orchestratorResult.Install, orchestratorResult.WasAlreadyInstalled);
    }

    /// <summary>
    /// Executes the installation of additional admin installs (SDKs and runtimes), optionally including
    /// a primary install request. Uses a two-phase approach: SDKs are installed first so that
    /// runtimes bundled within SDK archives are already on disk, then only standalone runtimes
    /// that are not yet present are downloaded.
    /// </summary>
    /// <param name="additionalInstalls">The list of additional admin installs to migrate.</param>
    /// <param name="installRoot">The installation root path.</param>
    /// <param name="manifestPath">Optional manifest path.</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <param name="requireMuxerUpdate">If true, fail when the muxer cannot be updated.</param>
    /// <param name="primaryRequest">Optional primary install request to batch with the appropriate phase.</param>
    /// <returns>The primary install result when <paramref name="primaryRequest"/> is supplied; null otherwise.</returns>
    public static InstallResult? ExecuteAdditionalInstalls(
        IEnumerable<DotnetInstall> additionalInstalls,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool noProgress,
        bool requireMuxerUpdate = false,
        DotnetInstallRequest? primaryRequest = null)
    {
        var installsList = additionalInstalls.ToList();
        if (installsList.Count == 0 && primaryRequest is null)
        {
            return null;
        }

        var sdkInstalls = installsList.Where(i => i.Component == InstallComponent.SDK).ToList();
        var runtimeInstalls = installsList.Where(i => i.Component != InstallComponent.SDK).ToList();

        bool primaryIsSdk = primaryRequest is not null && primaryRequest.Component == InstallComponent.SDK;
        var sdkPrimary = primaryIsSdk ? primaryRequest : null;
        var runtimePrimary = primaryIsSdk ? null : primaryRequest;

        DisplayBatchSummary(primaryRequest, installsList, installRoot);

        // Phase 1: install SDKs first — their archives typically bundle runtime binaries.
        var primaryResult = RunInstallBatch(sdkInstalls, installRoot, manifestPath, noProgress, requireMuxerUpdate, sdkPrimary);

        // Phase 2: skip runtimes whose files already landed on disk via an SDK archive.
        var remainingRuntimes = runtimeInstalls.Where(r => !RuntimeExistsOnDisk(installRoot, r)).ToList();
        var runtimeResult = RunInstallBatch(remainingRuntimes, installRoot, manifestPath, noProgress, requireMuxerUpdate, runtimePrimary);

        return primaryResult ?? runtimeResult;
    }

    private static void DisplayBatchSummary(
        DotnetInstallRequest? primaryRequest,
        List<DotnetInstall> additionalInstalls,
        DotnetInstallRoot installRoot)
    {
        if (primaryRequest is null)
        {
            return;
        }

        string desc = primaryRequest.Component.GetDisplayName();
#pragma warning disable CA1305 // Spectre.Console API does not accept IFormatProvider
        SpectreAnsiConsole.MarkupLineInterpolated($"Installing {desc} [{DotnetupTheme.Current.Accent}]{primaryRequest.ResolvedVersion}[/] and {additionalInstalls.Count} additional component(s) to [{DotnetupTheme.Current.Accent}]{installRoot.Path.EscapeMarkup()}[/]...");
#pragma warning restore CA1305
    }

    private static InstallResult? RunInstallBatch(
        List<DotnetInstall> installs,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool noProgress,
        bool requireMuxerUpdate,
        DotnetInstallRequest? primaryRequest)
    {
        var requests = BuildBatchRequestsFromInstalls(primaryRequest, installs, installRoot, manifestPath, requireMuxerUpdate);
        if (requests.Count == 0)
        {
            return null;
        }

        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
        using var sharedReporter = new LazyProgressReporter(progressTarget);

        var results = InstallerOrchestratorSingleton.Instance.InstallMany(requests, sharedReporter);
        return DisplayBatchResults(results, primaryRequest);
    }

    private static bool RuntimeExistsOnDisk(DotnetInstallRoot installRoot, DotnetInstall runtime)
    {
        string frameworkDir = Path.Combine(
            installRoot.Path,
            "shared",
            runtime.Component.GetFrameworkName(),
            runtime.Version.ToString());
        return Directory.Exists(frameworkDir);
    }

    private static List<DotnetInstallRequest> BuildBatchRequestsFromInstalls(
        DotnetInstallRequest? primaryRequest,
        List<DotnetInstall> additionalInstalls,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool requireMuxerUpdate)
    {
        var requests = new List<DotnetInstallRequest>();
        if (primaryRequest is not null)
        {
            requests.Add(primaryRequest);
        }

        requests.AddRange(additionalInstalls.Select(install => new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(install.Version.ToString()),
            install.Component,
            new InstallRequestOptions
            {
                ManifestPath = manifestPath,
                RequireMuxerUpdate = requireMuxerUpdate
            })));

        return requests;
    }

    private static InstallResult? DisplayBatchResults(
        IReadOnlyList<global::Microsoft.DotNet.Tools.Bootstrapper.InstallResult> results,
        DotnetInstallRequest? primaryRequest)
    {
        InstallResult? primaryResult = null;
        var installed = new List<string>();
        var alreadyInstalled = new List<string>();
        string? sharedPath = null;

        foreach (var result in results)
        {
            bool isPrimary = primaryRequest is not null
                && result.Install.Version.ToString() == primaryRequest.ResolvedVersion?.ToString()
                && result.Install.Component == primaryRequest.Component;

            if (isPrimary && primaryResult is null)
            {
                primaryResult = new InstallResult(result.Install, result.WasAlreadyInstalled);
            }

            sharedPath ??= result.Install.InstallRoot.Path;
            string accent = DotnetupTheme.Current.Accent;
            string label = string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", result.Install.Component.GetDisplayName(), accent, result.Install.Version.ToString().EscapeMarkup());
            if (result.WasAlreadyInstalled)
            {
                alreadyInstalled.Add(label);
            }
            else
            {
                installed.Add(label);
            }
        }

        EmitBatchSummaryLines(installed, alreadyInstalled, sharedPath);
        return primaryResult;
    }

    private static void EmitBatchSummaryLines(List<string> installed, List<string> alreadyInstalled, string? sharedPath)
    {
        string accent = DotnetupTheme.Current.Accent;
        string escapedPath = sharedPath?.EscapeMarkup() ?? string.Empty;

        if (installed.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "Installed {0} at [{1}]{2}[/]", string.Join(", ", installed), accent, escapedPath));
        }

        if (alreadyInstalled.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "{0} already installed at [{1}]{2}[/]", string.Join(", ", alreadyInstalled), accent, escapedPath));
        }
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
        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Brand("Complete!"));
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

    // Checks whether fullPath equals adminPath or is a child directory of it.
    // A separate equality check prevents false matches on path prefixes
    // (e.g. "C:\Program Files\dotnet is cool" matching "C:\Program Files\dotnet").
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
