// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks;

/// <summary>
/// This task uses the given RID graph in a given SDK to pick the best match from among a set of supported RIDs for the current RID
/// </summary>
public sealed class PickBestRid : TaskBase
{
    /// <summary>
    /// The path to the RID graph to read
    /// </summary>
    [Required]
    public string RuntimeGraphPath { get; set; } = "";

    /// <summary>
    /// The RID to find the best fit for
    /// </summary>
    [Required]
    public string TargetRid { get; set; } = "";

    /// <summary>
    /// All of the RIDs that are allowed to match against the <see cref="TargetRid"/>
    /// </summary>
    [Required]
    public string[] SupportedRids { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The RID from among <see cref="SupportedRids"/> that best matches the <see cref="TargetRid"/>, if any.
    /// </summary>
    [Output]
    public string? MatchingRid { get; set; }

    protected override void ExecuteCore()
    {
        if (!File.Exists(RuntimeGraphPath))
        {
            Log.LogError(Strings.RuntimeGraphFileDoesNotExist, RuntimeGraphPath);
            return;
        }

        RuntimeGraph graph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
        var bestRidForPlatform = NuGetUtils.GetBestMatchingRid(graph, TargetRid, SupportedRids, out bool wasInGraph);

        if (!wasInGraph || bestRidForPlatform == null)
        {
            Log.LogError(Strings.UnableToFindMatchingRid, TargetRid, string.Join(",", SupportedRids), RuntimeGraphPath);
            return;
        }

        MatchingRid = bestRidForPlatform;
    }
}
