// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace BinaryToolKit;

public static class RemoveBinaries
{
    public static void Execute(IEnumerable<string> binariesToRemove, string targetDirectory)
    {
        Log.LogInformation($"Removing binaries from '{targetDirectory}'...");
        
        foreach (var binary in binariesToRemove)
        {
            File.Delete(Path.Combine(targetDirectory, binary));
            Log.LogDebug($"    Removed '{binary}'");
        }

        Log.LogInformation($"Finished binary removal. Removed {binariesToRemove.Count()} binaries.");
    }
}