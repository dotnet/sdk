// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Updates version numbers in the stage 2 bundled versions file by copying values from the stage 0 file
    /// for items where the TargetFramework is not the latest supported. Matches and updates multiple item types.
    /// </summary>
    public sealed class OverrideAndCreateBundledNETCoreAppPackageVersion : Task
    {
        [Required] public string Stage0BundledVersionsPath { get; set; }
        [Required] public string Stage2BundledVersionsPath { get; set; }
        [Required] public string OutputPath { get; set; }

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
                    .OrderByDescending(tf => tf.Parsed, NuGetFramework.Comparer)
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
                                item2.SetAttributeValue(updateAttr, v0);
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

                stage2Doc.Save(OutputPath);
                return !Log.HasLoggedErrors;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true);
                return false;
            }
        }
    }
}
