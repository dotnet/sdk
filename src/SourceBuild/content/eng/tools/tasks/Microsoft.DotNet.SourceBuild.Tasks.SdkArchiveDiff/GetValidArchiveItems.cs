// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

public class GetValidArchiveItems : Microsoft.Build.Utilities.Task
{
    [Required]
    public required ITaskItem[] ArchiveItems { get; init; }

    [Required]
    public required string ArchiveName { get; init; }

    [Output]
    public ITaskItem[] ValidArchiveItems { get; set; } = [];

    public override bool Execute()
    {
        List<ITaskItem> archiveItems = new();
        foreach (var item in ArchiveItems)
        {
            var filename = Path.GetFileName(item.ItemSpec);
            try
            {
                // Ensure the version and RID info can be parsed from the item
                _ = Archive.GetInfoFromFileName(filename, ArchiveName);
                archiveItems.Add(item);
            }
            catch (ArgumentException e)
            {
                Log.LogMessage($"'{item.ItemSpec}' is not a valid archive name: '{e.Message}'");
                continue;
            }
        }
        switch (archiveItems.Count)
        {
            case 0:
                Log.LogMessage(MessageImportance.High, "No valid archive items found");
                ValidArchiveItems = [];
                return false;
            case 1:
                Log.LogMessage(MessageImportance.High, $"{archiveItems[0]} is the only valid archive item found");
                ValidArchiveItems = archiveItems.ToArray();
                break;
            default:
                archiveItems.Sort((a, b) => a.ItemSpec.Length - b.ItemSpec.Length);
                Log.LogMessage(MessageImportance.High, $"Multiple valid archive items found: '{string.Join("', '", archiveItems)}'");
                ValidArchiveItems = archiveItems.ToArray();
                break;
        }
        return true;
    }

}
