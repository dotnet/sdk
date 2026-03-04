// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Detects pre-existing .NET SDK and runtime installations in a dotnet root
/// that were not installed by dotnetup, and creates install specs and installation
/// records so that garbage collection doesn't delete them.
/// </summary>
internal static class PreexistingRootDetector
{
    /// <summary>
    /// Scans a dotnet root for existing SDK and runtime installations and adds them
    /// to the manifest if the root has no existing entries.
    /// </summary>
    public static void EnsureRootIsTracked(DotnetupSharedManifest manifest, DotnetInstallRoot installRoot)
    {
        var manifestData = manifest.ReadManifest();
        var existingRoot = manifestData.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) &&
            r.Architecture == installRoot.Architecture);

        // If the root already has entries, nothing to do
        if (existingRoot is not null && (existingRoot.InstallSpecs.Count > 0 || existingRoot.Installations.Count > 0))
        {
            return;
        }

        var rootPath = installRoot.Path;
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        bool anyFound = false;

        // Scan for SDKs: sdk/{version}/
        var sdkDir = Path.Combine(rootPath, "sdk");
        if (Directory.Exists(sdkDir))
        {
            foreach (var versionDir in Directory.GetDirectories(sdkDir))
            {
                var versionName = Path.GetFileName(versionDir);
                if (Microsoft.Deployment.DotNet.Releases.ReleaseVersion.TryParse(versionName, out _))
                {
                    var subcomponents = DiscoverSubcomponentsForSdk(rootPath, versionName);
                    AddPreexistingInstallation(manifest, installRoot, InstallComponent.SDK, versionName, subcomponents);
                    anyFound = true;
                }
            }
        }

        // Scan for runtimes: shared/{runtimeName}/{version}/
        var sharedDir = Path.Combine(rootPath, "shared");
        if (Directory.Exists(sharedDir))
        {
            foreach (var runtimeDir in Directory.GetDirectories(sharedDir))
            {
                var runtimeName = Path.GetFileName(runtimeDir);
                var component = MapRuntimeNameToComponent(runtimeName);
                if (component is null)
                {
                    continue;
                }

                foreach (var versionDir in Directory.GetDirectories(runtimeDir))
                {
                    var versionName = Path.GetFileName(versionDir);
                    if (Microsoft.Deployment.DotNet.Releases.ReleaseVersion.TryParse(versionName, out _))
                    {
                        var subcomponent = $"shared/{runtimeName}/{versionName}";
                        AddPreexistingInstallation(manifest, installRoot, component.Value, versionName, [subcomponent]);
                        anyFound = true;
                    }
                }
            }
        }

        if (anyFound)
        {
            Console.WriteLine($"Detected pre-existing .NET installations in {rootPath}. They have been added to the dotnetup manifest.");
        }
    }

    private static void AddPreexistingInstallation(
        DotnetupSharedManifest manifest,
        DotnetInstallRoot installRoot,
        InstallComponent component,
        string version,
        List<string> subcomponents)
    {
        // Add a "Previous" install spec pinned to the exact version
        manifest.AddInstallSpec(installRoot, new InstallSpec
        {
            Component = component,
            VersionOrChannel = version,
            InstallSource = InstallSource.Previous
        });

        // Add the installation record
        manifest.AddInstallation(installRoot, new Installation
        {
            Component = component,
            Version = version,
            Subcomponents = subcomponents
        });
    }

    /// <summary>
    /// Discovers subcomponents that belong to an SDK installation by scanning known folders.
    /// </summary>
    private static List<string> DiscoverSubcomponentsForSdk(string rootPath, string sdkVersion)
    {
        var subcomponents = new List<string>();

        // The SDK folder itself
        var sdkDir = Path.Combine(rootPath, "sdk", sdkVersion);
        if (Directory.Exists(sdkDir))
        {
            subcomponents.Add($"sdk/{sdkVersion}");
        }

        return subcomponents;
    }

    private static InstallComponent? MapRuntimeNameToComponent(string runtimeName)
    {
        return runtimeName switch
        {
            "Microsoft.NETCore.App" => InstallComponent.Runtime,
            "Microsoft.AspNetCore.App" => InstallComponent.ASPNETCore,
            "Microsoft.WindowsDesktop.App" => InstallComponent.WindowsDesktop,
            _ => null
        };
    }
}
