// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Provides build information extracted from assembly metadata.
/// </summary>
public static class BuildInfo
{
    private static readonly Lazy<(string Version, string CommitSha)> s_buildInfo = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        return ParseInformationalVersion(informationalVersion);
    });

    /// <summary>
    /// Gets the version string (e.g., "1.0.0").
    /// </summary>
    public static string Version => s_buildInfo.Value.Version;

    /// <summary>
    /// Gets the short commit SHA (7 characters, e.g., "abc123d").
    /// Returns "unknown" if not available.
    /// </summary>
    public static string CommitSha => s_buildInfo.Value.CommitSha;

    /// <summary>
    /// Parses the informational version string to extract version and commit SHA.
    /// </summary>
    /// <param name="informationalVersion">The informational version string (e.g., "1.0.0+abc123def").</param>
    /// <returns>A tuple of (version, commitSha).</returns>
    internal static (string Version, string CommitSha) ParseInformationalVersion(string informationalVersion)
    {
        // Format: "1.0.0+abc123d" or just "1.0.0"
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex > 0)
        {
            var version = informationalVersion.Substring(0, plusIndex);
            var commit = informationalVersion.Substring(plusIndex + 1);
            // Truncate commit to 7 characters (git's standard short SHA)
            if (commit.Length > 7)
            {
                commit = commit.Substring(0, 7);
            }
            return (version, commit);
        }

        return (informationalVersion, "unknown");
    }
}
