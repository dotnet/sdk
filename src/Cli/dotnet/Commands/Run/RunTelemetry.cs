// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Provides telemetry functionality for dotnet run command.
/// </summary>
internal static class RunTelemetry
{
    private const string RunEventName = "run";

    /// <summary>
    /// Sends telemetry for a dotnet run operation.
    /// </summary>
    /// <param name="isFileBased">True if this is a file-based app run, false for project-based</param>
    /// <param name="projectIdentifier">Obfuscated unique project identifier</param>
    /// <param name="launchProfile">The launch profile name if specified</param>
    /// <param name="noLaunchProfile">True if --no-launch-profile was specified</param>
    /// <param name="launchSettings">The applied launch settings model if any</param>
    /// <param name="sdkCount">Number of SDKs used (defaults to 1)</param>
    /// <param name="packageReferenceCount">Number of PackageReferences (defaults to 0)</param>
    /// <param name="projectReferenceCount">Number of ProjectReferences (defaults to 0)</param>
    /// <param name="additionalPropertiesCount">Number of additional properties (file-based only, defaults to 0)</param>
    /// <param name="usedMSBuild">Whether MSBuild was used (file-based only)</param>
    /// <param name="usedRoslynCompiler">Whether Roslyn compiler was used directly (file-based only)</param>
    public static void TrackRunEvent(
        bool isFileBased,
        string projectIdentifier,
        string? launchProfile = null,
        bool noLaunchProfile = false,
        ProjectLaunchSettingsModel? launchSettings = null,
        int sdkCount = 1,
        int packageReferenceCount = 0,
        int projectReferenceCount = 0,
        int additionalPropertiesCount = 0,
        bool? usedMSBuild = null,
        bool? usedRoslynCompiler = null)
    {
        var properties = new Dictionary<string, string?>
        {
            ["app_type"] = isFileBased ? "file_based" : "project_based",
            ["project_id"] = projectIdentifier,
            ["sdk_count"] = sdkCount.ToString(),
            ["package_reference_count"] = packageReferenceCount.ToString(),
            ["project_reference_count"] = projectReferenceCount.ToString(),
        };

        // Launch profile telemetry
        if (noLaunchProfile)
        {
            properties["launch_profile_requested"] = "none";
        }
        else if (!string.IsNullOrEmpty(launchProfile))
        {
            properties["launch_profile_requested"] = "explicit";
            properties["launch_profile_is_default"] = IsDefaultProfile(launchProfile) ? "true" : "false";
        }
        else if (launchSettings != null)
        {
            properties["launch_profile_requested"] = "default_used";
            properties["launch_profile_is_default"] = IsDefaultProfile(launchSettings.LaunchProfileName) ? "true" : "false";
        }
        else
        {
            properties["launch_profile_requested"] = "false";
        }

        // File-based app specific telemetry
        if (isFileBased)
        {
            properties["additional_properties_count"] = additionalPropertiesCount.ToString();
            if (usedMSBuild.HasValue)
            {
                properties["used_msbuild"] = usedMSBuild.Value ? "true" : "false";
            }
            if (usedRoslynCompiler.HasValue)
            {
                properties["used_roslyn_compiler"] = usedRoslynCompiler.Value ? "true" : "false";
            }
        }

        TelemetryEventEntry.TrackEvent(RunEventName, properties, measurements: null);
    }

    /// <summary>
    /// Generates an obfuscated unique identifier for a project-based app.
    /// </summary>
    /// <param name="projectFilePath">Full path to the project file</param>
    /// <param name="repoRoot">Repository root path if available</param>
    /// <param name="hasher">Optional hasher function, defaults to Sha256Hasher.Hash</param>
    /// <returns>Hashed project identifier</returns>
    public static string GetProjectBasedIdentifier(string projectFilePath, string? repoRoot = null, Func<string, string>? hasher = null)
    {
        // Use relative path from repo root if available, otherwise use full path
        string pathToHash = repoRoot != null && projectFilePath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(repoRoot, projectFilePath)
            : projectFilePath;

        return (hasher ?? Sha256Hasher.Hash)(pathToHash.ToLowerInvariant());
    }

