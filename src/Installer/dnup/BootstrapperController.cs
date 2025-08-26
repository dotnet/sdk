// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class BootstrapperController : IBootstrapperController
{
    private readonly IEnvironmentProvider _environmentProvider;

    public BootstrapperController(IEnvironmentProvider? environmentProvider = null)
    {
        _environmentProvider = environmentProvider ?? new EnvironmentProvider();
    }

    public InstallType GetConfiguredInstallType(out string? currentInstallPath)
    {
        currentInstallPath = null;
        string? foundDotnet = _environmentProvider.GetCommandPath("dotnet");
        if (string.IsNullOrEmpty(foundDotnet))
        {
            return InstallType.None;
        }

        string installDir = Path.GetDirectoryName(foundDotnet)!;
        currentInstallPath = installDir;

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        bool isAdminInstall = installDir.StartsWith(Path.Combine(programFiles, "dotnet"), StringComparison.OrdinalIgnoreCase)
            || installDir.StartsWith(Path.Combine(programFilesX86, "dotnet"), StringComparison.OrdinalIgnoreCase);

        if (isAdminInstall)
        {
            // Admin install: DOTNET_ROOT should not be set, or if set, should match installDir
            if (!string.IsNullOrEmpty(dotnetRoot) && !PathsEqual(dotnetRoot, installDir) && !dotnetRoot.StartsWith(Path.Combine(programFiles, "dotnet"), StringComparison.OrdinalIgnoreCase) && !dotnetRoot.StartsWith(Path.Combine(programFilesX86, "dotnet"), StringComparison.OrdinalIgnoreCase))
            {
                return InstallType.Inconsistent;
            }
            return InstallType.Admin;
        }
        else
        {
            // User install: DOTNET_ROOT must be set and match installDir
            if (string.IsNullOrEmpty(dotnetRoot) || !PathsEqual(dotnetRoot, installDir))
            {
                return InstallType.Inconsistent;
            }
            return InstallType.User;
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                            StringComparison.OrdinalIgnoreCase);
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

    public void InstallSdks(string dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions)
    {
        // TODO: Implement proper channel version resolution and parameter mapping
        DotnetInstallRequest request = new DotnetInstallRequest(
            "TODO_CHANNEL_VERSION",
            dotnetRoot,
            InstallType.User,
            InstallMode.SDK,
            InstallArchitecture.x64
        );

        InstallerOrchestratorSingleton.Instance.Install(request);
    }
    public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null, bool? allowPrerelease = null, string? rollForward = null) => throw new NotImplementedException();

    public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null)
    {
        // Get current PATH
        var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var pathEntries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        string exeName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
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
            case InstallType.None:
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
