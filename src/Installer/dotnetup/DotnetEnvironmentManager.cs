// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Central manager for discovering and configuring the environment.
/// Responsibilities:
/// - Detecting the current install type (user vs. system/admin) via PATH and registry.
/// - Resolving the default dotnetup-managed install path.
/// - Enumerating existing system-level .NET installs across platforms (Windows registry,
///   macOS /etc/dotnet/install_location files, Linux well-known paths).
/// - Delegating SDK installation and global.json management.
/// </summary>
internal class DotnetEnvironmentManager : IDotnetEnvironmentManager
{

    public DotnetEnvironmentManager()
    {
    }

    public DotnetInstallRootConfiguration? GetCurrentPathConfiguration()
    {
        var environmentProvider = new EnvironmentProvider();
        string? foundDotnet = environmentProvider.GetCommandPath("dotnet");
        if (string.IsNullOrEmpty(foundDotnet))
        {
            return null;
        }

        var currentInstallRoot = new DotnetInstallRoot(Path.GetDirectoryName(foundDotnet)!, InstallerUtilities.GetDefaultInstallArchitecture());

        // Use InstallRootManager to determine if the install is fully configured
        if (OperatingSystem.IsWindows())
        {
            var installRootManager = new InstallRootManager(this);

            // Check if user install root is fully configured
            var userChanges = installRootManager.GetUserInstallRootChanges();
            if (!userChanges.NeedsChange() && DotnetupUtilities.PathsEqual(currentInstallRoot.Path, userChanges.UserDotnetPath))
            {
                return new(currentInstallRoot, InstallType.User, IsFullyConfigured: true);
            }

            // Check if admin install root is fully configured
            var adminChanges = installRootManager.GetAdminInstallRootChanges();
            if (!adminChanges.NeedsChange())
            {
                return new(currentInstallRoot, InstallType.System, IsFullyConfigured: true);
            }

            // Not fully configured, but PATH resolves to dotnet
            // Determine type based on location using registry-based detection
            var programFilesDotnetPaths = WindowsPathHelper.GetProgramFilesDotnetPaths();
            bool isAdminPath = programFilesDotnetPaths.Any(path =>
                currentInstallRoot.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase));

            return new(currentInstallRoot, isAdminPath ? InstallType.System : InstallType.User, IsFullyConfigured: false);
        }
        else
        {
            // For non-Windows platforms, determine based on path location
            bool isAdminInstall = InstallPathClassifier.IsAdminInstallPath(currentInstallRoot.Path);

            // For now, we consider it fully configured if it's on PATH
            return new(currentInstallRoot, isAdminInstall ? InstallType.System : InstallType.User, IsFullyConfigured: true);
        }
    }

    public string GetDefaultDotnetInstallPath()
    {
        return DotnetupPaths.DefaultDotnetInstallPath;
    }

    public string? GetLatestInstalledSystemVersion()
    {
        var sdkInstalls = GetExistingSystemInstalls()
            .Where(i => i.Component == InstallComponent.SDK)
            .ToList();
        return sdkInstalls.Count > 0 ? sdkInstalls[0].Version.ToString() : null;
    }

    public List<string> GetInstalledSystemSdkVersions()
    {
        return [.. GetExistingSystemInstalls()
            .Where(i => i.Component == InstallComponent.SDK)
            .Select(i => i.Version.ToString())];
    }

    public List<DotnetInstall> GetExistingSystemInstalls()
    {
        var installs = new List<DotnetInstall>();

        foreach (var systemPath in GetSystemDotnetPaths())
        {
            try
            {
                installs.AddRange(HostFxrWrapper.getInstalls(systemPath));
            }
            catch (Exception ex)
            {
                // Log the failure rather than silently swallowing — aids debugging
                // when hostfxr is missing or the path is inaccessible.
                Activity.Current?.SetTag(TelemetryTagNames.HostfxrEnumerationFailed, true);
                Activity.Current?.SetTag(TelemetryTagNames.HostfxrEnumerationError, ex.GetType().Name);
                Activity.Current?.SetTag(TelemetryTagNames.HostfxrEnumerationPath, systemPath);
                AnsiConsole.MarkupLine(DotnetupTheme.Dim(
                    $"[{DotnetupTheme.Current.Warning}]Warning:[/] Could not enumerate installs at {systemPath.EscapeMarkup()}: {ex.Message.EscapeMarkup()}"));
            }
        }

        return FilterToNativeArchAndSort(installs);
    }

    /// <summary>
    /// Filters a list of installs to only the native architecture and sorts descending by version.
    /// Shared between production code and test mocks to ensure consistent behavior.
    /// </summary>
    public static List<DotnetInstall> FilterToNativeArchAndSort(List<DotnetInstall> installs)
    {
        var nativeArch = InstallerUtilities.GetDefaultInstallArchitecture();
        var filtered = installs
            .Where(i => i.InstallRoot.Architecture == nativeArch)
            .ToList();

        // Sort descending so newest versions appear first
        filtered.Sort((a, b) => string.Compare(b.Version.ToString(), a.Version.ToString(), StringComparison.OrdinalIgnoreCase));
        return filtered;
    }

    /// <summary>
    /// Returns the system-level .NET install directories for the current platform.
    /// Windows: reads registry (sharedhost\Path, then InstallLocation) per architecture,
    ///          falls back to %ProgramFiles%\dotnet.
    /// macOS: checks /etc/dotnet/install_location_{arch}, /etc/dotnet/install_location,
    ///        defaults to /usr/local/share/dotnet (plus /usr/local/share/dotnet/x64 under Rosetta).
    /// Linux: checks /usr/lib/dotnet, /usr/share/dotnet, /usr/lib64/dotnet.
    ///
    /// See https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md
    /// See https://github.com/dotnet/runtime/issues/109974
    /// </summary>
    internal static List<string> GetSystemDotnetPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsPathHelper.GetProgramFilesDotnetPaths();
        }

        var paths = new List<string>();

        if (OperatingSystem.IsMacOS())
        {
            AddMacOSPaths(paths);
        }
        else
        {
            AddLinuxPaths(paths);
        }

        return paths;
    }

    /// <summary>
    /// Adds macOS system dotnet paths in priority order:
    /// 1. Per-architecture install_location file: /etc/dotnet/install_location_{arch}
    /// 2. Default install_location file: /etc/dotnet/install_location
    /// 3. Default system location: /usr/local/share/dotnet
    /// 4. x64 emulation sublocation: /usr/local/share/dotnet/x64 (when running x64 on arm64 via Rosetta)
    /// </summary>
    private static void AddMacOSPaths(List<string> paths)
    {
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        // Per-architecture install_location file takes highest priority.
        // See: https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md
        TryReadInstallLocationFile(paths, $"/etc/dotnet/install_location_{arch}");

        // Fall back to the non-architecture-specific file.
        TryReadInstallLocationFile(paths, "/etc/dotnet/install_location");

        // Default macOS system location (the .NET macOS installer places files here).
        TryAddPath(paths, "/usr/local/share/dotnet");

        // When running x64 under Rosetta on an arm64 Mac, the x64 dotnet root
        // is a subdirectory rather than a sibling directory.
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
            RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            TryAddPath(paths, "/usr/local/share/dotnet/x64");
        }
    }

    /// <summary>
    /// Adds Linux system dotnet paths in priority order:
    /// 1. /usr/lib/dotnet — preferred by newer distros and the .NET install docs.
    /// 2. /usr/share/dotnet — used by some distributions (e.g., Ubuntu packages).
    /// 3. /usr/lib64/dotnet — used by some RPM-based distributions (Fedora, RHEL).
    ///
    /// Note: /etc/ld.so.conf and /etc/ld.so.conf.d/* configure the dynamic linker
    /// and may reference dotnet library paths on some distributions. The well-known
    /// directories above cover the standard package-manager install locations.
    /// </summary>
    private static void AddLinuxPaths(List<string> paths)
    {
        TryAddPath(paths, "/usr/lib/dotnet");
        TryAddPath(paths, "/usr/share/dotnet");
        TryAddPath(paths, "/usr/lib64/dotnet");
    }

    /// <summary>
    /// Reads a dotnet install_location file and adds the path it contains if valid.
    /// These files are written by the .NET installer on macOS and contain a single
    /// line with the dotnet root directory.
    /// </summary>
    private static void TryReadInstallLocationFile(List<string> paths, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            string? location = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrEmpty(location))
            {
                var normalized = location.TrimEnd(Path.DirectorySeparatorChar);
                if (Directory.Exists(normalized) && !paths.Contains(normalized, StringComparer.Ordinal))
                {
                    paths.Add(normalized);
                }
            }
        }
        catch
        {
            // Best-effort; file may be unreadable due to permissions
        }
    }

    private static void TryAddPath(List<string> paths, string path)
    {
        if (Directory.Exists(path) && !paths.Contains(path, StringComparer.Ordinal))
        {
            paths.Add(path);
        }
    }
    public void ApplyEnvironmentModifications(InstallType installType, string? dotnetRoot = null)
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, use InstallRootManager for proper configuration
            var installRootManager = new InstallRootManager(this);

            switch (installType)
            {
                case InstallType.User:
                    if (string.IsNullOrEmpty(dotnetRoot))
                    {
                        throw new ArgumentNullException(nameof(dotnetRoot));
                    }

                    var userChanges = installRootManager.GetUserInstallRootChanges();
                    bool succeeded = InstallRootManager.ApplyUserInstallRoot(
                        userChanges,
                        AnsiConsole.WriteLine,
                        msg => AnsiConsole.MarkupLine(DotnetupTheme.Error(msg)));

                    if (!succeeded)
                    {
                        throw new InvalidOperationException("Failed to configure user install root.");
                    }
                    break;

                case InstallType.System:
                    var adminChanges = installRootManager.GetAdminInstallRootChanges();
                    bool adminSucceeded = InstallRootManager.ApplyAdminInstallRoot(
                        adminChanges,
                        AnsiConsole.WriteLine,
                        msg => AnsiConsole.MarkupLine(DotnetupTheme.Error(msg)));

                    if (!adminSucceeded)
                    {
                        throw new InvalidOperationException("Failed to configure system install root.");
                    }
                    break;

                default:
                    throw new ArgumentException($"Unknown install type: {installType}", nameof(installType));
            }
        }
        else
        {
            ConfigureInstallTypeUnix(installType, dotnetRoot);
        }
    }

    private static void ConfigureInstallTypeUnix(InstallType installType, string? dotnetRoot)
    {
        var dotnetupPath = Environment.ProcessPath
            ?? throw new DotnetInstallException(DotnetInstallErrorCode.Unknown, "Unable to determine the dotnetup executable path.");

        IEnvShellProvider? shellProvider = ShellDetection.GetCurrentShellProvider();
        if (shellProvider is null)
        {
            var shellEnv = Environment.GetEnvironmentVariable("SHELL") ?? "(not set)";
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                $"Unable to detect a supported shell. SHELL={shellEnv}. Supported shells: {string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName))}");
        }

        switch (installType)
        {
            case InstallType.User:
                if (string.IsNullOrEmpty(dotnetRoot))
                {
                    throw new ArgumentNullException(nameof(dotnetRoot));
                }
                ShellProfileManager.AddProfileEntries(shellProvider, dotnetupPath, dotnetInstallPath: dotnetRoot);
                break;
            case InstallType.System:
                ShellProfileManager.AddProfileEntries(shellProvider, dotnetupPath, dotnetupOnly: true);
                break;
            default:
                throw new ArgumentException($"Unknown install type: {installType}", nameof(installType));
        }
    }

    /// <inheritdoc />
    public void ApplyGlobalJsonModifications(IReadOnlyList<ResolvedInstallRequest> requests)
    {
        foreach (var request in requests)
        {
            string? globalJsonPath = request.Request.Options.GlobalJsonPath;
            if (globalJsonPath is not null && request.Request.Component == InstallComponent.SDK)
            {
                GlobalJsonModifier.UpdateGlobalJson(globalJsonPath, request.ResolvedVersion.ToString());
            }
        }
    }
}
