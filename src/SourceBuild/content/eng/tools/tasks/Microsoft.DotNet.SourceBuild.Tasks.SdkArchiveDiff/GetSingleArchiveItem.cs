// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

public class GetSingleArchiveItem : Microsoft.Build.Utilities.Task
{
    [Required]
    public required ITaskItem[] SdkArchiveItems { get; init; }

    [Output]
    public string BestSdkArchiveItem { get; set; } = "";

    public override bool Execute()
    {
        List<string> archiveItems = new ();
        foreach(var item in SdkArchiveItems)
        {
            try
            {
                // Ensure the version and RID info can be parsed from the item
                _ = Archive.GetInfoFromArchivePath(item.ItemSpec);
                archiveItems.Add(item.ItemSpec);
            }
            catch (ArgumentException e)
            {
                Log.LogMessage(MessageImportance.High, e.Message);
                continue;
            }
        }
        switch (archiveItems.Count){
            case 0:
                Log.LogMessage(MessageImportance.High, "No valid archive items found");
                BestSdkArchiveItem = "";
                break;
            case 1:
                Log.LogMessage(MessageImportance.High, $"{archiveItems[0]} is the only valid archive item found");
                BestSdkArchiveItem = archiveItems[0];
                break;
            default:
                archiveItems.Sort((a,b) => a.Length - b.Length);
                Log.LogMessage(MessageImportance.High, $"Multiple valid archive items found: '{string.Join("', '", archiveItems)}'");
                BestSdkArchiveItem = archiveItems[0];
                Log.LogMessage(MessageImportance.High, $"Choosing '{BestSdkArchiveItem}");
                break;
        }
        return true;
    }

}
