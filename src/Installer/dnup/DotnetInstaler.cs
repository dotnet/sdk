// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class DotnetInstaler : IDotnetInstaller
{
    public SdkInstallType GetConfiguredInstallType(out string? currentInstallPath)
    {
        currentInstallPath = null;
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return SdkInstallType.None;
        }

        string exeName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string[] paths = pathEnv.Split(Path.PathSeparator);
        string? foundDotnet = null;
        foreach (var dir in paths)
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(candidate))
                {
                    foundDotnet = Path.GetFullPath(candidate);
                    break;
                }
            }
            catch { }
        }

        if (foundDotnet == null)
        {
            return SdkInstallType.None;
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
                return SdkInstallType.Inconsistent;
            }
            return SdkInstallType.Admin;
        }
        else
        {
            // User install: DOTNET_ROOT must be set and match installDir
            if (string.IsNullOrEmpty(dotnetRoot) || !PathsEqual(dotnetRoot, installDir))
            {
                return SdkInstallType.Inconsistent;
            }
            return SdkInstallType.User;
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
    public GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory) => throw new NotImplementedException();
    public string? GetLatestInstalledAdminVersion()
    {
        // TODO: Implement this
        return null;
    }
}
