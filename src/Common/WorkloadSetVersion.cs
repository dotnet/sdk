// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

internal static class WorkloadSetVersion
{
    public static string[] SeparateCoreComponents(string workloadSetVersion, out string[] sections)
    {
        sections = workloadSetVersion.Split(['-', '+'], 2);
        if (sections.Length < 1)
        {
            return [];
        }

        return sections[0].Split('.');
    }

    public static bool IsWorkloadSetPackageVersion(string workloadSetVersion)
    {
        int coreComponentsLength = SeparateCoreComponents(workloadSetVersion, out _).Length;
        return coreComponentsLength >= 3 && coreComponentsLength <= 4;
    }

    /// <summary>
    /// Detects whether a workload set version string appears to be in the internal package version
    /// format (e.g. "10.105.0" or "11.100.0-preview.5") rather than the user-facing format
    /// (e.g. "10.0.105" or "11.0.100-preview.5"). The user-facing format always has 0 as the
    /// second (minor) component.
    /// </summary>
    /// <param name="workloadSetVersion">The version string to inspect.</param>
    /// <param name="suggestedVersion">
    /// When the method returns <see langword="true"/>, contains a corrected version string in the
    /// expected user-facing format; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the version looks like it is in the wrong (package version) format;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public static bool IsWorkloadSetVersionInPackageVersionFormat(string workloadSetVersion, out string? suggestedVersion)
    {
        suggestedVersion = null;
        var coreComponents = SeparateCoreComponents(workloadSetVersion, out var sections);

        // The package version format always has exactly 3 dot-separated numeric components
        if (coreComponents.Length != 3)
        {
            return false;
        }

        // Use System.Version to validate numeric components instead of manual int.TryParse
        if (!Version.TryParse(sections[0], out var parsedVersion))
        {
            return false;
        }

        int major = parsedVersion.Major;
        int minor = parsedVersion.Minor;
        int patch = parsedVersion.Build;

        // The correct workload-set version always has 0 as the second (minor) component.
        // A non-zero minor is the tell-tale sign of the package version format.
        if (minor == 0)
        {
            return false;
        }

        // Re-attach any pre-release or build-metadata suffix, including the leading delimiter
        // ('-' or '+'). Split(['-', '+'], 2) removes the delimiter from sections[0], so the
        // character at sections[0].Length in the original string is the delimiter itself.
        string prereleaseSuffix = sections.Length > 1
            ? workloadSetVersion.Substring(sections[0].Length)
            : string.Empty;

        // In the package version format the layout is: major.sdkPatch.workloadSetPatch(-prerelease)
        // The corrected user-facing format is:         major.0.sdkPatch(.workloadSetPatch)(-prerelease)
        // where the workload-set patch component is omitted when it is 0.
        suggestedVersion = patch == 0
            ? $"{major}.0.{minor}{prereleaseSuffix}"
            : $"{major}.0.{minor}.{patch}{prereleaseSuffix}";

        return true;
    }
}
