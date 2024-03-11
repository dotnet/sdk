// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveRuntimePackAssets : TaskBase
    {
        public ITaskItem[] ResolvedRuntimePacks { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] UnavailableRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] SatelliteResourceLanguages { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] RuntimeFrameworks { get; set; }        

        public bool DesignTimeBuild { get; set; }

        public bool DisableTransitiveFrameworkReferenceDownloads { get; set; }

        [Output]
        public ITaskItem[] RuntimePackAssets { get; set; }

        protected override void ExecuteCore()
        {
            var runtimePackAssets = new List<ITaskItem>();

            // Find any RuntimeFrameworks that matches with FrameworkReferences, so that we can apply that RuntimeFrameworks profile to the corresponding RuntimePack
            // Matches the RuntimeFramework with the FrameworkReference by comparing the FrameworkName metadata
            List<ITaskItem> matchingRuntimeFrameworks = FrameworkReferences
                .SelectMany(fxReference => RuntimeFrameworks
                    .Where(rtFx => fxReference.ItemSpec.Equals(rtFx.GetMetadata(MetadataKeys.FrameworkName), StringComparison.OrdinalIgnoreCase) &&
                                   !string.IsNullOrEmpty(rtFx.GetMetadata("Profile"))))
                .ToList();

            HashSet<string> frameworkReferenceNames = new(FrameworkReferences.Select(item => item.ItemSpec), StringComparer.OrdinalIgnoreCase);

            foreach (var unavailableRuntimePack in UnavailableRuntimePacks)
            {
                if (frameworkReferenceNames.Contains(unavailableRuntimePack.ItemSpec))
                {
                    //  This is a runtime pack that should be used, but wasn't available for the specified RuntimeIdentifier
                    //  NETSDK1082: There was no runtime pack for {0} available for the specified RuntimeIdentifier '{1}'.
                    Log.LogError(Strings.NoRuntimePackAvailable, unavailableRuntimePack.ItemSpec,
                        unavailableRuntimePack.GetMetadata(MetadataKeys.RuntimeIdentifier));
                }
            }

            HashSet<string> processedRuntimePackRoots = new(StringComparer.OrdinalIgnoreCase);

            foreach (var runtimePack in ResolvedRuntimePacks)
            {
                if (!frameworkReferenceNames.Contains(runtimePack.GetMetadata(MetadataKeys.FrameworkName)))
                {
                    var additionalFrameworkReferences = runtimePack.GetMetadata(MetadataKeys.AdditionalFrameworkReferences);
                    if (additionalFrameworkReferences == null ||
                        !additionalFrameworkReferences.Split(';').Any(afr => frameworkReferenceNames.Contains(afr)))
                    {
                        //  This is a runtime pack for a shared framework that ultimately wasn't referenced, so don't include its assets
                        continue;
                    }
                }

                // For any RuntimeFrameworks that matches with FrameworkReferences, we can apply that RuntimeFrameworks profile to the corresponding RuntimePack
                // Matches the RuntimeFramework with the RuntimePack by comparing the FrameworkName metadata. 'profile' will be an empty string if no matching RuntimeFramework is found
                string profile = String.Join(";", matchingRuntimeFrameworks
                    .Where(matchingRTReference => runtimePack.GetMetadata("FrameworkName").Equals(matchingRTReference.ItemSpec))
                    .Select(matchingRTReference => matchingRTReference.GetMetadata("Profile")));

                string runtimePackRoot = runtimePack.GetMetadata(MetadataKeys.PackageDirectory);

                if (string.IsNullOrEmpty(runtimePackRoot) || !Directory.Exists(runtimePackRoot))
                {
                    if (!DesignTimeBuild)
                    {
                        //  Don't treat this as an error if we are doing a design-time build.  This is because the design-time
                        //  build needs to succeed in order to get the right information in order to run a restore to download
                        //  the runtime pack.

                        if (DisableTransitiveFrameworkReferenceDownloads)
                        {
                            Log.LogError(Strings.RuntimePackNotRestored_TransitiveDisabled, runtimePack.ItemSpec);
                        }
                        else
                        {
                            Log.LogError(Strings.RuntimePackNotDownloaded, runtimePack.ItemSpec,
                                runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier));
                        }
                    }
                    continue;
                }

                if (!processedRuntimePackRoots.Add(runtimePackRoot))
                {
                    //  We already added assets from this runtime pack (which can happen with FrameworkReferences to different
                    //  profiles of the same shared framework)
                    continue;
                }

                var runtimeListPath = Path.Combine(runtimePackRoot, "data", "RuntimeList.xml");

                if (File.Exists(runtimeListPath))
                {
                    var runtimePackAlwaysCopyLocal = runtimePack.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

                    AddRuntimePackAssetsFromManifest(runtimePackAssets, runtimePackRoot, runtimeListPath, runtimePack, runtimePackAlwaysCopyLocal, profile);
                }
                else
                {
                    throw new BuildErrorException(string.Format(Strings.RuntimeListNotFound, runtimeListPath));
                }
            }

            RuntimePackAssets = runtimePackAssets.ToArray();
        }

        private void AddRuntimePackAssetsFromManifest(List<ITaskItem> runtimePackAssets, string runtimePackRoot,
            string runtimeListPath, ITaskItem runtimePack, bool runtimePackAlwaysCopyLocal, string profile)
        {
            var assetSubPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            XDocument frameworkListDoc = XDocument.Load(runtimeListPath);
            foreach (var fileElement in frameworkListDoc.Root.Elements("File"))
            {
                // profile feature is only supported in net9.0 and later. We would ignore it for previous versions.
                if (!string.IsNullOrEmpty(profile))
                {
                    var profileAttributeValue = fileElement.Attribute("Profile")?.Value;

                    var assemblyProfiles = profileAttributeValue?.Split(';');
                    if (profileAttributeValue == null || !assemblyProfiles.Contains(profile, StringComparer.OrdinalIgnoreCase))
                    {
                        //  Assembly wasn't in the profile specified, so don't reference it
                        continue;
                    }
                }

                //  Call GetFullPath to normalize slashes
                string assetPath = Path.GetFullPath(Path.Combine(runtimePackRoot, fileElement.Attribute("Path").Value));

                string typeAttributeValue = fileElement.Attribute("Type").Value;
                string assetType;
                string culture = null;
                if (typeAttributeValue.Equals("Managed", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "runtime";
                }
                else if (typeAttributeValue.Equals("Native", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "native";
                }
                else if (typeAttributeValue.Equals("PgoData", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "pgodata";
                }
                else if (typeAttributeValue.Equals("Resources", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "resources";
                    culture = fileElement.Attribute("Culture")?.Value;
                    if (culture == null)
                    {
                        throw new BuildErrorException($"Culture not set in runtime manifest for {assetPath}");
                    }
                    if (SatelliteResourceLanguages.Length >= 1 &&
                        !SatelliteResourceLanguages.Any(lang => string.Equals(lang.ItemSpec, culture, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }
                else
                {
                    throw new BuildErrorException($"Unrecognized file type '{typeAttributeValue}' in {runtimeListPath}");
                }

                var assetItem = CreateAssetItem(assetPath, assetType, runtimePack, culture);

                // Ensure the asset item's destination sub-path is unique
                var assetSubPath = assetItem.GetMetadata(MetadataKeys.DestinationSubPath);
                if (!assetSubPaths.Add(assetSubPath))
                {
                    Log.LogError(Strings.DuplicateRuntimePackAsset, assetSubPath);
                    continue;
                }

                assetItem.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion")?.Value);
                assetItem.SetMetadata("FileVersion", fileElement.Attribute("FileVersion")?.Value);
                assetItem.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken")?.Value);
                assetItem.SetMetadata("DropFromSingleFile", fileElement.Attribute("DropFromSingleFile")?.Value);
                if (runtimePackAlwaysCopyLocal)
                {
                    assetItem.SetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");
                }

                runtimePackAssets.Add(assetItem);
            }
        }

        private static TaskItem CreateAssetItem(string assetPath, string assetType, ITaskItem runtimePack, string culture)
        {
            string runtimeIdentifier = runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier);

            var assetItem = new TaskItem(assetPath);

            if (assetType != "pgodata")
                assetItem.SetMetadata(MetadataKeys.CopyLocal, "true");

            if (string.IsNullOrEmpty(culture))
            {
                assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.GetFileName(assetPath));
            }
            else
            {
                assetItem.SetMetadata(MetadataKeys.DestinationSubDirectory, culture + Path.DirectorySeparatorChar);
                assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.Combine(culture, Path.GetFileName(assetPath)));
                assetItem.SetMetadata(MetadataKeys.Culture, culture);
            }

            assetItem.SetMetadata(MetadataKeys.AssetType, assetType);
            assetItem.SetMetadata(MetadataKeys.NuGetPackageId, runtimePack.GetMetadata(MetadataKeys.NuGetPackageId));
            assetItem.SetMetadata(MetadataKeys.NuGetPackageVersion, runtimePack.GetMetadata(MetadataKeys.NuGetPackageVersion));
            assetItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
            assetItem.SetMetadata(MetadataKeys.IsTrimmable, runtimePack.GetMetadata(MetadataKeys.IsTrimmable));

            return assetItem;
        }
    }
}
