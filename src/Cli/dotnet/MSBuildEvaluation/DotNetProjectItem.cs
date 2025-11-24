// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Microsoft.DotNet.Cli.MSBuildEvaluation;

/// <summary>
/// Provides typed access to project items from MSBuild evaluation, execution, construction.
/// This is a wrapper around ProjectItemInstance that provides a cleaner API.
/// </summary>
public sealed class DotNetProjectItem
{
    private readonly ProjectItem? _evalItem;
    private readonly ProjectItemInstance? _executionItem;
    private readonly ProjectItemElement? _constructionItem;

    public DotNetProjectItem(ProjectItem item)
    {
        _evalItem = item;
    }

    public DotNetProjectItem(ProjectItemInstance item)
    {
        _executionItem = item;
    }

    public DotNetProjectItem(ProjectItemElement item)
    {
        _constructionItem = item;
    }

    /// <summary>
    /// Gets the type of this item (e.g., "PackageReference", "ProjectReference", "Compile").
    /// </summary>
    public string ItemType =>
        _evalItem is not null ? _evalItem.ItemType :
        _executionItem is not null ? _executionItem.ItemType :
        _constructionItem is not null ? _constructionItem.ItemType :
        throw new UnreachableException();

    /// <summary>
    /// Gets the evaluated include value of this item.
    /// </summary>
    public string EvaluatedInclude =>
        _evalItem is not null ? _evalItem.EvaluatedInclude :
        _executionItem is not null ? _executionItem.EvaluatedInclude :
        _constructionItem is not null ? _constructionItem.Include :
        throw new UnreachableException();

    /// <summary>
    /// Gets the project file that this item came from.
    /// </summary>
    public string? ProjectFile =>
        _evalItem is not null ? _evalItem.Project.FullPath :
        _executionItem is not null ? _executionItem.Project.FullPath :
        _constructionItem is not null ? _constructionItem.ContainingProject.FullPath :
        throw new UnreachableException();


    /// <summary>
    /// Gets the full path of this item, if available.
    /// </summary>
    public string? FullPath => GetMetadataValue("FullPath");

    /// <summary>
    /// Cached provenance (location) information for this item.
    /// </summary>
    private ProvenanceResult? _provenance => _evalItem?.Project.GetItemProvenance(_evalItem.UnevaluatedInclude, ItemType).LastOrDefault();

    /// <summary>
    /// Gets the value of the specified metadata, or null if the metadata doesn't exist.
    /// </summary>
    /// <param name="metadataName">The name of the metadata to retrieve.</param>
    /// <returns>The metadata value, or null if not found.</returns>
    public string? GetMetadataValue(string metadataName)
    {
        if (string.IsNullOrEmpty(metadataName))
        {
            return null;
        }

        return _evalItem is not null ? _evalItem.GetMetadataValue(metadataName) : _executionItem?.GetMetadataValue(metadataName);
    }

    /// <summary>
    /// Gets all metadata names for this item.
    /// </summary>
    /// <returns>An enumerable of metadata names.</returns>
    public IEnumerable<string> GetMetadataNames() {
        if (_evalItem is not null)
        {
            if (_evalItem.MetadataCount == 0)
            {
                return [];
            }
            return _evalItem.Metadata.Select(m => m.Name);
        }
        else if (_executionItem is not null)
        {
            if (_executionItem.MetadataCount == 0)
            {
                return [];
            }
            return _executionItem.MetadataNames;
        }
        else if (_constructionItem is not null)
        {
            if (_constructionItem.Metadata.Count == 0)
            {
                return [];
            }
            return _constructionItem.Metadata.Select(m => m.Name);
        }
        throw new UnreachableException();
    }

    /// <summary>
    /// Gets all metadata as a dictionary.
    /// </summary>
    /// <returns>A dictionary of metadata names and values.</returns>
    public IDictionary<string, string> GetMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string metadataName in GetMetadataNames())
        {
            metadata[metadataName] = GetMetadataValue(metadataName) ?? "";
        }
        return metadata;
    }

    /// <summary>
    /// Returns a string representation of this item.
    /// </summary>
    public override string ToString() => $"{ItemType}: {EvaluatedInclude}";

    /// <summary>
    /// Updates this item's metadata value in the source project file, and saves the file after the modification
    /// </summary>
    /// <param name="attributeName"></param>
    /// <param name="value"></param>
    public UpdateSourceItemResult UpdateSourceItem(string attributeName, string? value)
    {
        if (_evalItem is not null)
        {
            var sourceItem = _provenance?.ItemElement;
            if (sourceItem == null)
            {
                return UpdateSourceItemResult.SourceItemNotFound;
            }

            var versionAttribute = sourceItem?.Metadata.FirstOrDefault(i => i.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase));
            if (versionAttribute == null)
            {
                return UpdateSourceItemResult.MetadataNotFound;
            }

            versionAttribute.Value = value;
            _evalItem.Project.Save();

            return UpdateSourceItemResult.Success;
        }
        else if (_executionItem is not null)
        {
            // ProjectItemInstance is read-only, so we cannot update it directly.
            return UpdateSourceItemResult.SourceItemNotFound;
        }
        else if (_constructionItem is not null)
        {
            var versionAttribute = _constructionItem.Metadata.FirstOrDefault(i => i.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase));
            if (versionAttribute == null)
            {
                return UpdateSourceItemResult.MetadataNotFound;
            }

            versionAttribute.Value = value;
            _constructionItem.ContainingProject.Save();
            return UpdateSourceItemResult.Success;
        }
        throw new UnreachableException();
    }

    public enum UpdateSourceItemResult
    {
        Success,
        MetadataNotFound,
        SourceItemNotFound
    }
}
