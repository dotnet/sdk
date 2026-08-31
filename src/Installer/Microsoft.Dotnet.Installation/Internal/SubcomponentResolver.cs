// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Result of resolving a subcomponent from a relative path.
/// </summary>
internal enum SubcomponentResolveResult
{
    /// <summary>Resolved successfully to a subcomponent.</summary>
    Resolved,
    /// <summary>Root-level file (e.g., dotnet.exe) — not a subcomponent.</summary>
    RootLevelFile,
    /// <summary>Unknown top-level folder — not a recognized subcomponent area.</summary>
    UnknownFolder,
    /// <summary>Known folder but path is not deep enough to identify a subcomponent.</summary>
    TooShallow,
    /// <summary>
    /// Intermediate directory entry in a known subcomponent hierarchy (e.g., "shared/" or "host/fxr/").
    /// Tar archives (produced on both Linux and Windows) include explicit directory entries
    /// for every level of the hierarchy; zip archives historically omit these intermediate
    /// entries and jump straight to files (e.g., "shared/Microsoft.NETCore.App/9.0.11/System.dll").
    /// See dotnet/sdk#52910 for Windows tar.gz production.
    /// </summary>
    IntermediateDirectory,
    /// <summary>Known non-subcomponent folder (e.g., swidtag, metadata) — expected to be ignored.</summary>
    IgnoredFolder,
    /// <summary>Input resolved to an empty path after normalization (e.g., "/", ".//").</summary>
    EmptyPath,
}

/// <summary>
/// Maps relative file paths from a dotnet root to their subcomponent identifier.
/// A subcomponent is a versioned folder at a known depth under the dotnet root.
/// </summary>
internal static class SubcomponentResolver
{
    // The number of path segments that identify a subcomponent for each known top-level folder.
    private static readonly Dictionary<string, int> s_subcomponentDepthByFolder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["host"] = 3,            // host/fxr/10.0.1
        ["packs"] = 3,           // packs/Microsoft.AspNetCore.App.Ref/10.0.1
        ["sdk"] = 2,             // sdk/10.0.101
        ["sdk-manifests"] = 4,   // sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2
        ["shared"] = 3,          // shared/Microsoft.NETCore.App/10.0.1
        ["templates"] = 2,       // templates/10.0.1
    };

    // Top-level folders that are known but are not subcomponent areas.
    // These are silently skipped during GC and extraction tracking.
    private static readonly HashSet<string> s_ignoredFolders =
    [
with(StringComparer.OrdinalIgnoreCase),
        "metadata",
        "swidtag",
    ];

    /// <summary>
    /// Resolves a relative archive entry path to its subcomponent identifier.
    /// </summary>
    /// <param name="relativeEntryPath">A relative path from the dotnet root (e.g., "sdk/10.0.101/dotnet.dll" or "shared/Microsoft.NETCore.App/10.0.1/System.dll"). May use forward or back slashes.</param>
    /// <param name="result">The result classification of the resolve operation.</param>
    /// <returns>The subcomponent identifier using forward slashes (e.g., "sdk/10.0.101"), or null if the entry cannot be resolved to a subcomponent.</returns>
    public static string? Resolve(string relativeEntryPath, out SubcomponentResolveResult result)
    {
        if (string.IsNullOrEmpty(relativeEntryPath))
        {
            result = SubcomponentResolveResult.RootLevelFile;
            return null;
        }

        var (normalized, isDirectoryEntry) = NormalizeEntryPath(relativeEntryPath);

        // RemoveEmptyEntries ensures inputs like "/" or "//" produce an empty array.
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            // Degenerate input (e.g., "/", ".//") that becomes empty after normalization.
            result = SubcomponentResolveResult.EmptyPath;
            return null;
        }

        var topLevelFolder = segments[0];

        if (s_ignoredFolders.Contains(topLevelFolder))
        {
            result = SubcomponentResolveResult.IgnoredFolder;
            return null;
        }

        if (!s_subcomponentDepthByFolder.TryGetValue(topLevelFolder, out int requiredDepth))
        {
            // Single-segment path that isn't a known folder (e.g., "dotnet.exe", "LICENSE.txt")
            if (segments.Length < 2)
            {
                result = SubcomponentResolveResult.RootLevelFile;
                return null;
            }

            // Unknown top-level folder — not a recognized subcomponent area
            result = SubcomponentResolveResult.UnknownFolder;
            return null;
        }

        if (segments.Length < requiredDepth)
        {
            // Directory entries (e.g., "shared/", "host/fxr/") are expected intermediate
            // parts of the hierarchy and get a distinct classification.
            result = isDirectoryEntry
                ? SubcomponentResolveResult.IntermediateDirectory
                : SubcomponentResolveResult.TooShallow;
            return null;
        }

        // Join the first 'requiredDepth' segments with forward slashes
        result = SubcomponentResolveResult.Resolved;
        return string.Join('/', segments, 0, requiredDepth);
    }

    /// <summary>
    /// Normalizes an archive entry path: converts backslashes to forward slashes,
    /// strips leading "./" prefixes (common in tar archives), and detects/trims
    /// trailing directory separators.
    /// </summary>
    /// <remarks>
    /// Both tar and zip formats use forward slashes in entry names (the ZIP
    /// specification requires it), so the trailing-'/' directory detection is
    /// cross-platform. Backslashes are normalized first for robustness.
    /// </remarks>
    private static (string normalized, bool isDirectoryEntry) NormalizeEntryPath(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        bool isDirectoryEntry = normalized.EndsWith('/', StringComparison.Ordinal);
        return (normalized.TrimEnd('/'), isDirectoryEntry);
    }

    /// <summary>
    /// Resolves a relative archive entry path to its subcomponent identifier.
    /// Convenience overload that discards the result classification.
    /// </summary>
    public static string? Resolve(string relativeEntryPath)
    {
        return Resolve(relativeEntryPath, out _);
    }

    /// <summary>
    /// Gets the subcomponent depth for a known top-level folder name.
    /// </summary>
    public static bool TryGetDepth(string topLevelFolderName, out int depth)
    {
        return s_subcomponentDepthByFolder.TryGetValue(topLevelFolderName, out depth);
    }

    /// <summary>
    /// Checks if a top-level folder name is a known non-subcomponent folder that should be ignored.
    /// </summary>
    public static bool IsIgnoredFolder(string topLevelFolderName)
    {
        return s_ignoredFolders.Contains(topLevelFolderName);
    }
}
