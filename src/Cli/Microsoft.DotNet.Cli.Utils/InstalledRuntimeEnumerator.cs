// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.NativeWrapper;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Enumerates installed .NET runtimes on the system using hostfxr
/// </summary>
internal static class InstalledRuntimeEnumerator
{
    private const string NetCoreAppFrameworkName = "Microsoft.NETCore.App";

    /// <summary>
    /// Checks if a tool can run with the installed runtimes by using hostfxr to resolve frameworks
    /// </summary>
    /// <param name="runtimeConfigPath">Path to the tool's runtimeconfig.json file</param>
    /// <returns>True if the tool can run with installed runtimes</returns>
    public static bool CanResolveFrameworks(string runtimeConfigPath)
    {
        if (!File.Exists(runtimeConfigPath))
        {
            return false;
        }

        try
        {
            var bundleProvider = new NETBundlesNativeWrapper();
            return bundleProvider.CanResolveFrameworks(runtimeConfigPath);
        }
        catch
        {
            // If hostfxr call fails, return false
            return false;
        }
    }

    /// <summary>
    /// Gets all installed .NET Core runtimes using hostfxr_get_dotnet_environment_info
    /// </summary>
    /// <returns>List of installed .NET Core runtime versions</returns>
    public static IEnumerable<NuGetVersion> GetInstalledRuntimes()
    {
        var runtimes = new List<NuGetVersion>();
        
        try
        {
            // Get the dotnet executable directory to pass to hostfxr
            var muxer = new Muxer();
            var dotnetPath = Path.GetDirectoryName(muxer.MuxerPath);
            
            // Use hostfxr to enumerate runtimes
            var bundleProvider = new NETBundlesNativeWrapper();
            var envInfo = bundleProvider.GetDotnetEnvironmentInfo(dotnetPath ?? string.Empty);

            // Filter to only Microsoft.NETCore.App runtimes and convert to NuGetVersion
            foreach (var runtime in envInfo.RuntimeInfo)
            {
                if (runtime.Name == NetCoreAppFrameworkName)
                {
                    if (NuGetVersion.TryParse(runtime.Version.ToString(), out var version))
                    {
                        runtimes.Add(version);
                    }
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
}
