// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// For stage 2, we want use the version numbers from stage 0 for the downlevel .NET versions.  This is because
    /// the latest patches of different major versions are built entirely separately, and we want to have tests
    /// on downlevel versions but we can't depend on the latest patches being available in test environments.
    ///
    /// So we copy the version numbers from stage 0 for those downlevel versions.  This also applies to the downlevel
    /// runtime pack versions in the Mono toolchain workload manifest, which override the bundled versions for
    /// browser-wasm and mobile projects.
    ///
    /// However, if the stage 2 version is a preview version, we don't overwrite it.  This is because in this case
    /// the version of .NET hasn't been released yet, so we want to use the later preview version.  The preview
    /// versions should all be on the same NuGet feeds anyway.
    /// </summary>
    public sealed class OverrideAndCreateBundledNETCoreAppPackageVersion : Task
    {
        [Required] public string Stage0BundledVersionsPath { get; set; }
        [Required] public string Stage2BundledVersionsPath { get; set; }

        /// <summary>
        /// WorkloadManifest.targets files of the microsoft.net.workload.mono.toolchain.current manifest in stage 0.
        /// The one with the highest manifest version is used.
        /// </summary>
        public ITaskItem[] Stage0MonoToolchainManifestTargetsPaths { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// WorkloadManifest.targets files of the microsoft.net.workload.mono.toolchain.current manifest in stage 2.
        /// The Mono workload targets override the runtime/targeting/WebAssembly SDK pack versions for downlevel
        /// browser-wasm and mobile projects, so their downlevel versions are replaced with the stage 0 ones as well.
        /// </summary>
        public ITaskItem[] Stage2MonoToolchainManifestTargetsPaths { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            try
            {
                var stage0Doc = XDocument.Load(Stage0BundledVersionsPath);
                var stage2Doc = XDocument.Load(Stage2BundledVersionsPath);
                var ns = stage2Doc.Root.Name.Namespace;

                // Load all items from all ItemGroups
                var items2 = stage2Doc.Root.Elements(ns + "ItemGroup").SelectMany(ig => ig.Elements()).ToList();
                var items0 = stage0Doc.Root.Elements(ns + "ItemGroup").SelectMany(ig => ig.Elements()).ToList();

                // Find latest TargetFramework using NuGetFramework
                var allTargetFrameworks = items2
                    .Select(e => e.Attribute("TargetFramework")?.Value)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .Select(tf => new { Raw = tf, Parsed = NuGetFramework.Parse(tf) })
                    .ToList();
                var latest = allTargetFrameworks
                    .OrderByDescending(tf => tf.Parsed.Version)
                    .FirstOrDefault();
                var latestTargetFramework = latest?.Raw;

                // Helper for matching and updating
                void UpdateItems(string elementName, string[] matchAttrs, string[] updateAttrs)
                {
                    var items2Filtered = items2
                        .Where(e => e.Name.LocalName == elementName && e.Attribute("TargetFramework")?.Value != latestTargetFramework)
                        .ToList();
                    foreach (var item2 in items2Filtered)
                    {
                        var matches0 = items0
                            .Where(e => e.Name.LocalName == elementName && matchAttrs.All(attr => (e.Attribute(attr)?.Value ?? "") == (item2.Attribute(attr)?.Value ?? "")))
                            .ToList();
                        if (matches0.Count == 0)
                        {
                            Log.LogError($"No matching {elementName} in stage 0 for: {string.Join(", ", matchAttrs.Select(a => $"{a}={item2.Attribute(a)?.Value}"))}");
                            continue;
                        }
                        if (matches0.Count > 1)
                        {
                            Log.LogError($"Multiple matches for {elementName} in stage 0 for: {string.Join(", ", matchAttrs.Select(a => $"{a}={item2.Attribute(a)?.Value}"))}");
                            continue;
                        }
                        var item0 = matches0[0];
                        foreach (var updateAttr in updateAttrs)
                        {
                            var v0 = item0.Attribute(updateAttr)?.Value;
                            var v2 = item2.Attribute(updateAttr)?.Value;
                            if (v0 != null && v2 != v0)
                            {
                                bool isPreview = false;
                                if (!string.IsNullOrEmpty(v2))
                                {
                                    try
                                    {
                                        var nugetVersion = NuGetVersion.Parse(v2);
                                        isPreview = nugetVersion.IsPrerelease;
                                    }
                                    catch
                                    {
                                        // If parsing fails, treat as non-preview
                                        isPreview = false;
                                    }
                                }
                                if (!isPreview)
                                {
                                    item2.SetAttributeValue(updateAttr, v0);
                                }
                            }
                        }
                        // Log if other metadata differs
                        foreach (var attr in item2.Attributes())
                        {
                            if (matchAttrs.Contains(attr.Name.LocalName) || updateAttrs.Contains(attr.Name.LocalName))
                                continue;
                            var v0 = item0.Attribute(attr.Name)?.Value;
                            if (v0 != null && v0 != attr.Value)
                                Log.LogMessage(MessageImportance.Low, $"{elementName} {string.Join(", ", matchAttrs.Select(a => $"{a}={item2.Attribute(a)?.Value}"))}: Metadata '{attr.Name}' differs: stage0='{v0}', stage2='{attr.Value}'");
                        }
                    }
                }

                UpdateItems("KnownFrameworkReference", new[] { "Include", "TargetFramework" }, new[] { "LatestRuntimeFrameworkVersion", "TargetingPackVersion" });
                UpdateItems("KnownAppHostPack", new[] { "Include", "TargetFramework" }, new[] { "AppHostPackVersion" });
                UpdateItems("KnownCrossgen2Pack", new[] { "Include", "TargetFramework" }, new[] { "Crossgen2PackVersion" });
                UpdateItems("KnownILCompilerPack", new[] { "Include", "TargetFramework" }, new[] { "ILCompilerPackVersion" });
                UpdateItems("KnownRuntimePack", new[] { "Include", "TargetFramework", "RuntimePackLabels" }, new[] { "LatestRuntimeFrameworkVersion" });
                UpdateItems("KnownILLinkPack", new[] { "Include", "TargetFramework" }, new[] { "ILLinkPackVersion" });
                UpdateItems("KnownAspNetCorePack", new[] { "Include", "TargetFramework" }, new[] { "AspNetCorePackVersion" });

                stage2Doc.Save(Stage2BundledVersionsPath);

                OverrideMonoToolchainManifestVersions();

                return !Log.HasLoggedErrors;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true);
                return false;
            }
        }

        private void OverrideMonoToolchainManifestVersions()
        {
            if (Stage0MonoToolchainManifestTargetsPaths.Length == 0 || Stage2MonoToolchainManifestTargetsPaths.Length == 0)
            {
                return;
            }

            // e.g. sdk-manifests/11.0.100-rc.1/microsoft.net.workload.mono.toolchain.current/11.0.100-rc.1.26425.128/WorkloadManifest.targets
            var stage0Path = Stage0MonoToolchainManifestTargetsPaths
                .Select(i => i.ItemSpec)
                .OrderByDescending(p => NuGetVersion.TryParse(Path.GetFileName(Path.GetDirectoryName(p)), out var v) ? v : new NuGetVersion(0, 0, 0))
                .First();
            var stage0Versions = LoadDownlevelRuntimePackVersions(XDocument.Load(stage0Path))
                .ToDictionary(e => e.Name.LocalName, e => e.Value.Trim());

            foreach (var stage2Path in Stage2MonoToolchainManifestTargetsPaths.Select(i => i.ItemSpec))
            {
                var stage2Doc = XDocument.Load(stage2Path, LoadOptions.PreserveWhitespace);
                bool changed = false;
                foreach (var property in LoadDownlevelRuntimePackVersions(stage2Doc))
                {
                    if (stage0Versions.TryGetValue(property.Name.LocalName, out var v0) &&
                        v0 != property.Value.Trim() &&
                        NuGetVersion.TryParse(property.Value.Trim(), out var v2) && !v2.IsPrerelease)
                    {
                        Log.LogMessage(MessageImportance.Low, $"{stage2Path}: overriding {property.Name.LocalName} '{property.Value}' with stage 0 value '{v0}'.");
                        property.Value = v0;
                        changed = true;
                    }
                }

                if (changed)
                {
                    stage2Doc.Save(stage2Path, SaveOptions.DisableFormatting);
                }
            }
        }

        // _RuntimePackInWorkloadVersion6, _RuntimePackInWorkloadVersion10, etc. but not _RuntimePackInWorkloadVersionCurrent.
        private static IEnumerable<XElement> LoadDownlevelRuntimePackVersions(XDocument doc)
        {
            const string Prefix = "_RuntimePackInWorkloadVersion";
            return doc.Root.Elements(doc.Root.Name.Namespace + "PropertyGroup")
                .SelectMany(pg => pg.Elements())
                .Where(e => e.Name.LocalName.StartsWith(Prefix, StringComparison.Ordinal) &&
                            e.Name.LocalName.Length > Prefix.Length &&
                            e.Name.LocalName.Substring(Prefix.Length).All(char.IsDigit))
                .ToList();
        }
    }
}
