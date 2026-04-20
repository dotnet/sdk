// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class UpdateServiceWorkerFileWithVersion : Task
{
    [Required]
    public string ServiceWorkerSource { get; set; }

    [Required]
    public string ServiceWorkerDestination { get; set; }

    [Required]
    public string ManifestVersion { get; set; }

    public override bool Execute()
    {
        if(!File.Exists(ServiceWorkerSource))
        {
            Log.LogError("ServiceWorkerSource does not exist: {0}", ServiceWorkerSource);
            return false;
        }

        Log.LogMessage(MessageImportance.Low, "Reading ServiceWorkerSource from disk: {0}", ServiceWorkerSource);
        string sourceContent = File.ReadAllText(ServiceWorkerSource);

        string versionedContent = $"/* Manifest version: {ManifestVersion} */{Environment.NewLine}{sourceContent}";

        Log.LogMessage(MessageImportance.Low, "Reading ServiceWorkerDestination from disk: {0}", ServiceWorkerDestination);
        string destinationContent = File.Exists(ServiceWorkerDestination) ? File.ReadAllText(ServiceWorkerDestination) : null;

        if (!string.Equals(destinationContent, versionedContent, StringComparison.Ordinal))
        {
            Log.LogMessage(MessageImportance.Low, "Writing contents to ServiceWorkerDestination: {0}", ServiceWorkerDestination);
            File.WriteAllText(ServiceWorkerDestination, versionedContent);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, "ServiceWorkerDestination is up to date. No changes needed.");
        }

        return true;
    }
}
