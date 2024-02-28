// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

public class GetSingleTarballItem : Microsoft.Build.Utilities.Task
{
    [Required]
    public required ITaskItem[] SdkTarballItems { get; init; }

    [Output]
    public string BestSdkTarballItem { get; set; } = "";

    public override bool Execute()
    {
        List<string> tarballItems = new ();
        foreach(var item in SdkTarballItems)
        {
            try
            {
                var (versionString, rid, extension) = Archive.GetInfoFromArchivePath(item.ItemSpec);
                tarballItems.Add(item.ItemSpec);
            }
            catch (ArgumentException e)
            {
                Log.LogMessage(MessageImportance.High, e.Message);
                continue;
            }
        }
        switch (tarballItems.Count){
            case 0:
                Log.LogMessage(MessageImportance.High, "No valid tarball items found");
                BestSdkTarballItem = "";
                break;
            case 1:
                Log.LogMessage(MessageImportance.High, $"{tarballItems[0]} is the only valid tarball item found");
                BestSdkTarballItem = tarballItems[0];
                break;
            default:
                tarballItems.Sort((a,b) => a.Length - b.Length);
                Log.LogMessage(MessageImportance.High, $"Multiple valid tarball items found: '{string.Join("', '", tarballItems)}'");
                BestSdkTarballItem = tarballItems[0];
                Log.LogMessage(MessageImportance.High, $"Choosing '{BestSdkTarballItem}");
                break;
        }
        return true;
    }

}
