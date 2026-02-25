// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Maps relative file paths from a dotnet root to their subcomponent identifier.
/// A subcomponent is a versioned folder at a known depth under the dotnet root.
/// </summary>
internal static class SubcomponentResolver
{
    // The number of path segments that identify a subcomponent for each known top-level folder.
    private static readonly Dictionary<string, int> SubcomponentDepthByFolder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sdk"] = 2,             // sdk/10.0.101
        ["shared"] = 3,          // shared/Microsoft.NETCore.App/10.0.1
        ["host"] = 3,            // host/fxr/10.0.1
        ["packs"] = 3,           // packs/Microsoft.AspNetCore.App.Ref/10.0.1
        ["templates"] = 2,       // templates/10.0.1
        ["sdk-manifests"] = 4,   // sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2
    };

    /// <summary>
    /// Resolves a relative archive entry path to its subcomponent identifier.
    /// </summary>
    /// <param name="relativeEntryPath">A relative path from the dotnet root (e.g., "sdk/10.0.101/dotnet.dll" or "shared/Microsoft.NETCore.App/10.0.1/System.dll"). May use forward or back slashes.</param>
    /// <returns>The subcomponent identifier using forward slashes (e.g., "sdk/10.0.101"), or null if the entry is a root-level file or belongs to an unknown folder.</returns>
    public static string? Resolve(string relativeEntryPath)
    {
        if (string.IsNullOrEmpty(relativeEntryPath))
        {
            return null;
        }

        // Normalize to forward slashes and trim trailing slashes
        var normalized = relativeEntryPath.Replace('\\', '/').TrimEnd('/');

        // Split into segments
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            // Root-level file (e.g., "dotnet.exe", "LICENSE.txt")
            return null;
        }

        var topLevelFolder = segments[0];
        if (!SubcomponentDepthByFolder.TryGetValue(topLevelFolder, out int requiredDepth))
        {
            // Unknown top-level folder — not a recognized subcomponent
            return null;
        }

        if (segments.Length < requiredDepth)
        {
            // Entry is inside a known folder but not deep enough to identify a subcomponent
            return null;
        }

        // Join the first 'requiredDepth' segments with forward slashes
        return string.Join('/', segments, 0, requiredDepth);
    }

    /// <summary>
    /// Gets the subcomponent depth for a known top-level folder name.
    /// </summary>
    public static bool TryGetDepth(string topLevelFolderName, out int depth)
    {
        return SubcomponentDepthByFolder.TryGetValue(topLevelFolderName, out depth);
    }
}
