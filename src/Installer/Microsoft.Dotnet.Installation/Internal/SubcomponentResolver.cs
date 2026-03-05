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
    /// <summary>Known non-subcomponent folder (e.g., swidtag, .metadata) — expected to be ignored.</summary>
    IgnoredFolder,
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
        ".metadata",
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

        // Normalize to forward slashes and trim trailing slashes
        var normalized = relativeEntryPath.Replace('\\', '/').TrimEnd('/');

        // Split into segments
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            // Root-level file (e.g., "dotnet.exe", "LICENSE.txt")
            result = SubcomponentResolveResult.RootLevelFile;
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
            // Unknown top-level folder — not a recognized subcomponent
            result = SubcomponentResolveResult.UnknownFolder;
            return null;
        }

        if (segments.Length < requiredDepth)
        {
            // Entry is inside a known folder but not deep enough to identify a subcomponent
            result = SubcomponentResolveResult.TooShallow;
            return null;
        }

        // Join the first 'requiredDepth' segments with forward slashes
        result = SubcomponentResolveResult.Resolved;
        return string.Join('/', segments, 0, requiredDepth);
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
