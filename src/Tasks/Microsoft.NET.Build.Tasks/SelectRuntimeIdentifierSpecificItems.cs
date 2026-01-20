// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks;

/// <summary>
/// MSBuild task that filters a set of Items by matching on compatible RuntimeIdentifier.
/// This task filters an Item list by those items that contain a specific Metadata that is
/// compatible with a specified Runtime Identifier, according to a given RuntimeIdentifierGraph file.
/// </summary>
public class SelectRuntimeIdentifierSpecificItems : TaskBase
{
    /// <summary>
    /// The target runtime identifier to check compatibility against.
    /// </summary>
    [Required]
    public string TargetRuntimeIdentifier { get; set; } = null!;

    /// <summary>
    /// The list of candidate items to filter.
    /// </summary>
    [Required]
    public ITaskItem[] Items { get; set; } = null!;

    /// <summary>
    /// The name of the MSBuild metadata to check on each item. Defaults to "RuntimeIdentifier".
    /// </summary>
    public string? RuntimeIdentifierItemMetadata { get; set; } = "RuntimeIdentifier";

    /// <summary>
    /// Path to the RuntimeIdentifierGraph file.
    /// </summary>
    [Required]
    public string RuntimeIdentifierGraphPath { get; set; } = null!;

    /// <summary>
    /// The filtered items that are compatible with the <see cref="TargetRuntimeIdentifier"/>
    /// </summary>
    [Output]
    public ITaskItem[]? SelectedItems { get; set; }

    protected override void ExecuteCore()
    {
        if (Items.Length == 0)
        {
            SelectedItems = Array.Empty<ITaskItem>();
            return;
        }

        string ridMetadata = RuntimeIdentifierItemMetadata ?? "RuntimeIdentifier";

        RuntimeGraph runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeIdentifierGraphPath);

        var selectedItems = new List<ITaskItem>();

        foreach (var item in Items)
        {
            string? itemRuntimeIdentifier = item.GetMetadata(ridMetadata);

            if (string.IsNullOrEmpty(itemRuntimeIdentifier))
            {
                // Item doesn't have the runtime identifier metadata, skip it
                continue;
            }

            // Check if the item's runtime identifier is compatible with the target runtime identifier
            if (runtimeGraph.AreCompatible(TargetRuntimeIdentifier, itemRuntimeIdentifier))
            {
                selectedItems.Add(item);
            }
        }

        SelectedItems = selectedItems.ToArray();
    }
}