    /// <summary>
    /// Generates an obfuscated unique identifier for a file-based app.
    /// This leverages the same caching infrastructure identifier already used.
    /// </summary>
    /// <param name="entryPointFilePath">Full path to the entry point file</param>
    /// <param name="hasher">Optional hasher function, defaults to Sha256Hasher.Hash</param>
    /// <returns>Hashed file identifier</returns>
    public static string GetFileBasedIdentifier(string entryPointFilePath, Func<string, string>? hasher = null)
    {
        // Use the configured telemetry hasher
        return (hasher ?? Sha256Hasher.Hash)(entryPointFilePath.ToLowerInvariant());
    }

    /// <summary>
    /// Counts the number of SDKs used in a file-based app.
    /// </summary>
    /// <param name="directives">Directives for file-based apps</param>
    /// <returns>Number of SDKs</returns>
    public static int CountSdks(ImmutableArray<CSharpDirective> directives)
    {
        if (directives.IsDefaultOrEmpty)
        {
            return 1; // Default assumption - Microsoft.NET.Sdk
        }

        // File-based: count SDK directives
        var sdkDirectives = directives.OfType<CSharpDirective.Sdk>().Count();
        // If no explicit SDK directives, there's still the default Microsoft.NET.Sdk
        return sdkDirectives > 0 ? sdkDirectives : 1;
    }

    /// <summary>
    /// Counts the number of PackageReferences in a file-based app.
    /// </summary>
    /// <param name="directives">Directives for file-based apps</param>
    /// <returns>Number of package references</returns>
    public static int CountPackageReferences(ImmutableArray<CSharpDirective> directives)
    {
        if (directives.IsDefaultOrEmpty)
        {
            return 0;
        }

        // File-based: count package directives
        return directives.OfType<CSharpDirective.Package>().Count();
    }

    /// <summary>
    /// Counts the number of direct ProjectReferences in a file-based app.
    /// </summary>
    /// <param name="directives">Directives for file-based apps</param>
    /// <returns>Number of project references</returns>
    public static int CountProjectReferences(ImmutableArray<CSharpDirective> directives)
    {
        if (directives.IsDefaultOrEmpty)
        {
            return 0;
        }

        // File-based: count project directives
        return directives.OfType<CSharpDirective.Project>().Count();
    }

    /// <summary>
    /// Counts the number of PackageReferences in a project-based app.
    /// </summary>
    /// <param name="project">Project instance for project-based apps</param>
    /// <returns>Number of package references</returns>
    public static int CountPackageReferences(ProjectInstance project)
    {
        return project.GetItems("PackageReference").Count;
    }

    /// <summary>
    /// Counts the number of direct ProjectReferences in a project-based app.
    /// </summary>
    /// <param name="project">Project instance for project-based apps</param>
    /// <returns>Number of project references</returns>
    public static int CountProjectReferences(ProjectInstance project)
    {
        return project.GetItems("ProjectReference").Count;
    }

    /// <summary>
    /// Counts the number of additional properties for file-based apps.
    /// </summary>
    /// <param name="directives">Directives for file-based apps</param>
    /// <returns>Number of additional properties</returns>
    public static int CountAdditionalProperties(ImmutableArray<CSharpDirective> directives)
    {
        if (directives.IsDefaultOrEmpty)
        {
            return 0;
        }

        return directives.OfType<CSharpDirective.Property>().Count();
    }

    /// <summary>
    /// Determines if a launch profile name is one of the default ones.
    /// </summary>
    /// <param name="profileName">The profile name to check</param>
    /// <returns>True if it's a default profile name</returns>
    private static bool IsDefaultProfile(string? profileName)
    {
        if (string.IsNullOrEmpty(profileName))
        {
            return false;
        }

        // The default profile name at this point is "(Default)"
        return profileName.Equals("(Default)", StringComparison.OrdinalIgnoreCase);
    }
}