// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class DotnetInstallManager : IDotnetInstallManager
{
    private readonly IEnvironmentProvider _environmentProvider;

    public DotnetInstallManager(IEnvironmentProvider? environmentProvider = null)
    {
        _environmentProvider = environmentProvider ?? new EnvironmentProvider();
    }

    public DotnetInstallRootConfiguration? GetConfiguredInstallType()
    {
        string? foundDotnet = _environmentProvider.GetCommandPath("dotnet");
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
                return new(currentInstallRoot, InstallType.Admin, IsFullyConfigured: true);
            }

            // Not fully configured, but PATH resolves to dotnet
            // Determine type based on location using registry-based detection
            var programFilesDotnetPaths = WindowsPathHelper.GetProgramFilesDotnetPaths();
            bool isAdminPath = programFilesDotnetPaths.Any(path =>
                currentInstallRoot.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase));

            return new(currentInstallRoot, isAdminPath ? InstallType.Admin : InstallType.User, IsFullyConfigured: false);
        }
        else
        {
            // For non-Windows platforms, determine based on path location
            bool isAdminInstall = InstallExecutor.IsAdminInstallPath(currentInstallRoot.Path);

            // For now, we consider it fully configured if it's on PATH
            return new(currentInstallRoot, isAdminInstall ? InstallType.Admin : InstallType.User, IsFullyConfigured: true);
        }
    }

    public string GetDefaultDotnetInstallPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dotnet");
    }

    public GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory)
    {
        return GlobalJsonModifier.GetGlobalJsonInfo(initialDirectory);
    }

    public string? GetLatestInstalledAdminVersion()
    {
        var sdkInstalls = GetExistingSystemInstalls()
            .Where(i => i.Component == InstallComponent.SDK)
            .ToList();
        return sdkInstalls.Count > 0 ? sdkInstalls[0].Version.ToString() : null;
    }

    public List<string> GetInstalledAdminSdkVersions()
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
            catch
            {
                // If we can't enumerate installs (e.g., hostfxr not found), skip this path
            }
        }

        // Sort descending so newest versions appear first
        installs.Sort((a, b) => string.Compare(b.Version.ToString(), a.Version.ToString(), StringComparison.OrdinalIgnoreCase));
        return installs;
    }

    /// <summary>
    /// Returns the system-level .NET install directories for the current platform.
    /// Windows: uses the registry to find install locations under Program Files.
    /// macOS: reads /etc/dotnet/install_location, defaults to /usr/local/share/dotnet.
    /// Linux: checks /usr/share/dotnet and /usr/lib/dotnet.
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
            // macOS installer writes the dotnet root here
            const string installLocationFile = "/etc/dotnet/install_location";
            try
            {
                if (File.Exists(installLocationFile))
                {
                    string? location = File.ReadAllText(installLocationFile).Trim();
                    if (!string.IsNullOrEmpty(location) && Directory.Exists(location))
                    {
                        paths.Add(location.TrimEnd(Path.DirectorySeparatorChar));
                    }
                }
            }
            catch
            {
                // Best-effort; file may not exist or be unreadable
            }

            // Default macOS system location when install_location file is missing
            TryAddPath(paths, "/usr/local/share/dotnet");
        }
        else
        {
            // Linux package-manager locations
            TryAddPath(paths, "/usr/share/dotnet");
            TryAddPath(paths, "/usr/lib/dotnet");
        }

        return paths;

        static void TryAddPath(List<string> list, string path)
        {
            if (Directory.Exists(path) && !list.Contains(path, StringComparer.Ordinal))
            {
                list.Add(path);
            }
        }
    }

    public void InstallSdks(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions)
    {
        foreach (var channelVersion in sdkVersions)
        {
            InstallSDK(dotnetRoot, new UpdateChannel(channelVersion));
        }
    }

    private static void InstallSDK(DotnetInstallRoot dotnetRoot, UpdateChannel channel)
    {
        DotnetInstallRequest request = new DotnetInstallRequest(
            dotnetRoot,
            channel,
            InstallComponent.SDK,
            new InstallRequestOptions()
        );

        InstallResult installResult = InstallerOrchestratorSingleton.Instance.Install(request);
        Spectre.Console.AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[{DotnetupTheme.Current.Success}]Installed .NET SDK {installResult.Install.Version} at {installResult.Install.InstallRoot.Path}[/]");
    }

    public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null)
    {
        GlobalJsonModifier.UpdateGlobalJson(globalJsonPath, sdkVersion);
    }

    internal static string? ReplaceGlobalJsonSdkVersion(string jsonText, string newVersion)
    {
        return GlobalJsonModifier.ReplaceGlobalJsonSdkVersion(jsonText, newVersion);
    }

    public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null)
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

                case InstallType.Admin:
                    var adminChanges = installRootManager.GetAdminInstallRootChanges();
                    bool adminSucceeded = InstallRootManager.ApplyAdminInstallRoot(
                        adminChanges,
                        AnsiConsole.WriteLine,
                        msg => AnsiConsole.MarkupLine(DotnetupTheme.Error(msg)));

                    if (!adminSucceeded)
                    {
                        throw new InvalidOperationException("Failed to configure admin install root.");
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
        // Non-Windows platforms: use the simpler PATH-based approach
        // Get current PATH
        var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var pathEntries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        string exeName = "dotnet";
        // Remove only actual dotnet installation folders from PATH
        pathEntries = [.. pathEntries.Where(p => !File.Exists(Path.Combine(p, exeName)))];

        switch (installType)
        {
            case InstallType.User:
                if (string.IsNullOrEmpty(dotnetRoot))
                {
                    throw new ArgumentNullException(nameof(dotnetRoot));
                }
                // Add dotnetRoot to PATH
                pathEntries.Insert(0, dotnetRoot);
                // Set DOTNET_ROOT
                Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot, EnvironmentVariableTarget.User);
                break;
            case InstallType.Admin:
                if (string.IsNullOrEmpty(dotnetRoot))
                {
                    throw new ArgumentNullException(nameof(dotnetRoot));
                }
                // Add dotnetRoot to PATH
                pathEntries.Insert(0, dotnetRoot);
                // Unset DOTNET_ROOT
                Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
                break;
            default:
                throw new ArgumentException($"Unknown install type: {installType}", nameof(installType));
        }
        // Update PATH
        var newPath = string.Join(Path.PathSeparator, pathEntries);
        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
    }
}
