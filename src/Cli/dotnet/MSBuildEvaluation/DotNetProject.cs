// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable RS0030 // OK to use MSBuild APIs in this wrapper file.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
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
    /// Used by raw-XML-manipulation scenarios, for example adding itemgroups/conditions.
    /// </summary>
    private readonly ProjectRootElement _projectRootElement = Project.Xml;

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
    public override string ToString() => FullPath ?? "<virtual project>";

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

    public enum AddType
    {
        Added,
        AlreadyExists
    }
    public record struct ItemAddResult(List<(string Include, AddType AddType)> AddResult);

    /// <summary>
    /// Adds items of the specified type to the project, optionally conditioned on a target framework.
    /// The items are added inside the first item group that contains only items of the same type for
    /// the specified framework, or a new item group is created if none exist that satisfy that condition.
    /// </summary>
    /// <returns>An ItemAddResult containing lists of existing and added items</returns>
    public ItemAddResult AddItemsOfType(string itemType, IEnumerable<(string include, Dictionary<string, string>? metadata)> itemsToAdd, string? tfmForCondition = null)
    {
        List<(string, AddType)> addResult = new();

        var itemGroup = _projectRootElement.FindUniformOrCreateItemGroupWithCondition(
            itemType,
            tfmForCondition);
        foreach (var itemData in itemsToAdd)
        {
            var normalizedItemRef = itemData.include.Replace('/', '\\');
            if (_projectRootElement.HasExistingItemWithCondition(tfmForCondition, normalizedItemRef))
            {
                addResult.Add((itemData.include, AddType.AlreadyExists));
                continue;
            }

            var itemElement = _projectRootElement.CreateItemElement(itemType, normalizedItemRef);
            if (itemData.metadata != null)
            {
                foreach (var metadata in itemData.metadata)
                {
                    itemElement.AddMetadata(metadata.Key, metadata.Value);
                }
            }
            itemGroup.AppendChild(itemElement);
            addResult.Add((itemData.include, AddType.Added));
        }
        _projectRootElement.Save();
        Project.ReevaluateIfNecessary();
        return new(addResult);
    }

    public enum RemoveType
    {
        Removed,
        NotFound
    }

    public record struct ItemRemoveResult(List<(string Include, DotNetProject.RemoveType RemoveType)> RemoveResult);

    public ItemRemoveResult RemoveItemsOfType(string itemType, IEnumerable<string> itemsToRemove, string? tfmForCondition = null)
    {
        List<(string, RemoveType)> removeResult = new();
        foreach (var itemData in itemsToRemove)
        {
            var normalizedItemRef = itemData.Replace('/', '\\');
            var existingItems = _projectRootElement.FindExistingItemsWithCondition(tfmForCondition, normalizedItemRef);
            if (existingItems.Any())
            {
                foreach (var existingItem in existingItems)
                {
                    ProjectElementContainer itemGroupParent = existingItem.Parent;
                    itemGroupParent.RemoveChild(existingItem);
                    if (itemGroupParent.Children.Count == 0)
                    {
                        itemGroupParent.Parent.RemoveChild(itemGroupParent);
                    }
                    removeResult.Add((itemData, RemoveType.Removed));
                }
                continue;
            }
            else
            {
                removeResult.Add((itemData, RemoveType.NotFound));
            }
        }
        _projectRootElement.Save();
        Project.ReevaluateIfNecessary();
        return new(removeResult);
    }
}

/// <summary>
/// These extension methods are located here to make project file manipulation easier, but _should not_ leak out of the context of the DotNetProject.
/// </summary>
file static class MSBuildProjectExtensions
{
    public static bool IsConditionalOnFramework(this ProjectElement el, string? framework)
    {
        if (!TryGetFrameworkConditionString(framework, out string? conditionStr))
        {
            return el.ConditionChain().Count == 0;
        }

        var condChain = el.ConditionChain();
        return condChain.Count == 1 && condChain.First().Trim() == conditionStr;
    }

    public static ISet<string> ConditionChain(this ProjectElement projectElement)
    {
        var conditionChainSet = new HashSet<string>();

        if (!string.IsNullOrEmpty(projectElement.Condition))
        {
            conditionChainSet.Add(projectElement.Condition);
        }

        foreach (var parent in projectElement.AllParents)
        {
            if (!string.IsNullOrEmpty(parent.Condition))
            {
                conditionChainSet.Add(parent.Condition);
            }
        }

        return conditionChainSet;
    }

    public static ProjectItemGroupElement? LastItemGroup(this ProjectRootElement root)
    {
        return root.ItemGroupsReversed.FirstOrDefault();
    }

    public static ProjectItemGroupElement FindUniformOrCreateItemGroupWithCondition(this ProjectRootElement root, string projectItemElementType, string? framework)
    {
        var lastMatchingItemGroup = FindExistingUniformItemGroupWithCondition(root, projectItemElementType, framework);

        if (lastMatchingItemGroup != null)
        {
            return lastMatchingItemGroup;
        }

        ProjectItemGroupElement ret = root.CreateItemGroupElement();
        if (TryGetFrameworkConditionString(framework, out string? condStr))
        {
            ret.Condition = condStr;
        }

        root.InsertAfterChild(ret, root.LastItemGroup());
        return ret;
    }

    public static ProjectItemGroupElement? FindExistingUniformItemGroupWithCondition(this ProjectRootElement root, string projectItemElementType, string? framework)
    {
        return root.ItemGroupsReversed.FirstOrDefault((itemGroup) => itemGroup.IsConditionalOnFramework(framework) && itemGroup.IsUniformItemElementType(projectItemElementType));
    }

    public static bool IsUniformItemElementType(this ProjectItemGroupElement group, string projectItemElementType)
    {
        return group.Items.All((it) => it.ItemType == projectItemElementType);
    }

    public static IEnumerable<ProjectItemElement> FindExistingItemsWithCondition(this ProjectRootElement root, string? framework, string include)
    {
        return root.Items.Where((el) => el.IsConditionalOnFramework(framework) && el.HasInclude(include));
    }

    public static bool HasExistingItemWithCondition(this ProjectRootElement root, string? framework, string include)
    {
        return root.FindExistingItemsWithCondition(framework, include).Count() != 0;
    }

    public static IEnumerable<ProjectItemElement> GetAllItemsWithElementType(this ProjectRootElement root, string projectItemElementType)
    {
        return root.Items.Where((it) => it.ItemType == projectItemElementType);
    }

    public static bool HasInclude(this ProjectItemElement el, string include)
    {
        include = NormalizedForComparison(include);
        foreach (var i in el.Includes())
        {
            if (include == NormalizedForComparison(i))
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<string> Includes(
        this ProjectItemElement item)
    {
        return SplitSemicolonDelimitedValues(item.Include);
    }

    private static IEnumerable<string> SplitSemicolonDelimitedValues(string combinedValue)
    {
        return string.IsNullOrEmpty(combinedValue) ? Enumerable.Empty<string>() : combinedValue.Split(';');
    }


    private static bool TryGetFrameworkConditionString(string? framework, out string? condition)
    {
        if (string.IsNullOrEmpty(framework))
        {
            condition = null;
            return false;
        }

        condition = $"'$(TargetFramework)' == '{framework}'";
        return true;
    }

    public static string NormalizedForComparison(this string include) => include.ToLower().Replace('/', '\\');
}

