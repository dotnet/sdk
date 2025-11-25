// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable RS0030 // OK to use MSBuild APIs in this wrapper file.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.MSBuildEvaluation;

/// <summary>
/// Provides typed access to project properties and items from MSBuild evaluation.
/// This is a wrapper around ProjectInstance that provides a cleaner API with strongly-typed
/// access to common properties used by dotnet CLI commands.
/// </summary>
public sealed class DotNetProject(Project Project)
{
    private readonly Dictionary<string, DotNetProjectItem[]> _itemCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets an underlying ProjectInstance for advanced scenarios.
    /// DO NOT CALL THIS generally.
    /// </summary>
    /// <returns></returns>
    public ProjectInstance Instance() => Project.CreateProjectInstance();

    /// <summary>
    /// Gets the full path to the project file.
    /// </summary>
    public string? FullPath => Project.FullPath;

    /// <summary>
    /// Gets the directory containing the project file.
    /// </summary>
    public string? Directory => Path.GetDirectoryName(FullPath);

    // Strongly-typed access to common properties

    /// <summary>
    /// Gets the target framework for the project (e.g., "net8.0").
    /// </summary>
    public NuGetFramework? TargetFramework => GetPropertyValue("TargetFramework") is string tf ? NuGetFramework.Parse(tf) : null;

    /// <summary>
    /// Gets all target frameworks for multi-targeting projects.
    /// </summary>
    public NuGetFramework[]? TargetFrameworks => GetPropertyValues("TargetFrameworks")?.Select(NuGetFramework.Parse).ToArray();

    public string? RuntimeIdentifier => GetPropertyValue("RuntimeIdentifier");

    public string[]? RuntimeIdentifiers => GetPropertyValues("RuntimeIdentifiers");

    /// <summary>
    /// Gets the configuration (e.g., "Debug", "Release").
    /// </summary>
    public string Configuration => GetPropertyValue("Configuration") ?? "Debug";

    /// <summary>
    /// Gets the platform (e.g., "AnyCPU", "x64").
    /// </summary>
    public string Platform => GetPropertyValue("Platform") ?? "AnyCPU";

    /// <summary>
    /// Gets the output type (e.g., "Exe", "Library").
    /// </summary>
    public string OutputType => GetPropertyValue("OutputType") ?? "";

    /// <summary>
    /// Gets the output path for build artifacts.
    /// </summary>
    public string? OutputPath => GetPropertyValue("OutputPath");

    /// <summary>
    /// Gets the project assets file path (used by NuGet).
    /// </summary>
    public string? ProjectAssetsFile => GetPropertyValue("ProjectAssetsFile");

