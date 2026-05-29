// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli;

/// <summary>
///  Resolves the .NET installation root and hostfxr library path.
///  Dependencies are injected for testability.
/// </summary>
internal static class DotnetRootResolver
{
    /// <summary>
    ///  Resolves the .NET installation root directory, mimicking muxer behavior.
    /// </summary>
    internal static string ResolveDotnetRoot(
        Func<string, string?> getEnvVar,
        string? processPath,
        Architecture processArch,
        bool isWindows,
        Func<string, bool> directoryExists,
        Func<string, bool> fileExists,
        string baseDirectory)
    {
        // Check DOTNET_ROOT first (standard on all platforms)
        string? dotnetRoot = getEnvVar("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot) && directoryExists(dotnetRoot))
        {
            return dotnetRoot;
        }

        // On Windows, also check the architecture-specific variant
        if (isWindows)
        {
            string archVar = processArch switch
            {
                Architecture.X64 => "DOTNET_ROOT(x64)",
                Architecture.X86 => "DOTNET_ROOT(x86)",
                Architecture.Arm64 => "DOTNET_ROOT(ARM64)",
                _ => ""
            };

            if (!string.IsNullOrEmpty(archVar))
            {
                dotnetRoot = getEnvVar(archVar);
                if (!string.IsNullOrEmpty(dotnetRoot) && directoryExists(dotnetRoot))
                {
                    return dotnetRoot;
                }
            }
        }

        // Fall back to resolving from the process path
        if (processPath is not null)
        {
            string? processDir = Path.GetDirectoryName(processPath);
            if (processDir is not null)
            {
                // Walk up looking for a directory with dotnet(.exe)
                string? candidate = processDir;
                while (candidate is not null)
                {
                    if (fileExists(Path.Combine(candidate, "dotnet" + (isWindows ? ".exe" : ""))))
                    {
                        return candidate;
                    }
                    candidate = Path.GetDirectoryName(candidate);
                }
            }
        }

        // Last resort: assume relative to baseDirectory
        return Path.GetDirectoryName(baseDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? baseDirectory;
    }

    /// <summary>
    ///  Finds the hostfxr library path under the given .NET root.
    /// </summary>
    internal static string ResolveHostfxrPath(
        string dotnetRoot,
        bool isWindows,
        bool isMacOS,
        Func<string, bool> directoryExists,
        Func<string, string[]> getDirectories,
        Func<string, bool> fileExists)
    {
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        if (!directoryExists(fxrDir))
        {
            return string.Empty;
        }

        // Pick the highest version directory by parsing version numbers
        string? latestFxr = getDirectories(fxrDir)
            .Select(path => new
            {
                Path = path,
                Version = Version.TryParse(Path.GetFileName(path), out Version? version) ? version : null
            })
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();

        if (latestFxr is null)
        {
            return string.Empty;
        }

        string hostfxrName = isWindows
            ? "hostfxr.dll"
            : isMacOS
                ? "libhostfxr.dylib"
                : "libhostfxr.so";

        string hostfxrPath = Path.Combine(latestFxr, hostfxrName);
        return fileExists(hostfxrPath) ? hostfxrPath : string.Empty;
    }
}
