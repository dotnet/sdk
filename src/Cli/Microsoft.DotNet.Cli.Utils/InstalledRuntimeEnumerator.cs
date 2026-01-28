// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Enumerates installed .NET runtimes on the system
/// </summary>
internal static class InstalledRuntimeEnumerator
{
    private const string NetCoreAppFrameworkName = "Microsoft.NETCore.App";

    /// <summary>
    /// Gets all installed .NET Core runtimes
    /// </summary>
    /// <returns>List of installed .NET Core runtime versions</returns>
    public static IEnumerable<NuGetVersion> GetInstalledRuntimes()
    {
        var runtimes = new List<NuGetVersion>();
        
        try
        {
            var dotnetRoot = GetDotnetRoot();
            if (dotnetRoot == null)
            {
                return runtimes;
            }

            var sharedFrameworkPath = Path.Combine(dotnetRoot, "shared", NetCoreAppFrameworkName);
            if (!Directory.Exists(sharedFrameworkPath))
            {
                return runtimes;
            }

            foreach (var versionDir in Directory.GetDirectories(sharedFrameworkPath))
            {
                var versionString = Path.GetFileName(versionDir);
                if (NuGetVersion.TryParse(versionString, out var version))
                {
                    runtimes.Add(version);
                }
            }
        }
        catch
        {
            // If we fail to enumerate runtimes, return empty list
            // This ensures the tool installation continues with default behavior
        }

        return runtimes;
    }

    /// <summary>
    /// Checks if a compatible runtime is available for the given framework requirement
    /// </summary>
    /// <param name="requiredFramework">The framework required by the tool</param>
    /// <param name="allowRollForward">Whether roll-forward is allowed</param>
    /// <returns>True if a compatible runtime is available</returns>
    public static bool IsCompatibleRuntimeAvailable(NuGetFramework requiredFramework, bool allowRollForward = false)
    {
        if (requiredFramework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            // Only check .NET Core runtimes
            return true;
        }

        var installedRuntimes = GetInstalledRuntimes();
        var requiredVersion = requiredFramework.Version;

        foreach (var installedVersion in installedRuntimes)
        {
            // Exact match or higher minor version in same major
            if (installedVersion.Major == requiredVersion.Major)
            {
                if (installedVersion.Minor >= requiredVersion.Minor)
                {
                    return true;
                }
            }
            // If roll-forward is allowed, check for higher major versions
            else if (allowRollForward && installedVersion.Major > requiredVersion.Major)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if allowing roll-forward would help find a compatible runtime
    /// </summary>
    /// <param name="requiredFramework">The framework required by the tool</param>
    /// <returns>True if roll-forward would find a compatible runtime</returns>
    public static bool WouldRollForwardHelp(NuGetFramework requiredFramework)
    {
        if (requiredFramework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            return false;
        }

        var installedRuntimes = GetInstalledRuntimes();
        var requiredVersion = requiredFramework.Version;

        // Check if there's any runtime with a higher major version
        return installedRuntimes.Any(v => v.Major > requiredVersion.Major);
    }

    private static string? GetDotnetRoot()
    {
        // Try to get dotnet root from current process location
        // The SDK is typically in <root>/sdk/<version>, so we go up two levels
        string? rootPath = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)));
        
        if (rootPath != null && Directory.Exists(Path.Combine(rootPath, "shared")))
        {
            return rootPath;
        }

        // Fallback to DOTNET_ROOT environment variable
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (dotnetRoot != null && Directory.Exists(Path.Combine(dotnetRoot, "shared")))
        {
            return dotnetRoot;
        }

        return null;
    }
}
