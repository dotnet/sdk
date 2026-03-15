// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
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
        // TODO: Implement this
        return null;
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
        Spectre.Console.AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]Installed .NET SDK {installResult.Install.Version}, available via {installResult.Install.InstallRoot}[/]");
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
                        msg => AnsiConsole.MarkupLine($"[red]{msg}[/]"));

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
                        msg => AnsiConsole.MarkupLine($"[red]{msg}[/]"));

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
            // Non-Windows platforms: persist environment via shell profiles only.
            // Environment.SetEnvironmentVariable with EnvironmentVariableTarget.User
            // has no real persistent store on Unix, so shell profile entries are the
            // sole persistence mechanism.

            switch (installType)
            {
                case InstallType.User:
                    if (string.IsNullOrEmpty(dotnetRoot))
                    {
                        throw new ArgumentNullException(nameof(dotnetRoot));
                    }

                    // Persist to shell profiles
                    var dotnetupPath = Environment.ProcessPath;
                    var shellProvider = ShellDetection.GetCurrentShellProvider();
                    if (dotnetupPath is not null && shellProvider is not null)
                    {
                        ShellProfileManager.AddProfileEntries(shellProvider, dotnetupPath);
                    }
                    break;
                case InstallType.Admin:
                    // Replace shell profile entries with dotnetup-only (no DOTNET_ROOT or dotnet PATH)
                    var adminDotnetupPath = Environment.ProcessPath;
                    var adminShellProvider = ShellDetection.GetCurrentShellProvider();
                    if (adminDotnetupPath is not null && adminShellProvider is not null)
                    {
                        ShellProfileManager.AddProfileEntries(adminShellProvider, adminDotnetupPath, dotnetupOnly: true);
                    }
                    break;
                default:
                    throw new ArgumentException($"Unknown install type: {installType}", nameof(installType));
            }
        }
    }
}
