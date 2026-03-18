// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using static Microsoft.NET.Build.Tasks.ResolveTargetingPackAssets;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// This class processes the FrameworkReference items.  It adds PackageReferences for the
    /// targeting packs which provide the reference assemblies, and creates RuntimeFramework
    /// items, which are written to the runtimeconfig file
    /// </summary>
    public class ProcessFrameworkReferences : TaskBase
    {
        public string? TargetFrameworkIdentifier { get; set; }

        [Required]
        public string TargetFrameworkVersion { get; set; } = null!;

        public string? TargetPlatformIdentifier { get; set; }

        public string? TargetPlatformVersion { get; set; }

        public string? TargetingPackRoot { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; } = null!;

        public bool SelfContained { get; set; }

        public bool ReadyToRunEnabled { get; set; }

        public bool ReadyToRunUseCrossgen2 { get; set; }

        public bool PublishAot { get; set; }

        public bool RequiresILLinkPack { get; set; }

        public bool IsAotCompatible { get; set; }

        public bool SilenceIsAotCompatibleUnsupportedWarning { get; set; }

        public string? MinNonEolTargetFrameworkForAot { get; set; }

        public bool EnableAotAnalyzer { get; set; }

        public string? FirstTargetFrameworkVersionToSupportAotAnalyzer { get; set; }

        public bool PublishTrimmed { get; set; }

        public bool IsTrimmable { get; set; }

        public string? FirstTargetFrameworkVersionToSupportTrimAnalyzer { get; set; }

        public bool SilenceIsTrimmableUnsupportedWarning { get; set; }

        public string? MinNonEolTargetFrameworkForTrimming { get; set; }

        public bool EnableTrimAnalyzer { get; set; }

        public bool EnableSingleFileAnalyzer { get; set; }

        public string? FirstTargetFrameworkVersionToSupportSingleFileAnalyzer { get; set; }

        public bool SilenceEnableSingleFileAnalyzerUnsupportedWarning { get; set; }

        public string? MinNonEolTargetFrameworkForSingleFile { get; set; }

        public bool AotUseKnownRuntimePackForTarget { get; set; }

        public string? RuntimeIdentifier { get; set; }

        public string[]? RuntimeIdentifiers { get; set; }

        public string? RuntimeFrameworkVersion { get; set; }

        public bool TargetLatestRuntimePatch { get; set; }

        public bool TargetLatestRuntimePatchIsDefault { get; set; }

        public bool EnableTargetingPackDownload { get; set; }

        public bool EnableRuntimePackDownload { get; set; }

        public bool EnableWindowsTargeting { get; set; }

        public bool DisableTransitiveFrameworkReferenceDownloads { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownFrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownCrossgen2Packs { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownILCompilerPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownILLinkPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownWebAssemblySdkPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownAspNetCorePacks { get; set; } = Array.Empty<ITaskItem>();

        public bool UsingMicrosoftNETSdkWebAssembly { get; set; }

        public bool RequiresAspNetWebAssets { get; set; }

        [Required]
        public string NETCoreSdkRuntimeIdentifier { get; set; } = null!;

        public string? NETCoreSdkPortableRuntimeIdentifier { get; set; }

        [Required]
        public string NetCoreRoot { get; set; } = null!;

        [Required]
        public string NETCoreSdkVersion { get; set; } = null!;

        [Output]
        public ITaskItem[]? PackagesToDownload { get; set; }

        [Output]
        public ITaskItem[]? RuntimeFrameworks { get; set; }

        [Output]
        public ITaskItem[]? TargetingPacks { get; set; }

        [Output]
        public ITaskItem[]? RuntimePacks { get; set; }

        [Output]
        public ITaskItem[]? Crossgen2Packs { get; set; }

        [Output]
        public ITaskItem[]? HostILCompilerPacks { get; set; }

        [Output]
        public ITaskItem[]? TargetILCompilerPacks { get; set; }

        [Output]
        public ITaskItem[]? ImplicitPackageReferences { get; set; }

        //  Runtime packs which aren't available for the specified RuntimeIdentifier
        [Output]
        public ITaskItem[]? UnavailableRuntimePacks { get; set; }

        [Output]
        public string[]? KnownRuntimeIdentifierPlatforms { get; set; }

        /// <summary>
        /// The runtime identifier used to represent platform-agnostic components - 'any' runtime will suffice!
        /// </summary>
        private const string RuntimeIdentifierForPlatformAgnosticComponents = "any";

        private Version? _normalizedTargetFrameworkVersion;

        /// <summary>
        /// Since this Target is mostly focused on managing RID-specific assets, we massage the 'any' RID
        /// (which is platform-agnostic) into a 'null' value to make processing simpler.
        /// </summary>
        private string? EffectiveRuntimeIdentifier => RuntimeIdentifier == RuntimeIdentifierForPlatformAgnosticComponents ? null : RuntimeIdentifier;

        /// <summary>
        /// If the current project is specifically targeting a platform, as opposed to being platform-agnostic.
        /// </summary>
        private bool ProjectIsPlatformSpecific => !string.IsNullOrEmpty(EffectiveRuntimeIdentifier);

        /// <summary>
        /// We have several deployment models that require the use of runtime assets of various kinds.
        /// This member helps identify when any of those models are in use, because we've had bugs in the past
        /// where we didn't properly account for all of them.
        /// </summary>
        private bool DeploymentModelRequiresRuntimeComponents =>
            SelfContained || ReadyToRunEnabled || RequiresILLinkPack; // RequiresILLinkPack indicates trimming/AOT scenarios, see the _ComputeToolPackInputsToProcessFrameworkReferences Target.

        protected override void ExecuteCore()
        {
            PacksAccumulator? packs = null;
            List<KnownRuntimePack>? knownRuntimePacksForTargetFramework = null;

            //  Perf optimization: If there are no FrameworkReference items, then don't do anything
            //  (This means that if you don't have any direct framework references, you won't get any transitive ones either
            if (FrameworkReferences != null && FrameworkReferences.Length != 0)
            {
                _normalizedTargetFrameworkVersion = NormalizeVersion(new Version(TargetFrameworkVersion));
                packs = new PacksAccumulator();
                AddPacksForFrameworkReferences(packs, out knownRuntimePacksForTargetFramework);
            }

            _normalizedTargetFrameworkVersion ??= NormalizeVersion(new Version(TargetFrameworkVersion));
            packs ??= new PacksAccumulator();

            var implicitPackageReferences = new List<ITaskItem>();

            if (!TryAddToolPacks(packs, implicitPackageReferences))
                return;

            AssignOutputs(packs, knownRuntimePacksForTargetFramework, implicitPackageReferences);
        }

        private bool TryAddToolPacks(PacksAccumulator packs, List<ITaskItem> implicitPackageReferences) =>
            TryAddCrossgen2Pack(packs, implicitPackageReferences)
            && TryAddILCompilerPack(packs, implicitPackageReferences)
            && TryAddILLinkPack(packs, implicitPackageReferences)
            && TryAddOptionalPacks(packs, implicitPackageReferences);

        private bool TryAddCrossgen2Pack(PacksAccumulator packs, List<ITaskItem> implicitPackageReferences)
        {
            if (!ReadyToRunEnabled || !ReadyToRunUseCrossgen2)
                return true;

            if (AddToolPack(ToolPackType.Crossgen2, _normalizedTargetFrameworkVersion!, packs.PackagesToDownload, implicitPackageReferences) is not ToolPackSupport.Supported)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return false;
            }

            return true;
        }

        private bool TryAddILCompilerPack(PacksAccumulator packs, List<ITaskItem> implicitPackageReferences)
        {
            if (!PublishAot)
                return true;

            switch (AddToolPack(ToolPackType.ILCompiler, _normalizedTargetFrameworkVersion!, packs.PackagesToDownload, implicitPackageReferences))
            {
                case ToolPackSupport.UnsupportedForTargetFramework:
                    Log.LogError(Strings.AotUnsupportedTargetFramework);
                    return false;
                case ToolPackSupport.UnsupportedForHostRuntimeIdentifier:
                    Log.LogError(Strings.AotUnsupportedHostRuntimeIdentifier, NETCoreSdkRuntimeIdentifier);
                    return false;
                case ToolPackSupport.UnsupportedForTargetRuntimeIdentifier when EffectiveRuntimeIdentifier != null:
                    Log.LogError(Strings.AotUnsupportedTargetRuntimeIdentifier, EffectiveRuntimeIdentifier!);
                    return false;
                default:
                    return true;
            }
        }

        private bool TryAddILLinkPack(PacksAccumulator packs, List<ITaskItem> implicitPackageReferences)
        {
            if (!RequiresILLinkPack)
                return true;

            if (AddToolPack(ToolPackType.ILLink, _normalizedTargetFrameworkVersion!, packs.PackagesToDownload, implicitPackageReferences) is not ToolPackSupport.Supported)
                HandleILLinkPackUnsupported();

            return true;
        }

        private bool TryAddOptionalPacks(PacksAccumulator packs, List<ITaskItem> implicitPackageReferences)
        {
            if (UsingMicrosoftNETSdkWebAssembly)
            {
                // WebAssemblySdk is used for .NET >= 6, it's ok if no pack is added.
                AddToolPack(ToolPackType.WebAssemblySdk, _normalizedTargetFrameworkVersion!, packs.PackagesToDownload, implicitPackageReferences);
            }

            if (RequiresAspNetWebAssets && _normalizedTargetFrameworkVersion!.Major >= 10)
            {
                if (AddToolPack(ToolPackType.AspNetCore, _normalizedTargetFrameworkVersion!, packs.PackagesToDownload, implicitPackageReferences) is not ToolPackSupport.Supported)
                    Log.LogWarning(Strings.AspNetCorePackUnsupportedTargetFramework);
            }

            return true;
        }

        private void HandleILLinkPackUnsupported()
        {
            // Keep the checked properties in sync with _RequiresILLinkPack in Microsoft.NET.Publish.targets.
            if (PublishAot)
                // If PublishAot is set, this should produce a specific error above already.
                // Also produce one here just in case there are custom KnownILCompilerPack/KnownILLinkPack
                // items that bypass the error above.
                Log.LogError(Strings.AotUnsupportedTargetFramework);
            else if (IsAotCompatible || EnableAotAnalyzer)
                WarnAotCompatibleUnsupported();
            else if (PublishTrimmed)
                Log.LogError(Strings.PublishTrimmedRequiresVersion30);
            else if (IsTrimmable || EnableTrimAnalyzer)
                WarnTrimmableUnsupported();
            else if (EnableSingleFileAnalyzer)
                WarnSingleFileAnalyzerUnsupported();
            else
                // _RequiresILLinkPack was set. This setting acts as an override for the
                // user-visible properties, and should generally only be used by
                // other SDKs that can't use the other properties for some reason.
                Log.LogError(Strings.ILLinkNoValidRuntimePackageError);
        }

        private void WarnAotCompatibleUnsupported()
        {
            if (!SilenceIsAotCompatibleUnsupportedWarning)
                Log.LogWarning(Strings.IsAotCompatibleUnsupported, MinNonEolTargetFrameworkForAot!);
        }

        private void WarnTrimmableUnsupported()
        {
            if (!SilenceIsTrimmableUnsupportedWarning)
                Log.LogWarning(Strings.IsTrimmableUnsupported, MinNonEolTargetFrameworkForTrimming!);
        }

        private void WarnSingleFileAnalyzerUnsupported()
        {
            // There's no IsSingleFileCompatible setting. EnableSingleFileAnalyzer is the
            // recommended way to ensure single-file compatibility for libraries.
            if (!SilenceEnableSingleFileAnalyzerUnsupportedWarning)
                Log.LogWarning(Strings.EnableSingleFileAnalyzerUnsupported, MinNonEolTargetFrameworkForSingleFile!);
        }

        private void AssignOutputs(
            PacksAccumulator packs,
            List<KnownRuntimePack>? knownRuntimePacksForTargetFramework,
            List<ITaskItem> implicitPackageReferences)
        {
            if (packs.PackagesToDownload.Any())
                PackagesToDownload = packs.PackagesToDownload.Distinct(new PackageToDownloadComparer<ITaskItem>()).ToArray();

            if (packs.RuntimeFrameworks.Any())
                RuntimeFrameworks = packs.RuntimeFrameworks.ToArray();

            if (packs.TargetingPacks.Any())
                TargetingPacks = packs.TargetingPacks.ToArray();

            if (packs.RuntimePacks.Any())
                RuntimePacks = packs.RuntimePacks.ToArray();

            if (packs.UnavailableRuntimePacks.Any())
                UnavailableRuntimePacks = packs.UnavailableRuntimePacks.ToArray();

            if (implicitPackageReferences.Any())
                ImplicitPackageReferences = implicitPackageReferences.ToArray();

            AssignKnownRuntimeIdentifierPlatforms(knownRuntimePacksForTargetFramework);
        }

        private void AssignKnownRuntimeIdentifierPlatforms(List<KnownRuntimePack>? knownRuntimePacksForTargetFramework)
        {
            if (knownRuntimePacksForTargetFramework?.Any() != true)
                return;

            // Determine the known runtime identifier platforms based on all available Microsoft.NETCore.App packs
            var knownRuntimeIdentifierPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var netCoreAppPacks = knownRuntimePacksForTargetFramework.Where(krp => krp.Name.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase));
            foreach (KnownRuntimePack netCoreAppPack in netCoreAppPacks)
            {
                foreach (var runtimeIdentifier in netCoreAppPack.RuntimePackRuntimeIdentifiers.Split(';'))
                {
                    int separator = runtimeIdentifier.LastIndexOf('-');
                    string platform = separator < 0 ? runtimeIdentifier : runtimeIdentifier.Substring(0, separator);
                    knownRuntimeIdentifierPlatforms.Add(platform);
                }
            }

            if (knownRuntimeIdentifierPlatforms.Count > 0)
                KnownRuntimeIdentifierPlatforms = knownRuntimeIdentifierPlatforms.ToArray();
        }

        void AddPacksForFrameworkReferences(
            PacksAccumulator packs,
            out List<KnownRuntimePack> knownRuntimePacksForTargetFramework)
        {
            var knownFrameworkReferencesForTargetFramework =
                KnownFrameworkReferences
                    .Select(item => new KnownFrameworkReference(item))
                    .Where(kfr => KnownFrameworkReferenceAppliesToTargetFramework(kfr.TargetFramework))
                    .ToList();

            //  Get known runtime packs from known framework references.
            //  Only use items where the framework reference name matches the RuntimeFrameworkName.
            //  This will filter out known framework references for "profiles", ie WindowsForms and WPF
            knownRuntimePacksForTargetFramework =
                knownFrameworkReferencesForTargetFramework
                    .Where(kfr => kfr.Name.Equals(kfr.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase))
                    .Select(kfr => kfr.ToKnownRuntimePack())
                    .ToList();

            //  Add additional known runtime packs
            knownRuntimePacksForTargetFramework.AddRange(
                KnownRuntimePacks.Select(item => new KnownRuntimePack(item))
                                 .Where(krp => KnownFrameworkReferenceAppliesToTargetFramework(krp.TargetFramework)));

            var frameworkReferenceMap = FrameworkReferences.ToDictionary(fr => fr.ItemSpec, StringComparer.OrdinalIgnoreCase);
            Log.LogMessage(MessageImportance.Low, $"Found {frameworkReferenceMap.Count} known framework references for target framework {TargetFrameworkIdentifier}");
            Log.LogMessage(MessageImportance.Low, $"Found {knownRuntimePacksForTargetFramework.Count} known runtime packs for target framework {TargetFrameworkIdentifier}");

            bool windowsOnlyErrorLogged = false;
            foreach (var knownFrameworkReference in knownFrameworkReferencesForTargetFramework)
            {
                frameworkReferenceMap.TryGetValue(knownFrameworkReference.Name, out ITaskItem? frameworkReference);

                if (IsWindowsOnlyUnsupportedOnCurrentPlatform(knownFrameworkReference))
                {
                    if (!windowsOnlyErrorLogged && frameworkReference != null)
                    {
                        Log.LogError(Strings.WindowsDesktopFrameworkRequiresWindows);
                        windowsOnlyErrorLogged = true;
                    }
                    Log.LogMessage(MessageImportance.Low, $"Ignoring framework reference to {knownFrameworkReference.Name} as it is Windows-only and the current platform is not Windows.");
                    continue;
                }

                KnownRuntimePack? selectedRuntimePack = SelectRuntimePack(frameworkReference, knownFrameworkReference, knownRuntimePacksForTargetFramework);

                string targetingPackVersion = ResolveTargetingPackVersion(frameworkReference, knownFrameworkReference);
                var targetingPackDescriptor = new TargetingPackDescriptor(knownFrameworkReference, selectedRuntimePack, targetingPackVersion);
                CreateTargetingPackItem(targetingPackDescriptor, frameworkReference, packs);

                string runtimeFrameworkVersion = GetRuntimeFrameworkVersion(
                    frameworkReference, knownFrameworkReference, selectedRuntimePack, out string runtimePackVersion);

                var frameworkRefState = new FrameworkReferenceState(knownFrameworkReference, selectedRuntimePack, runtimePackVersion, frameworkReference);
                ProcessRuntimeIdentifiersForFrameworkReference(frameworkRefState, knownFrameworkReferencesForTargetFramework, packs);

                if (!string.IsNullOrEmpty(knownFrameworkReference.RuntimeFrameworkName) && !knownFrameworkReference.RuntimePackAlwaysCopyLocal)
                {
                    TaskItem runtimeFramework = new(knownFrameworkReference.RuntimeFrameworkName);
                    runtimeFramework.SetMetadata(MetadataKeys.Version, runtimeFrameworkVersion);
                    runtimeFramework.SetMetadata(MetadataKeys.FrameworkName, knownFrameworkReference.Name);
                    runtimeFramework.SetMetadata("Profile", knownFrameworkReference.Profile);
                    packs.RuntimeFrameworks.Add(runtimeFramework);
                    Log.LogMessage(MessageImportance.Low, $"Added runtime framework '{runtimeFramework.ItemSpec}@{runtimeFrameworkVersion}'");
                }
            }
        }

        private string ResolveTargetingPackVersion(ITaskItem? frameworkReference, KnownFrameworkReference knownFrameworkReference)
        {
            //  Allow targeting pack version to be overridden via metadata on FrameworkReference
            string? targetingPackVersion = frameworkReference?.GetMetadata("TargetingPackVersion");
            if (string.IsNullOrEmpty(targetingPackVersion))
                targetingPackVersion = knownFrameworkReference.TargetingPackVersion;

            //  Look up targeting pack version from workload manifests if necessary
            return GetResolvedPackVersion(knownFrameworkReference.TargetingPackName, targetingPackVersion);
        }

        private void CreateTargetingPackItem(
            TargetingPackDescriptor descriptor,
            ITaskItem? frameworkReference,
            PacksAccumulator packs)
        {
            var (knownFrameworkReference, selectedRuntimePack, targetingPackVersion) = descriptor;

            //  Add targeting pack and all known runtime packs to "preferred packages" list.
            //  These are packages that will win in conflict resolution for assets that have identical assembly and file versions
            var preferredPackages = BuildPreferredPackages(knownFrameworkReference, selectedRuntimePack);

            TaskItem targetingPack = new(knownFrameworkReference.Name);
            targetingPack.SetMetadata(MetadataKeys.NuGetPackageId, knownFrameworkReference.TargetingPackName);
            targetingPack.SetMetadata(MetadataKeys.PackageConflictPreferredPackages, string.Join(";", preferredPackages));
            targetingPack.SetMetadata(MetadataKeys.NuGetPackageVersion, targetingPackVersion);
            targetingPack.SetMetadata("TargetingPackFormat", knownFrameworkReference.TargetingPackFormat);
            targetingPack.SetMetadata("TargetFramework", knownFrameworkReference.TargetFramework.GetShortFolderName());
            targetingPack.SetMetadata(MetadataKeys.RuntimeFrameworkName, knownFrameworkReference.RuntimeFrameworkName);
            if (selectedRuntimePack != null)
                targetingPack.SetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers, selectedRuntimePack?.RuntimePackRuntimeIdentifiers);

            if (!string.IsNullOrEmpty(knownFrameworkReference.Profile))
                targetingPack.SetMetadata("Profile", knownFrameworkReference.Profile);

            //  Get the path of the targeting pack in the targeting pack root (e.g. dotnet/packs)
            string? targetingPackPath = GetPackPath(knownFrameworkReference.TargetingPackName, targetingPackVersion);
            if (targetingPackPath != null)
            {
                // Use targeting pack from packs folder
                targetingPack.SetMetadata(MetadataKeys.PackageDirectory, targetingPackPath);
                targetingPack.SetMetadata(MetadataKeys.Path, targetingPackPath);
            }
            else if (ShouldDownloadTargetingPack(frameworkReference))
            {
                //  Download targeting pack
                TaskItem packageToDownload = new(knownFrameworkReference.TargetingPackName);
                packageToDownload.SetMetadata(MetadataKeys.Version, targetingPackVersion);
                packs.PackagesToDownload.Add(packageToDownload);
            }

            packs.TargetingPacks.Add(targetingPack);
            Log.LogMessage(MessageImportance.Low, $"Selected targeting pack '{targetingPack.ItemSpec}@{targetingPackVersion}'");
        }

        private HashSet<string> BuildPreferredPackages(KnownFrameworkReference knownFrameworkReference, KnownRuntimePack? selectedRuntimePack)
        {
            var preferredPackages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                knownFrameworkReference.TargetingPackName
            };

            if (selectedRuntimePack is KnownRuntimePack selectedPack)
            {
                foreach (var runtimeIdentifier in selectedPack.RuntimePackRuntimeIdentifiers.Split(';'))
                {
                    foreach (var runtimePackNamePattern in selectedPack.RuntimePackNamePatterns.Split(';'))
                    {
                        preferredPackages.Add(runtimePackNamePattern.Replace("**RID**", runtimeIdentifier));
                    }
                }
                Log.LogMessage(MessageImportance.Low, $"Selected {selectedPack.Name} with RIDs '{selectedPack.RuntimePackRuntimeIdentifiers}'");
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"No runtime pack found for {knownFrameworkReference.Name}.");
            }

            return preferredPackages;
        }

        private void ProcessRuntimeIdentifiersForFrameworkReference(
            FrameworkReferenceState state,
            List<KnownFrameworkReference> knownFrameworkReferencesForTargetFramework,
            PacksAccumulator packs)
        {
            var (knownFrameworkReference, selectedRuntimePack, runtimePackVersion, frameworkReference) = state;

            var (runtimePackForRIDProcessing, useRuntimePackAndDownloadIfNecessary) =
                DetermineRuntimePackUsage(knownFrameworkReference, selectedRuntimePack);

            bool processedPrimaryRid = false;
            if (HasRuntimePackRequirement(selectedRuntimePack))
            {
                var additionalRefs = FindAdditionalRuntimePackRefs(knownFrameworkReference, knownFrameworkReferencesForTargetFramework);
                var primaryRid = EffectiveRuntimeIdentifier ?? RuntimeIdentifierForPlatformAgnosticComponents;
                var primaryOptions = new RuntimePackResolutionOptions(
                    additionalRefs, useRuntimePackAndDownloadIfNecessary,
                    WasReferencedDirectly: frameworkReference != null, DownloadOnly: false);
                ProcessRuntimeIdentifier(primaryRid, runtimePackVersion, runtimePackForRIDProcessing, primaryOptions, packs);
                processedPrimaryRid = true;
            }

            var additionalRidContext = new AdditionalRidProcessingContext(
                runtimePackForRIDProcessing, runtimePackVersion,
                useRuntimePackAndDownloadIfNecessary, processedPrimaryRid, frameworkReference);
            ProcessAdditionalRids(additionalRidContext, packs);
        }

        private static (KnownRuntimePack packForRIDProcessing, bool usePackAndDownload) DetermineRuntimePackUsage(
            KnownFrameworkReference knownFrameworkReference,
            KnownRuntimePack? selectedRuntimePack)
        {
            if (knownFrameworkReference.Name.Equals(knownFrameworkReference.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase) && selectedRuntimePack != null)
            {
                //  Only add runtime packs where the framework reference name matches the RuntimeFrameworkName
                //  Framework references for "profiles" will use the runtime pack from the corresponding non-profile framework
                return (selectedRuntimePack.Value, usePackAndDownload: true);
            }

            if (!knownFrameworkReference.RuntimePackRuntimeIdentifiers.Equals(selectedRuntimePack?.RuntimePackRuntimeIdentifiers))
            {
                // If the profile has a different set of runtime identifiers than the runtime pack, use the profile.
                return (knownFrameworkReference.ToKnownRuntimePack(), usePackAndDownload: true);
            }

            // For the remaining profiles, don't include them in package download but add them to unavailable if necessary.
            return (knownFrameworkReference.ToKnownRuntimePack(), usePackAndDownload: false);
        }

        private bool HasRuntimePackRequirement(KnownRuntimePack? selectedRuntimePack)
        {
            if (selectedRuntimePack != null && selectedRuntimePack.Value.RuntimePackAlwaysCopyLocal)
                return true;

            return DeploymentModelRequiresRuntimeComponents
                && ProjectIsPlatformSpecific
                && selectedRuntimePack?.HasRuntimePackages == true;
        }

        private static List<string>? FindAdditionalRuntimePackRefs(
            KnownFrameworkReference knownFrameworkReference,
            List<KnownFrameworkReference> knownFrameworkReferencesForTargetFramework)
        {
            List<string>? additionalRefs = null;
            foreach (var additionalKfr in knownFrameworkReferencesForTargetFramework)
            {
                if (additionalKfr.RuntimeFrameworkName.Equals(knownFrameworkReference.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase) &&
                    !additionalKfr.RuntimeFrameworkName.Equals(additionalKfr.Name, StringComparison.OrdinalIgnoreCase))
                {
                    additionalRefs ??= [];
                    additionalRefs.Add(additionalKfr.Name);
                }
            }
            return additionalRefs;
        }

        private void ProcessAdditionalRids(AdditionalRidProcessingContext ctx, PacksAccumulator packs)
        {
            foreach (var runtimeIdentifier in RuntimeIdentifiers ?? Array.Empty<string>())
            {
                if (ctx.ProcessedPrimaryRid && runtimeIdentifier == EffectiveRuntimeIdentifier)
                    continue;

                if (runtimeIdentifier == RuntimeIdentifierForPlatformAgnosticComponents)
                {
                    // The `any` RID represents a platform-agnostic target. As such, it has no
                    // platform-specific runtime pack associated with it.
                    continue;
                }

                //  Pass downloadOnly: true — we want to download the runtime packs but not use their assets
                var options = new RuntimePackResolutionOptions(
                    AdditionalFrameworkReferences: null, ctx.UsePackAndDownload,
                    WasReferencedDirectly: ctx.FrameworkReference != null, DownloadOnly: true);
                ProcessRuntimeIdentifier(runtimeIdentifier, ctx.RuntimePackVersion, ctx.RuntimePackForRID, options, packs);
            }
        }

        private bool KnownFrameworkReferenceAppliesToTargetFramework(NuGetFramework kfr)
        {
            if (!kfr.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase)
                || NormalizeVersion(kfr.Version) != _normalizedTargetFrameworkVersion)
                return false;

            if (!string.IsNullOrEmpty(kfr.Platform) && kfr.PlatformVersion != null)
                return TargetPlatformVersionMatches(kfr);

            return true;
        }

        private bool TargetPlatformVersionMatches(NuGetFramework kfr)
        {
            if (!kfr.Platform.Equals(TargetPlatformIdentifier, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!Version.TryParse(TargetPlatformVersion, out var targetPlatformVersionParsed))
                return false;

            if (NormalizeVersion(targetPlatformVersionParsed) != NormalizeVersion(kfr.PlatformVersion)
                || NormalizeVersion(kfr.Version) != _normalizedTargetFrameworkVersion)
                return false;

            return true;
        }

        private KnownRuntimePack? SelectRuntimePack(ITaskItem? frameworkReference, KnownFrameworkReference knownFrameworkReference, List<KnownRuntimePack> knownRuntimePacks)
        {
            var requiredLabelsMetadata = frameworkReference?.GetMetadata(MetadataKeys.RuntimePackLabels) ?? "";

            HashSet<string>? requiredRuntimePackLabels = null;
            if (frameworkReference != null)
            {
                requiredRuntimePackLabels = new HashSet<string>(requiredLabelsMetadata.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            }

            //  The runtime pack name matches the RuntimeFrameworkName on the KnownFrameworkReference
            var matchingRuntimePacks = knownRuntimePacks
                .Where(krp => krp.Name.Equals(knownFrameworkReference.RuntimeFrameworkName, StringComparison.OrdinalIgnoreCase))
                .Where(krp => requiredRuntimePackLabels == null
                    ? krp.RuntimePackLabels.Length == 0
                    : requiredRuntimePackLabels.SetEquals(krp.RuntimePackLabels))
                .ToList();

            if (matchingRuntimePacks.Count == 0)
                return null;

            if (matchingRuntimePacks.Count == 1)
                return matchingRuntimePacks[0];

            string runtimePackDescriptionForErrorMessage = knownFrameworkReference.RuntimeFrameworkName +
                (requiredLabelsMetadata == string.Empty ? string.Empty : ":" + requiredLabelsMetadata);

            Log.LogError(Strings.ConflictingRuntimePackInformation, runtimePackDescriptionForErrorMessage,
                string.Join(Environment.NewLine, matchingRuntimePacks.Select(rp => rp.RuntimePackNamePatterns)));

            return knownFrameworkReference.ToKnownRuntimePack();
        }

        private void ProcessRuntimeIdentifier(
            string runtimeIdentifier,
            string runtimePackVersion,
            KnownRuntimePack selectedRuntimePack,
            RuntimePackResolutionOptions options,
            PacksAccumulator packs)
        {
            var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            var knownRids = selectedRuntimePack.RuntimePackRuntimeIdentifiers.Split(';');
            var excludedRids = selectedRuntimePack.RuntimePackExcludedRuntimeIdentifiers.Split(';');
            Log.LogMessage(MessageImportance.Low, $"Finding best RID match for pack {selectedRuntimePack.Name}@{runtimePackVersion} for target RID '{runtimeIdentifier}' from '{selectedRuntimePack.RuntimePackRuntimeIdentifiers}' excluding '{selectedRuntimePack.RuntimePackExcludedRuntimeIdentifiers}'");

            string? runtimePackRuntimeIdentifier = NuGetUtils.GetBestMatchingRidWithExclusion(
                runtimeGraph, runtimeIdentifier, excludedRids, knownRids, out bool wasInGraph);

            if (runtimePackRuntimeIdentifier == null)
            {
                HandleUnavailableRuntimePack(runtimeIdentifier, selectedRuntimePack, wasInGraph, packs);
            }
            else if (options.AddPackAndDownloadIfNecessary)
            {
                AddResolvedRuntimePack(runtimePackRuntimeIdentifier, runtimePackVersion, selectedRuntimePack, options, packs);
            }
        }

        private void HandleUnavailableRuntimePack(
            string runtimeIdentifier,
            KnownRuntimePack selectedRuntimePack,
            bool wasInGraph,
            PacksAccumulator packs)
        {
            if (wasInGraph)
            {
                //  Report this as an error later, if necessary.  This is because we try to download
                //  all available runtime packs in case there is a transitive reference to a shared
                //  framework we don't directly reference.  But we don't want to immediately error out
                //  here if a runtime pack that we might not need to reference isn't available for the
                //  targeted RID (e.g. Microsoft.WindowsDesktop.App for a linux RID).
                var unavailableRuntimePack = new TaskItem(selectedRuntimePack.Name);
                unavailableRuntimePack.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
                packs.UnavailableRuntimePacks.Add(unavailableRuntimePack);
            }
            else if (!packs.UnrecognisedRuntimeIdentifiers.Contains(runtimeIdentifier))
            {
                //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                Log.LogError(Strings.RuntimeIdentifierNotRecognized, runtimeIdentifier);
                packs.UnrecognisedRuntimeIdentifiers.Add(runtimeIdentifier);
            }
        }

        private void AddResolvedRuntimePack(
            string runtimePackRid,
            string runtimePackVersion,
            KnownRuntimePack selectedRuntimePack,
            RuntimePackResolutionOptions options,
            PacksAccumulator packs)
        {
            var isTrimmable = selectedRuntimePack.IsTrimmable;
            foreach (var runtimePackNamePattern in selectedRuntimePack.RuntimePackNamePatterns.Split(';'))
            {
                string runtimePackName = runtimePackNamePattern.Replace("**RID**", runtimePackRid);

                //  Look up runtimePackVersion from workload manifests if necessary
                string resolvedRuntimePackVersion = GetResolvedPackVersion(runtimePackName, runtimePackVersion);
                string? runtimePackPath = GetPackPath(runtimePackName, resolvedRuntimePackVersion);

                if (!options.DownloadOnly)
                {
                    TaskItem runtimePackItem = new(runtimePackName);
                    runtimePackItem.SetMetadata(MetadataKeys.NuGetPackageId, runtimePackName);
                    runtimePackItem.SetMetadata(MetadataKeys.NuGetPackageVersion, resolvedRuntimePackVersion);
                    runtimePackItem.SetMetadata(MetadataKeys.FrameworkName, selectedRuntimePack.Name);
                    runtimePackItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimePackRid);
                    runtimePackItem.SetMetadata(MetadataKeys.IsTrimmable, isTrimmable);

                    if (selectedRuntimePack.RuntimePackAlwaysCopyLocal)
                        runtimePackItem.SetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

                    if (options.AdditionalFrameworkReferences != null)
                        runtimePackItem.SetMetadata(MetadataKeys.AdditionalFrameworkReferences, string.Join(";", options.AdditionalFrameworkReferences));

                    if (runtimePackPath != null)
                        runtimePackItem.SetMetadata(MetadataKeys.PackageDirectory, runtimePackPath);

                    packs.RuntimePacks.Add(runtimePackItem);
                }

                if (ShouldDownloadRuntimePack(runtimePackPath, options.WasReferencedDirectly))
                {
                    TaskItem packageToDownload = new(runtimePackName);
                    packageToDownload.SetMetadata(MetadataKeys.Version, resolvedRuntimePackVersion);
                    packs.PackagesToDownload.Add(packageToDownload);
                }
            }
        }

        // Enum values should match the name of the pack: Known<Foo>Pack
        private enum ToolPackType
        {
            Crossgen2,
            ILCompiler,
            ILLink,
            WebAssemblySdk,
            AspNetCore,
        }

        enum ToolPackSupport
        {
            UnsupportedForTargetFramework,
            UnsupportedForHostRuntimeIdentifier,
            UnsupportedForTargetRuntimeIdentifier,
            Supported
        }

        private ToolPackSupport AddToolPack(
            ToolPackType toolPackType,
            Version normalizedTargetFrameworkVersion,
            List<ITaskItem> packagesToDownload,
            List<ITaskItem> implicitPackageReferences)
        {
            var knownPack = FindKnownPackForTargetFramework(toolPackType, normalizedTargetFrameworkVersion);
            if (knownPack == null)
                return ToolPackSupport.UnsupportedForTargetFramework;

            var packVersion = knownPack.GetMetadata(toolPackType.ToString() + "PackVersion");
            if (!string.IsNullOrEmpty(RuntimeFrameworkVersion))
                packVersion = RuntimeFrameworkVersion;

            Log.LogMessage(MessageImportance.Low, $"Found {toolPackType} pack '{knownPack.ItemSpec}@{packVersion}'");

            var ridResult = TryHandleRidSpecificToolPack(toolPackType, knownPack, packVersion, packagesToDownload);
            if (ridResult != ToolPackSupport.Supported)
                return ridResult;

            if (toolPackType is ToolPackType.ILLink)
            {
                // The ILLink tool pack is available for some TargetFrameworks where we nonetheless consider
                // IsTrimmable/IsAotCompatible/EnableSingleFile to be unsupported, because the framework
                // was not annotated with the attributes.
                var analyzerResult = CheckILLinkAnalyzerSupport(normalizedTargetFrameworkVersion);
                if (analyzerResult != ToolPackSupport.Supported)
                    return analyzerResult;
            }

            AddImplicitPackageReferences(toolPackType, knownPack, packVersion, implicitPackageReferences);
            return ToolPackSupport.Supported;
        }

        private ITaskItem? FindKnownPackForTargetFramework(ToolPackType toolPackType, Version normalizedTargetFrameworkVersion)
        {
            Log.LogMessage(MessageImportance.Low, $"Adding tool pack {toolPackType} for runtime {normalizedTargetFrameworkVersion}");

            return GetKnownPacksForType(toolPackType)
                .Where(pack =>
                {
                    var packTargetFramework = NuGetFramework.Parse(pack.GetMetadata("TargetFramework"));
                    return packTargetFramework.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                        NormalizeVersion(packTargetFramework.Version) == normalizedTargetFrameworkVersion;
                })
                .SingleOrDefault();
        }

        private ITaskItem[] GetKnownPacksForType(ToolPackType toolPackType) => toolPackType switch
        {
            ToolPackType.Crossgen2 => KnownCrossgen2Packs,
            ToolPackType.ILCompiler => KnownILCompilerPacks,
            ToolPackType.ILLink => KnownILLinkPacks,
            ToolPackType.WebAssemblySdk => KnownWebAssemblySdkPacks,
            ToolPackType.AspNetCore => KnownAspNetCorePacks,
            _ => throw new ArgumentException($"Unknown package type {toolPackType}", nameof(toolPackType))
        };

        private ToolPackSupport TryHandleRidSpecificToolPack(
            ToolPackType toolPackType,
            ITaskItem knownPack,
            string packVersion,
            List<ITaskItem> packagesToDownload)
        {
            // Crossgen and ILCompiler have RID-specific bits; all other pack types need no RID resolution.
            if (toolPackType is not ToolPackType.Crossgen2 and not ToolPackType.ILCompiler)
                return ToolPackSupport.Supported;

            var packName = toolPackType.ToString();
            var (hostPackItem, hostRidContext, hostResult) = ResolveHostRidForToolPack(knownPack, packName, packVersion, packagesToDownload);
            if (hostResult != ToolPackSupport.Supported)
                return hostResult;

            switch (toolPackType)
            {
                case ToolPackType.Crossgen2:
                    Crossgen2Packs = new[] { hostPackItem };
                    break;
                case ToolPackType.ILCompiler:
                    var ilcResult = TrySetupILCompilerPacks(hostPackItem, hostRidContext, knownPack, packagesToDownload);
                    if (ilcResult != ToolPackSupport.Supported)
                        return ilcResult;
                    break;
            }

            return ToolPackSupport.Supported;
        }

        private void AddImplicitPackageReferences(
            ToolPackType toolPackType,
            ITaskItem knownPack,
            string packVersion,
            List<ITaskItem> implicitPackageReferences)
        {
            // Packs with RID-agnostic build packages that contain MSBuild targets.
            if (toolPackType is not ToolPackType.Crossgen2 && EnableRuntimePackDownload)
            {
                Log.LogMessage(MessageImportance.Low, $"Added {knownPack.ItemSpec}@{packVersion} for build-time targets");
                var buildPackage = new TaskItem(knownPack.ItemSpec);
                buildPackage.SetMetadata(MetadataKeys.Version, packVersion);
                implicitPackageReferences.Add(buildPackage);
            }

            // Before net8.0, ILLink analyzers shipped in a separate package.
            // Add the analyzer package with version taken from KnownILLinkPack if the version is less than 8.0.0.
            // The version comparison doesn't consider prerelease labels, so 8.0.0-foo will be considered equal to 8.0.0 and
            // will not get the extra analyzer package reference.
            if (toolPackType is ToolPackType.ILLink && IsPreNet8ILLinkPack(packVersion))
            {
                if (!EnableRuntimePackDownload)
                    return;

                var analyzerPackage = new TaskItem("Microsoft.NET.ILLink.Analyzers");
                analyzerPackage.SetMetadata(MetadataKeys.Version, packVersion);
                implicitPackageReferences.Add(analyzerPackage);
                Log.LogMessage(MessageImportance.Low, $"Added {analyzerPackage.ItemSpec}@{packVersion} for linker analyzers");
            }
        }

        private ToolPackSupport TrySetupILCompilerPacks(
            TaskItem hostPackItem,
            ToolPackResolutionContext hostRidContext,
            ITaskItem knownPack,
            List<ITaskItem> packagesToDownload)
        {
            // ILCompiler supports cross-target compilation. If there is a cross-target request,
            // we need to download that package as well unless we use KnownRuntimePack entries for the target.
            if (!AotUseKnownRuntimePackForTarget)
            {
                var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
                var crossResult = AddILCompilerCrossTargetPacks(hostRidContext, knownPack, runtimeGraph, packagesToDownload);
                if (crossResult != ToolPackSupport.Supported)
                    return crossResult;
            }

            HostILCompilerPacks = new[] { hostPackItem };
            return ToolPackSupport.Supported;
        }

        private (TaskItem hostPackItem, ToolPackResolutionContext context, ToolPackSupport support) ResolveHostRidForToolPack(
            ITaskItem knownPack,
            string packName,
            string packVersion,
            List<ITaskItem> packagesToDownload)
        {
            var packNamePattern = knownPack.GetMetadata(packName + "PackNamePattern");
            var packSupportedRids = knownPack.GetMetadata(packName + "RuntimeIdentifiers").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var packSupportedPortableRids = knownPack.GetMetadata(packName + "PortableRuntimeIdentifiers").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            // Use the non-portable RIDs if there are no portable RIDs defined.
            packSupportedPortableRids = packSupportedPortableRids.Length > 0 ? packSupportedPortableRids : packSupportedRids;

            // When publishing for a non-portable RID, prefer NETCoreSdkRuntimeIdentifier for the host.
            // Otherwise prefer the NETCoreSdkPortableRuntimeIdentifier.
            // This makes non-portable SDKs behave the same as portable SDKs except for the specific case of targetting a non-portable RID.
            // This ensures that targeting portable RIDs doesn't require any non-portable assets that aren't packaged in the SDK.
            // Due to size concerns, the non-portable ILCompiler and Crossgen2 aren't included by default in non-portable SDK distributions.
            var runtimeGraph = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath);
            var runtimeIdentifier = RuntimeIdentifier ?? RuntimeIdentifierForPlatformAgnosticComponents;

            string? supportedTargetRid = NuGetUtils.GetBestMatchingRid(runtimeGraph, runtimeIdentifier, packSupportedRids, out _);
            string? supportedPortableTargetRid = NuGetUtils.GetBestMatchingRid(runtimeGraph, runtimeIdentifier, packSupportedPortableRids, out _);

            bool usePortable = ShouldUsePortableRid(NETCoreSdkPortableRuntimeIdentifier, supportedTargetRid, supportedPortableTargetRid);

            // Get the best RID for the host machine, which will be used to validate that we can run crossgen for the target platform and architecture
            Log.LogMessage(MessageImportance.Low, $"Determining best RID for '{knownPack.ItemSpec}@{packVersion}' from among '{knownPack.GetMetadata(packName + "RuntimeIdentifiers")}'");

            string? hostRid = usePortable
                ? NuGetUtils.GetBestMatchingRid(runtimeGraph, NETCoreSdkPortableRuntimeIdentifier!, packSupportedPortableRids, out _)
                : NuGetUtils.GetBestMatchingRid(runtimeGraph, NETCoreSdkRuntimeIdentifier!, packSupportedRids, out _);

            if (hostRid == null)
            {
                Log.LogMessage(MessageImportance.Low, $"No matching RID was found'");
                return (null!, default, ToolPackSupport.UnsupportedForHostRuntimeIdentifier);
            }

            Log.LogMessage(MessageImportance.Low, $"Best RID for '{knownPack.ItemSpec}@{packVersion}' is '{hostRid}'");
            var runtimePackName = packNamePattern.Replace("**RID**", hostRid);

            var runtimePackItem = new TaskItem(runtimePackName);
            runtimePackItem.SetMetadata(MetadataKeys.NuGetPackageId, runtimePackName);
            runtimePackItem.SetMetadata(MetadataKeys.NuGetPackageVersion, packVersion);

            string? runtimePackPath = GetPackPath(runtimePackName, packVersion);
            if (runtimePackPath != null)
            {
                runtimePackItem.SetMetadata(MetadataKeys.PackageDirectory, runtimePackPath);
            }
            else if (EnableRuntimePackDownload)
            {
                // We need to download the runtime pack
                var runtimePackToDownload = new TaskItem(runtimePackName);
                runtimePackToDownload.SetMetadata(MetadataKeys.Version, packVersion);
                packagesToDownload.Add(runtimePackToDownload);
            }

            runtimePackItem.SetMetadata(MetadataKeys.RuntimeIdentifier, hostRid);
            Log.LogMessage(MessageImportance.Low, $"Added {packName} runtime pack '{runtimePackName}@{packVersion}'");

            var context = new ToolPackResolutionContext(packNamePattern, packVersion, hostRid, packSupportedRids);
            return (runtimePackItem, context, ToolPackSupport.Supported);
        }

        private ToolPackSupport AddILCompilerCrossTargetPacks(
            ToolPackResolutionContext context,
            ITaskItem knownPack,
            RuntimeGraph runtimeGraph,
            List<ITaskItem> packagesToDownload)
        {
            var aotPackRuntimeIdentifiers = BuildAotPackRidList();

            foreach (var aotPackRuntimeIdentifier in aotPackRuntimeIdentifiers)
            {
                Log.LogMessage(MessageImportance.Low, $"Checking for cross-targeting compilation packs for {aotPackRuntimeIdentifier}");
                var targetRid = NuGetUtils.GetBestMatchingRid(runtimeGraph, aotPackRuntimeIdentifier, context.SupportedRuntimeIdentifiers, out _);
                if (targetRid == null)
                {
                    if (aotPackRuntimeIdentifier == EffectiveRuntimeIdentifier)
                    {
                        // We can't find the right pack for AOT, return an error
                        return ToolPackSupport.UnsupportedForTargetRuntimeIdentifier;
                    }
                    else
                    {
                        // When processing additional RIDs, don't error out during restore, we just won't have a pack to download.
                        // If a publish is attempted for that RID later, it will fail then.
                        Log.LogMessage(MessageImportance.Low, $"No compilation pack found for {aotPackRuntimeIdentifier}");
                        continue;
                    }
                }

                // If there's an available runtime pack, use it instead of the ILCompiler package for target-specific bits.
                bool useRuntimePackForAllTargets = false;
                string targetPackNamePattern = context.PackNamePattern;
                if (knownPack.GetMetadata("ILCompilerRuntimePackNamePattern") is string runtimePackNamePattern && runtimePackNamePattern != string.Empty)
                {
                    targetPackNamePattern = runtimePackNamePattern;
                    useRuntimePackForAllTargets = true;
                }

                if (ShouldAddCrossTargetILCompilerPack(useRuntimePackForAllTargets, context.HostRuntimeIdentifier, targetRid))
                {
                    var targetIlcPackName = targetPackNamePattern.Replace("**RID**", targetRid);
                    var targetIlcPack = new TaskItem(targetIlcPackName);
                    targetIlcPack.SetMetadata(MetadataKeys.NuGetPackageId, targetIlcPackName);
                    targetIlcPack.SetMetadata(MetadataKeys.NuGetPackageVersion, context.PackVersion);
                    if (aotPackRuntimeIdentifier == EffectiveRuntimeIdentifier)
                    {
                        TargetILCompilerPacks = new[] { targetIlcPack };
                        Log.LogMessage(MessageImportance.Low, $"Added {targetIlcPackName}@{context.PackVersion} for cross-targeting compilation for {aotPackRuntimeIdentifier}");
                    }

                    string? targetILCompilerPackPath = GetPackPath(targetIlcPackName, context.PackVersion);
                    if (targetILCompilerPackPath != null)
                    {
                        targetIlcPack.SetMetadata(MetadataKeys.PackageDirectory, targetILCompilerPackPath);
                    }
                    else if (EnableRuntimePackDownload)
                    {
                        // We need to download the runtime pack
                        var targetIlcPackToDownload = new TaskItem(targetIlcPackName);
                        targetIlcPackToDownload.SetMetadata(MetadataKeys.Version, context.PackVersion);
                        packagesToDownload.Add(targetIlcPackToDownload);
                        Log.LogMessage(MessageImportance.Low, $"Added PackageDownload for {targetIlcPackName}@{context.PackVersion} for cross-targeting compilation for {aotPackRuntimeIdentifier}");
                    }
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"No cross-targeting compilation packs required for {aotPackRuntimeIdentifier}.");
                }
            }

            return ToolPackSupport.Supported;
        }

        private List<string> BuildAotPackRidList()
        {
            var aotPackRuntimeIdentifiers = new List<string>();
            if (EffectiveRuntimeIdentifier != null)
                aotPackRuntimeIdentifiers.Add(EffectiveRuntimeIdentifier);

            foreach (var aotPackRuntimeIdentifier in RuntimeIdentifiers ?? Array.Empty<string>())
            {
                if (aotPackRuntimeIdentifier != EffectiveRuntimeIdentifier)
                    aotPackRuntimeIdentifiers.Add(aotPackRuntimeIdentifier);
            }

            return aotPackRuntimeIdentifiers;
        }

        private ToolPackSupport CheckILLinkAnalyzerSupport(Version normalizedTargetFrameworkVersion)
        {
            if (FirstTargetFrameworkVersionToSupportAotAnalyzer != null)
            {
                var firstVersion = NormalizeVersion(new Version(FirstTargetFrameworkVersionToSupportAotAnalyzer));
                if ((IsAotCompatible || EnableAotAnalyzer) && normalizedTargetFrameworkVersion < firstVersion)
                    return ToolPackSupport.UnsupportedForTargetFramework;
            }

            if (FirstTargetFrameworkVersionToSupportSingleFileAnalyzer != null)
            {
                var firstVersion = NormalizeVersion(new Version(FirstTargetFrameworkVersionToSupportSingleFileAnalyzer));
                if (EnableSingleFileAnalyzer && normalizedTargetFrameworkVersion < firstVersion)
                    return ToolPackSupport.UnsupportedForTargetFramework;
            }

            if (FirstTargetFrameworkVersionToSupportTrimAnalyzer != null)
            {
                var firstVersion = NormalizeVersion(new Version(FirstTargetFrameworkVersionToSupportTrimAnalyzer));
                if ((IsTrimmable || EnableTrimAnalyzer) && normalizedTargetFrameworkVersion < firstVersion)
                    return ToolPackSupport.UnsupportedForTargetFramework;
            }

            return ToolPackSupport.Supported;
        }

        private bool IsWindowsOnlyUnsupportedOnCurrentPlatform(KnownFrameworkReference knownFrameworkReference) =>
            knownFrameworkReference.IsWindowsOnly &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !EnableWindowsTargeting;

        private bool ShouldDownloadTargetingPack(ITaskItem? frameworkReference) =>
            EnableTargetingPackDownload &&
            !(DisableTransitiveFrameworkReferenceDownloads && frameworkReference == null);

        private bool ShouldDownloadRuntimePack(string? runtimePackPath, bool wasReferencedDirectly) =>
            EnableRuntimePackDownload &&
            runtimePackPath == null &&
            (wasReferencedDirectly || !DisableTransitiveFrameworkReferenceDownloads);

        private static bool ShouldUsePortableRid(string? sdkPortableRid, string? supportedTargetRid, string? supportedPortableTargetRid) =>
            !string.IsNullOrEmpty(sdkPortableRid) && supportedTargetRid == supportedPortableTargetRid;

        private static bool ShouldAddCrossTargetILCompilerPack(bool useRuntimePackForAllTargets, string hostRid, string targetRid) =>
            useRuntimePackForAllTargets || !hostRid.Equals(targetRid);

        private static bool IsPreNet8ILLinkPack(string packVersion) =>
            new VersionComparer(VersionComparison.Version).Compare(NuGetVersion.Parse(packVersion), new NuGetVersion(8, 0, 0)) < 0;

        private string GetRuntimeFrameworkVersion(
            ITaskItem? frameworkReference,
            KnownFrameworkReference knownFrameworkReference,
            KnownRuntimePack? knownRuntimePack,
            out string runtimePackVersion)
        {
            //  Precedence order for selecting runtime framework version
            //  - RuntimeFrameworkVersion metadata on FrameworkReference item
            //  - RuntimeFrameworkVersion MSBuild property
            //  - Then, use either the LatestRuntimeFrameworkVersion or the DefaultRuntimeFrameworkVersion of the KnownFrameworkReference, based on
            //      - The value (if set) of TargetLatestRuntimePatch metadata on the FrameworkReference
            //      - The TargetLatestRuntimePatch MSBuild property (which defaults to True if SelfContained is true, and False otherwise)
            //      - But, if TargetLatestRuntimePatch was defaulted and not overridden by user, then acquire latest runtime pack for future
            //        self-contained deployment (or for crossgen of framework-dependent deployment), while targeting the default version.

            string? requestedVersion = GetRequestedRuntimeFrameworkVersion(frameworkReference);
            if (!string.IsNullOrEmpty(requestedVersion))
            {
                runtimePackVersion = requestedVersion;
                return requestedVersion;
            }

            switch (GetRuntimePatchRequest(frameworkReference))
            {
                case RuntimePatchRequest.UseDefaultVersion:
                    runtimePackVersion = knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                    return knownFrameworkReference.DefaultRuntimeFrameworkVersion;

                case RuntimePatchRequest.UseLatestVersion:
                    if (knownRuntimePack is KnownRuntimePack knownPack)
                    {
                        runtimePackVersion = knownPack.LatestRuntimeFrameworkVersion;
                        return knownPack.LatestRuntimeFrameworkVersion;
                    }
                    else
                    {
                        runtimePackVersion = knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                        return knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                    }
                case RuntimePatchRequest.UseDefaultVersionWithLatestRuntimePack:
                    if (knownRuntimePack is KnownRuntimePack knownPack2)
                    {
                        runtimePackVersion = knownPack2.LatestRuntimeFrameworkVersion;
                    }
                    else
                    {
                        runtimePackVersion = knownFrameworkReference.DefaultRuntimeFrameworkVersion;
                    }
                    return knownFrameworkReference.DefaultRuntimeFrameworkVersion;

                default:
                    // Unreachable
                    throw new InvalidOperationException();
            }
        }

        private string? GetPackPath(string packName, string packVersion)
        {
            IEnumerable<string> GetPackFolders()
            {
                var packRootEnvironmentVariable = Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_PACK_ROOTS);
                if (!string.IsNullOrEmpty(packRootEnvironmentVariable))
                {
                    foreach (var packRoot in packRootEnvironmentVariable.Split(Path.PathSeparator))
                    {
                        yield return Path.Combine(packRoot, "packs");
                    }
                }

                if (!string.IsNullOrEmpty(NetCoreRoot) && !string.IsNullOrEmpty(NETCoreSdkVersion))
                {
                    if (WorkloadFileBasedInstall.IsUserLocal(NetCoreRoot, NETCoreSdkVersion) &&
                        new CliFolderPathCalculatorCore().GetDotnetUserProfileFolderPath() is { } userProfileDir)
                    {
                        yield return Path.Combine(userProfileDir, "packs");
                    }
                }

                if (!string.IsNullOrEmpty(TargetingPackRoot))
                {
                    yield return TargetingPackRoot;
                }
            }

            foreach (var packFolder in GetPackFolders())
            {
                string packPath = Path.Combine(packFolder, packName, packVersion);
                if (Directory.Exists(packPath))
                {
                    return packPath;
                }
            }

            return null;
        }

        Lazy<WorkloadResolver> _workloadResolver
        {
            get
            {
                field ??= LazyCreateWorkloadResolver();
                return field;
             }
        }

        private string GetResolvedPackVersion(string packID, string packVersion)
        {
            if (!packVersion.Equals("**FromWorkload**", StringComparison.OrdinalIgnoreCase))
            {
                return packVersion;
            }

            var packInfo = _workloadResolver.Value.TryGetPackInfo(new WorkloadPackId(packID));
            if (packInfo == null)
            {
                Log.LogError(Strings.CouldNotGetPackVersionFromWorkloadManifests, packID);
                return packVersion;
            }
            return packInfo.Version;
        }

        private Lazy<WorkloadResolver> LazyCreateWorkloadResolver()
        {
            return new(() =>
        {
                string? userProfileDir = new CliFolderPathCalculatorCore().GetDotnetUserProfileFolderPath();

                //  When running MSBuild tasks, the current directory is always the project directory, so we can use that as the
                //  starting point to search for global.json
                string? globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory);

                var manifestProvider = new SdkDirectoryWorkloadManifestProvider(NetCoreRoot, NETCoreSdkVersion, userProfileDir, globalJsonPath);
                return WorkloadResolver.Create(manifestProvider, NetCoreRoot, NETCoreSdkVersion, userProfileDir);
        });
        }

        private enum RuntimePatchRequest
        {
            UseDefaultVersionWithLatestRuntimePack,
            UseDefaultVersion,
            UseLatestVersion,
        }

        /// <summary>
        /// Compare PackageToDownload by name and version.
        /// Used to deduplicate PackageToDownloads
        /// </summary>
        private class PackageToDownloadComparer<T> : IEqualityComparer<T> where T : ITaskItem
        {
            public bool Equals(T? x, T? y)
            {
                if (x is null || y is null)
                {
                    return false;
                }

                return x.ItemSpec.Equals(y.ItemSpec,
                           StringComparison.OrdinalIgnoreCase) &&
                       x.GetMetadata(MetadataKeys.Version).Equals(
                           y.GetMetadata(MetadataKeys.Version), StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(T obj)
            {
                var hashCode = -1923861349;
                hashCode = hashCode * -1521134295 + obj.ItemSpec.GetHashCode();
                hashCode = hashCode * -1521134295 + obj.GetMetadata(MetadataKeys.Version).GetHashCode();
                return hashCode;
            }
        }

        private RuntimePatchRequest GetRuntimePatchRequest(ITaskItem? frameworkReference)
        {
            string? value = frameworkReference?.GetMetadata("TargetLatestRuntimePatch");
            if (!string.IsNullOrEmpty(value))
            {
                return MSBuildUtilities.ConvertStringToBool(value, defaultValue: false)
                    ? RuntimePatchRequest.UseLatestVersion
                    : RuntimePatchRequest.UseDefaultVersion;
            }

            if (TargetLatestRuntimePatch)
            {
                return RuntimePatchRequest.UseLatestVersion;
            }

            return TargetLatestRuntimePatchIsDefault
                ? RuntimePatchRequest.UseDefaultVersionWithLatestRuntimePack
                : RuntimePatchRequest.UseDefaultVersion;
        }

        private string? GetRequestedRuntimeFrameworkVersion(ITaskItem? frameworkReference)
        {
            string? requestedVersion = frameworkReference?.GetMetadata("RuntimeFrameworkVersion");

            if (string.IsNullOrEmpty(requestedVersion))
            {
                requestedVersion = RuntimeFrameworkVersion;
            }

            return requestedVersion;
        }

        internal static Version NormalizeVersion(Version version)
        {
            if (version.Revision == 0)
            {
                if (version.Build == 0)
                {
                    return new Version(version.Major, version.Minor);
                }
                else
                {
                    return new Version(version.Major, version.Minor, version.Build);
                }
            }

            return version;
        }

        private struct KnownFrameworkReference
        {
            ITaskItem _item;
            public KnownFrameworkReference(ITaskItem item)
            {
                _item = item;
                TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
            }

            //  The name / itemspec of the FrameworkReference used in the project
            public string Name => _item.ItemSpec;

            //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public string RuntimeFrameworkName => _item.GetMetadata(MetadataKeys.RuntimeFrameworkName);
            public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");

            //  The ID of the targeting pack NuGet package to reference
            public string TargetingPackName => _item.GetMetadata("TargetingPackName");
            public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");
            public string TargetingPackFormat => _item.GetMetadata("TargetingPackFormat");

            public string RuntimePackRuntimeIdentifiers => _item.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

            public bool IsWindowsOnly => _item.HasMetadataValue("IsWindowsOnly", "true");

            public bool RuntimePackAlwaysCopyLocal =>
                _item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

            public string Profile => _item.GetMetadata("Profile");

            public NuGetFramework TargetFramework { get; }

            public KnownRuntimePack ToKnownRuntimePack()
            {
                return new KnownRuntimePack(_item);
            }
        }

        [DebuggerDisplay("{Name}@{LatestRuntimeFrameworkVersion} for {TargetFramework}")]
        private struct KnownRuntimePack(ITaskItem item)
        {

            //  The name / itemspec of the FrameworkReference used in the project
            public readonly string Name => item.ItemSpec;

            //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public readonly string LatestRuntimeFrameworkVersion => item.GetMetadata("LatestRuntimeFrameworkVersion");

            public readonly string RuntimePackNamePatterns => item.GetMetadata("RuntimePackNamePatterns");

            public readonly bool HasRuntimePackages => !string.IsNullOrEmpty(RuntimePackNamePatterns);

            public readonly string RuntimePackRuntimeIdentifiers => item.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

            public readonly string RuntimePackExcludedRuntimeIdentifiers => item.GetMetadata(MetadataKeys.RuntimePackExcludedRuntimeIdentifiers);

            public readonly string IsTrimmable => item.GetMetadata(MetadataKeys.IsTrimmable);

            public readonly bool IsWindowsOnly => item.HasMetadataValue("IsWindowsOnly", "true");

            public readonly bool RuntimePackAlwaysCopyLocal =>
                item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

            public readonly string[] RuntimePackLabels => item.GetMetadata(MetadataKeys.RuntimePackLabels) is string s ? s.Split([';'], StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();

            public readonly NuGetFramework TargetFramework => NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
        }

        /// <summary>
        /// Accumulates the five output lists and the unrecognised-RID set that are built up
        /// while processing framework references.  Passing this object instead of five individual
        /// <see cref="List{T}"/> parameters keeps method signatures focused.
        /// </summary>
        private sealed class PacksAccumulator
        {
            public List<ITaskItem> PackagesToDownload { get; } = new();
            public List<ITaskItem> RuntimeFrameworks { get; } = new();
            public List<ITaskItem> TargetingPacks { get; } = new();
            public List<ITaskItem> RuntimePacks { get; } = new();
            public List<ITaskItem> UnavailableRuntimePacks { get; } = new();
            public HashSet<string> UnrecognisedRuntimeIdentifiers { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Captures all per-framework-reference data needed by <see cref="ProcessRuntimeIdentifiersForFrameworkReference"/>.</summary>
        private readonly record struct FrameworkReferenceState(
            KnownFrameworkReference KnownFrameworkReference,
            KnownRuntimePack? SelectedRuntimePack,
            string RuntimePackVersion,
            ITaskItem? FrameworkReference);

        /// <summary>Captures the per-RID resolution options used by <see cref="ProcessRuntimeIdentifier"/>.</summary>
        private readonly record struct RuntimePackResolutionOptions(
            List<string>? AdditionalFrameworkReferences,
            bool AddPackAndDownloadIfNecessary,
            bool WasReferencedDirectly,
            bool DownloadOnly);

        /// <summary>Carries the resolved host-RID context from <see cref="ResolveHostRidForToolPack"/> to <see cref="AddILCompilerCrossTargetPacks"/>.</summary>
        private readonly record struct ToolPackResolutionContext(
            string PackNamePattern,
            string PackVersion,
            string HostRuntimeIdentifier,
            string[] SupportedRuntimeIdentifiers);

        /// <summary>Bundles the three inputs needed to describe a targeting pack item being created.</summary>
        private readonly record struct TargetingPackDescriptor(
            KnownFrameworkReference KnownFrameworkReference,
            KnownRuntimePack? SelectedRuntimePack,
            string TargetingPackVersion);

        /// <summary>Carries the context needed by <see cref="ProcessAdditionalRids"/> to process each supplementary RuntimeIdentifier.</summary>
        private readonly record struct AdditionalRidProcessingContext(
            KnownRuntimePack RuntimePackForRID,
            string RuntimePackVersion,
            bool UsePackAndDownload,
            bool ProcessedPrimaryRid,
            ITaskItem? FrameworkReference);
    }
}
