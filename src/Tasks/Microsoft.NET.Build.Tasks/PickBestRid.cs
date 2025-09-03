// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// This task uses the given RID graph in a given SDK to pick the best match from among a set of supported RIDs for the current RID
    /// </summary>
    public sealed class PickBestRid : TaskBase
    {
        /// <summary>
        /// The path to the RID graph to read
        /// </summary>
        [Required]
        public string RuntimeGraphPath { get; set; }

        /// <summary>
        /// The RID to find the best fit for
        /// </summary>
        [Required]
        public string CurrentRid { get; set; }

        /// <summary>
        /// All of the RIDs that are allowed to match against the Current RID
        /// </summary>
        [Required]
        public string[] SupportedRids { get; set; }

        /// <summary>
        /// The solution to the puzzle
        /// </summary>
        [Output]
        public string MatchingRid { get; set; }

        /// <summary>
        /// Computes the thing
        /// </summary>
        protected override void ExecuteCore()
        {
            if (!File.Exists(RuntimeGraphPath))
            {
                Log.LogError(Strings.RuntimeGraphFileDoesNotExist, RuntimeGraphPath);
                return;
            }

            RuntimeGraph graph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            string bestRidForPlatform = NuGetUtils.GetBestMatchingRid(graph, CurrentRid, SupportedRids, out bool wasInGraph);

            if (!wasInGraph || bestRidForPlatform == null)
            {
                Log.LogError(Strings.UnableToFindMatchingRid, CurrentRid, string.Join(",", SupportedRids), RuntimeGraphPath);
                return;
            }

            MatchingRid = bestRidForPlatform;
        }
    }
}