    /// <summary>
    /// Gets whether this project is packable.
    /// </summary>
    public bool IsPackable => string.Equals(GetPropertyValue("IsPackable"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this project uses central package management.
    /// </summary>
    public bool ManagePackageVersionsCentrally => string.Equals(GetPropertyValue("ManagePackageVersionsCentrally"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the value of the specified property, or null if the property doesn't exist.
    /// </summary>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>The property value, or null if not found.</returns>
    public string? GetPropertyValue(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return null;
        }

        return Project.GetPropertyValue(propertyName);
    }

    /// <summary>
    /// Gets the values of a property that contains multiple semicolon-separated values.
    /// </summary>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>An array of property values.</returns>
    public string[]? GetPropertyValues(string propertyName)
    {
        var value = GetPropertyValue(propertyName);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Gets all items of the specified type.
    /// </summary>
    /// <param name="itemType">The type of items to retrieve (e.g., "PackageReference", "ProjectReference").</param>
    /// <returns>An enumerable of project items.</returns>
    public IEnumerable<DotNetProjectItem> GetItems(string itemType)
    {
        if (string.IsNullOrEmpty(itemType))
        {
            return [];
        }

        if (!_itemCache.TryGetValue(itemType, out var cachedItems))
        {
            cachedItems = Project.GetItems(itemType)
                .Select(item => new DotNetProjectItem(item))
                .ToArray();
            _itemCache[itemType] = cachedItems;
        }

        return cachedItems;
    }

    /// <summary>
    /// Tries to find a single item of the specified type with the given include specification. If multiple are found, the 'last' one is returned.
    /// </summary>
    /// <param name="itemType">The type of item to find.</param>
    /// <param name="includeSpec">The include specification to match.</param>
    /// <returns>The found project item, or null if not found.</returns>
    private bool TryFindItem(string itemType, string includeSpec,  [NotNullWhen(true)] out DotNetProjectItem? item)
    {
        item = GetItems(itemType).LastOrDefault(i => string.Equals(i.EvaluatedInclude, includeSpec, StringComparison.OrdinalIgnoreCase));
        return item != null;
    }

    /// <summary>
    /// Tries to get a PackageVersion item for the specified package ID.
    /// </summary>
    public bool TryGetPackageVersion(string packageId, [NotNullWhen(true)] out DotNetProjectItem? item) =>
        TryFindItem("PackageVersion", packageId, out item);

    public IEnumerable<DotNetProjectItem> ProjectReferences => GetItems("ProjectReference");

    /// <summary>
    /// Tries to add a new item to the project. The item will be added in the first item group that
    /// contains items of the same type, or a new item group will be created if none exist.
    /// </summary>
    public bool TryAddItem(string itemType, string includeSpec, Dictionary<string, string?>? metadata, [NotNullWhen(true)] out DotNetProjectItem? item)
    {
        var hostItemGroup =
            Project.Xml.ItemGroups
            .Where(e => e.Items.Any(i => string.Equals(i.ItemType, itemType, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault()
            ?? Project.Xml.AddItemGroup();

        var rawItem = hostItemGroup.AddItem(itemType, includeSpec, metadata);
        item = new DotNetProjectItem(rawItem);
        return true;
    }

    /// <summary>
    /// Gets all available configurations for this project.
    /// </summary>
    public string[] Configurations => GetPropertyValue("Configurations") is string foundConfig
        ? foundConfig
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .DefaultIfEmpty("Debug")
            .ToArray()
        : ["Debug", "Release"];

    /// <summary>
    /// Gets all available platforms for this project.
    /// </summary>
    public IEnumerable<string> GetPlatforms()
    {
        return (GetPropertyValue("Platforms") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .DefaultIfEmpty("AnyCPU");
    }

    /// <summary>
    /// Gets a unique identifier for this project.
    /// </summary>
    public string GetProjectId()
    {
        var projectGuidProperty = GetPropertyValue("ProjectGuid");
        var projectGuid = string.IsNullOrEmpty(projectGuidProperty)
            ? Guid.NewGuid()
            : new Guid(projectGuidProperty);
        return projectGuid.ToString("B").ToUpper();
    }

    public string? GetProjectTypeGuid()
    {
        string? projectTypeGuid = GetPropertyValue("ProjectTypeGuids");
        if (!string.IsNullOrEmpty(projectTypeGuid))
        {
            var firstGuid = projectTypeGuid.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstGuid))
            {
                return firstGuid;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the default project type GUID for this project.
    /// </summary>
    public string? GetDefaultProjectTypeGuid()
    {
        string? projectTypeGuid = GetPropertyValue("DefaultProjectTypeGuid");
        if (string.IsNullOrEmpty(projectTypeGuid) && (FullPath?.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase) ?? true))
        {
            projectTypeGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";
        }
        return projectTypeGuid;
    }

    /// <summary>
    /// Gets whether this project is an executable project.
    /// </summary>
    public bool IsExecutable => string.Equals(OutputType, "Exe", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(OutputType, "WinExe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a string representation of this project.
    /// </summary>
    public override string ToString() => FullPath ?? "<unnamed project>";

    /// <summary>
    /// Builds the project with the specified targets and loggers. Delegates to the underlying ProjectInstance directly.
    /// </summary>
    /// <remarks>
    /// NO ONE SHOULD BE CALLING THIS except the <see cref="DotNetProjectBuilder"/>.
    /// </remarks>
    public bool Build(ReadOnlySpan<string> targets, IEnumerable<Build.Framework.ILogger>? loggers, IEnumerable<Build.Logging.ForwardingLoggerRecord>? remoteLoggers, out IDictionary<string, TargetResult> targetOutputs)
    {
        return Instance().Build
        (
            targets: targets.ToArray(),
            loggers: loggers,
            remoteLoggers: remoteLoggers,
            targetOutputs: out targetOutputs
        );
    }

    /// <summary>
    /// Evaluates the provided string by expanding items and properties, as if it was found at the very end of the project file. This is useful for some hosts for which this kind of best-effort evaluation is sufficient. Does not expand bare metadata expressions.
    /// </summary>
    public string ExpandString(string unexpandedValue) => Project.ExpandString(unexpandedValue);

    public bool SupportsTarget(string targetName) => Project.Targets.ContainsKey(targetName);
}
