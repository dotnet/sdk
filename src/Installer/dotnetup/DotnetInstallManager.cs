// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        string? directory = initialDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            string globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                using var stream = File.OpenRead(globalJsonPath);
                var contents = JsonSerializer.Deserialize(
                    stream,
                    GlobalJsonContentsJsonContext.Default.GlobalJsonContents);
                return new GlobalJsonInfo
                {
                    GlobalJsonPath = globalJsonPath,
                    GlobalJsonContents = contents
                };
            }
            var parent = Directory.GetParent(directory);
            if (parent == null)
                break;
            directory = parent.FullName;
        }
        return new GlobalJsonInfo();
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
            InstallSDK(dotnetRoot, progressContext, new UpdateChannel(channelVersion));
        }
    }

    private void InstallSDK(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, UpdateChannel channnel)
    {
        DotnetInstallRequest request = new DotnetInstallRequest(
            dotnetRoot,
            channnel,
            InstallComponent.SDK,
            new InstallRequestOptions()
        );

        InstallResult installResult = InstallerOrchestratorSingleton.Instance.Install(request);
        if (installResult.Install == null)
        {
            throw new Exception($"Failed to install .NET SDK {channnel.Name}");
        }
        else
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[green]Installed .NET SDK {installResult.Install.Version}, available via {installResult.Install.InstallRoot}[/]");
        }
    }

    public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null, bool? allowPrerelease = null, string? rollForward = null) => throw new NotImplementedException();

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
                        throw new ArgumentNullException(nameof(dotnetRoot));

                    var userChanges = installRootManager.GetUserInstallRootChanges();
                    bool succeeded = installRootManager.ApplyUserInstallRoot(
                        userChanges,
                        msg => AnsiConsole.WriteLine(msg),
                        msg => AnsiConsole.MarkupLine($"[red]{msg}[/]"));

                    if (!succeeded)
                    {
                        throw new InvalidOperationException("Failed to configure user install root.");
                    }
                    break;

                case InstallType.Admin:
                    var adminChanges = installRootManager.GetAdminInstallRootChanges();
                    bool adminSucceeded = installRootManager.ApplyAdminInstallRoot(
                        adminChanges,
                        msg => AnsiConsole.WriteLine(msg),
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
            // Non-Windows platforms: use the simpler PATH-based approach
            // Get current PATH
            var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            var pathEntries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
            string exeName = "dotnet";
            // Remove only actual dotnet installation folders from PATH
            pathEntries = pathEntries.Where(p => !File.Exists(Path.Combine(p, exeName))).ToList();

            switch (installType)
            {
                case InstallType.User:
                    if (string.IsNullOrEmpty(dotnetRoot))
                        throw new ArgumentNullException(nameof(dotnetRoot));
                    // Add dotnetRoot to PATH
                    pathEntries.Insert(0, dotnetRoot);
                    // Set DOTNET_ROOT
                    Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot, EnvironmentVariableTarget.User);
                    break;
                case InstallType.Admin:
                    if (string.IsNullOrEmpty(dotnetRoot))
                        throw new ArgumentNullException(nameof(dotnetRoot));
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
}
