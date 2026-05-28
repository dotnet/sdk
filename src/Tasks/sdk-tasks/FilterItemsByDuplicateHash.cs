// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Filters a set of candidate files to only those whose content hash matches at least one file
/// in a reference set. 
/// </summary>
public sealed class FilterItemsByDuplicateHash : Task
{
    /// <summary>
    /// The candidate files to filter.
    /// </summary>
    [Required]
    public ITaskItem[] CandidateFiles { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// The reference files to compare against. These are not modified.
    /// </summary>
    [Required]
    public ITaskItem[] ReferenceFiles { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Output: the subset of CandidateFiles whose content hash does NOT match any ReferenceFile.
    /// </summary>
    [Output]
    public ITaskItem[] UnmatchedFiles { get; set; } = Array.Empty<ITaskItem>();

    public override bool Execute()
    {
        // Hash all reference files
        var referenceHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in ReferenceFiles)
        {
            try
            {
                referenceHashes.Add(FileHasher.ComputeFileHash(item.ItemSpec));
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to hash reference file '{item.ItemSpec}': {ex.Message}");
            }
        }

        Log.LogMessage(MessageImportance.Normal, $"Hashed {referenceHashes.Count} unique reference files.");

        var unmatched = new List<ITaskItem>();
        foreach (var item in CandidateFiles)
        {
            try
            {
                var hash = FileHasher.ComputeFileHash(item.ItemSpec);
                if (!referenceHashes.Contains(hash))
                {
                    unmatched.Add(item);
                    Log.LogMessage(MessageImportance.Normal, $"  Unmatched: {item.ItemSpec}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to hash candidate file '{item.ItemSpec}': {ex.Message}");
            }
        }

        UnmatchedFiles = unmatched.ToArray();

        Log.LogMessage(MessageImportance.High,
            $"FilterItemsByDuplicateHash: {unmatched.Count} of {CandidateFiles.Length} candidates are unique.");

        return true;
    }
}
#endif
