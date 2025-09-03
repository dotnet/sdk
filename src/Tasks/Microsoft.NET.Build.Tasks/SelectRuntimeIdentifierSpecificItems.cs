// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
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
        public string? TargetRuntimeIdentifier { get; set; }

        /// <summary>
        /// The list of candidate items to filter.
        /// </summary>
        [Required]
        public ITaskItem[]? Items { get; set; }

        /// <summary>
        /// The name of the MSBuild metadata to check on each item. Defaults to "RuntimeIdentifier".
        /// </summary>
        public string? RuntimeIdentifierItemMetadata { get; set; } = "RuntimeIdentifier";

        /// <summary>
        /// Path to the RuntimeIdentifierGraph file.
        /// </summary>
        [Required]
        public string? RuntimeIdentifierGraphPath { get; set; }

        /// <summary>
        /// The filtered items that are compatible with the target runtime identifier.
        /// </summary>
        [Output]
        public ITaskItem[]? SelectedItems { get; set; }

        protected override void ExecuteCore()
        {
            if (Items == null || Items.Length == 0)
            {
                SelectedItems = Array.Empty<ITaskItem>();
                return;
            }

            string targetRid = TargetRuntimeIdentifier ?? string.Empty;
            string ridGraphPath = RuntimeIdentifierGraphPath ?? string.Empty;
            string ridMetadata = RuntimeIdentifierItemMetadata ?? "RuntimeIdentifier";

            if (string.IsNullOrEmpty(targetRid))
            {
                Log.LogError("TargetRuntimeIdentifier is required but was not provided.");
                return;
            }

            if (string.IsNullOrEmpty(ridGraphPath))
            {
                Log.LogError("RuntimeIdentifierGraphPath is required but was not provided.");
                return;
            }

            RuntimeGraph runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(ridGraphPath);

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
                if (IsCompatibleRuntimeIdentifier(runtimeGraph, targetRid, itemRuntimeIdentifier))
                {
                    selectedItems.Add(item);
                }
            }

            SelectedItems = selectedItems.ToArray();
        }

        /// <summary>
        /// Determines if a candidate runtime identifier is compatible with a target runtime identifier.
        /// </summary>
        /// <param name="runtimeGraph">The runtime graph containing compatibility information.</param>
        /// <param name="targetRuntimeIdentifier">The target runtime identifier.</param>
        /// <param name="candidateRuntimeIdentifier">The candidate runtime identifier to check.</param>
        /// <returns>True if the candidate is compatible with the target, false otherwise.</returns>
        private static bool IsCompatibleRuntimeIdentifier(RuntimeGraph runtimeGraph, string targetRuntimeIdentifier, string candidateRuntimeIdentifier)
        {
            if (string.Equals(targetRuntimeIdentifier, candidateRuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Get the list of compatible runtime identifiers for the target
            var compatibleRuntimeIdentifiers = runtimeGraph.ExpandRuntime(targetRuntimeIdentifier);

            // Check if the candidate runtime identifier is in the list of compatible ones
            return compatibleRuntimeIdentifiers.Contains(candidateRuntimeIdentifier, StringComparer.OrdinalIgnoreCase);
        }
    }
}