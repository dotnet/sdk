// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    // This task does the build work of processing the project inputs and producing a set of pseudo-static web assets
    // specific to Blazor.
    public class ComputeBlazorBuildAssets : Task
    {
        [Required]
        public ITaskItem[] Candidates { get; set; }

        [Required]
        public ITaskItem ProjectAssembly { get; set; }

        [Required]
        public ITaskItem ProjectDebugSymbols { get; set; }

        [Required]
        public ITaskItem[] SatelliteAssemblies { get; set; }

        [Required]
        public ITaskItem[] ProjectSatelliteAssemblies { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public bool TimeZoneSupport { get; set; }

        [Required]
        public bool InvariantGlobalization { get; set; }

        [Required]
        public bool CopySymbols { get; set; }

        [Output]
        public ITaskItem[] AssetCandidates { get; set; }

        [Output]
        public ITaskItem[] FilesToRemove { get; set; }

        public override bool Execute()
        {
            var filesToRemove = new List<ITaskItem>();
            var assetCandidates = new List<ITaskItem>();

            try
            {
                for (int i = 0; i < Candidates.Length; i++)
                {
                    var candidate = Candidates[i];
                    if (ShouldFilterCandidate(candidate, TimeZoneSupport, InvariantGlobalization, CopySymbols))
                    {
                        filesToRemove.Add(candidate);
                        continue;
                    }

                    var satelliteAssembly = SatelliteAssemblies.FirstOrDefault(s => s.ItemSpec == candidate.ItemSpec);
                    if (satelliteAssembly != null)
                    {
                        var assetCandidate = new TaskItem(satelliteAssembly);
                        assetCandidate.SetMetadata("AssetRole", "Related");
                        assetCandidate.SetMetadata("AssetTraitName", "Culture");
                        var inferredCulture = assetCandidate.GetMetadata("DestinationSubDirectory").Trim('\\', '/');
                        assetCandidate.SetMetadata("AssetTraitValue", inferredCulture);
                        assetCandidate.SetMetadata("RelativePath", inferredCulture);
                        assetCandidate.SetMetadata("RelatedAsset", assetCandidate.GetMetadata("OriginalItemSpec"));

                        assetCandidates.Add(assetCandidate);
                        continue;
                    }

                    var projectSatelliteAssembly = ProjectSatelliteAssemblies.FirstOrDefault(s => s.ItemSpec == candidate.ItemSpec);
                    if (satelliteAssembly != null)
                    {
                        var assetCandidate = new TaskItem(projectSatelliteAssembly);
                        var projectAssemblyAssetPath = Path.GetFullPath(Path.Combine(
                            OutputPath,
                            "wwwroot",
                            "_framework",
                            ProjectAssembly.GetMetadata("FileName") + ProjectAssembly.GetMetadata("Extension")));

                        assetCandidate.SetMetadata("AssetRole", "Related");
                        assetCandidate.SetMetadata("AssetTraitName", "Culture");
                        assetCandidate.SetMetadata("AssetTraitValue", assetCandidate.GetMetadata("Culture"));
                        assetCandidate.SetMetadata("RelativePath", Path.Combine("_framework", assetCandidate.GetMetadata("TargetPath")));
                        assetCandidate.SetMetadata("RelatedAsset", projectAssemblyAssetPath);

                        assetCandidates.Add(assetCandidate);
                        continue;
                    }

                    var destinationSubPath = candidate.GetMetadata("DestinationSubPath");
                    if (string.IsNullOrEmpty(destinationSubPath))
                    {
                        var relativePath = candidate.GetMetadata("FileName") + candidate.GetMetadata("Extension");
                        candidate.SetMetadata("RelativePath", $"_framework/{relativePath}");
                    }
                    else
                    {
                        candidate.SetMetadata("RelativePath", $"_framework/{destinationSubPath}");
                    }

                    var culture = candidate.GetMetadata("Culture");
                    if (!string.IsNullOrEmpty(culture))
                    {
                        candidate.SetMetadata("AssetRole", "Related");
                        candidate.SetMetadata("AssetTraitName", "Culture");
                        candidate.SetMetadata("AssetTraitValue", culture);
                        var fileName = candidate.GetMetadata("FileName");
                        var suffixIndex = fileName.Length - ".resources".Length;
                        var relatedAssetPath = Path.Combine(
                            OutputPath,
                            "wwwroot",
                            "_framework",
                            fileName.Substring(0, suffixIndex) + ProjectAssembly.GetMetadata("Extension"));

                        candidate.SetMetadata("RelatedAsset", Path.GetFullPath(relatedAssetPath));
                    }

                    assetCandidates.Add(candidate);
                }

                var intermediateAssembly = new TaskItem(ProjectAssembly);
                intermediateAssembly.SetMetadata("RelativePath", $"_framework/{intermediateAssembly.GetMetadata("FileName")}{intermediateAssembly.GetMetadata("Extension")}");
                assetCandidates.Add(intermediateAssembly);

                var debugSymbols = new TaskItem(ProjectDebugSymbols);
                debugSymbols.SetMetadata("RelativePath", $"_framework/{debugSymbols.GetMetadata("FileName")}{debugSymbols.GetMetadata("Extension")}");
                assetCandidates.Add(debugSymbols);

                for (var i = 0; i < assetCandidates.Count; i++)
                {
                    var candidate = assetCandidates[i];
                    ApplyUniqueMetadataProperties(candidate);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                return false;
            }

            FilesToRemove = filesToRemove.ToArray();
            AssetCandidates = assetCandidates.ToArray();

            return !Log.HasLoggedErrors;
        }

        private static void ApplyUniqueMetadataProperties(ITaskItem candidate)
        {
            var extension = candidate.GetMetadata("Extension");
            var filename = candidate.GetMetadata("FileName");
            switch (extension)
            {
                case ".dll":
                    if (string.IsNullOrEmpty(candidate.GetMetadata("AssetTraitName")))
                    {
                        candidate.SetMetadata("AssetTraitName", "BlazorWebAssemblyResource");
                        candidate.SetMetadata("AssetTraitValue", "runtime");
                    }
                    break;
                case ".wasm":
                case ".blat":
                case ".dat" when filename.StartsWith("icudt"):
                    candidate.SetMetadata("AssetTraitName", "BlazorWebAssemblyResource");
                    candidate.SetMetadata("AssetTraitValue", "native");
                    break;
                case ".pdb":
                    candidate.SetMetadata("AssetTraitName", "BlazorWebAssemblyResource");
                    candidate.SetMetadata("AssetTraitValue", "symbol");
                    break;
                default:
                    break;
            }
        }

        public static bool ShouldFilterCandidate(ITaskItem candidate, bool timezoneSupport, bool invariantGlobalization, bool copySymbols)
        {
            var extension = candidate.GetMetadata("Extension");
            var fileName = candidate.GetMetadata("FileName");
            if (extension == ".a" ||
                (!timezoneSupport && extension == ".blat" && fileName == "dotnet.timezones") ||
                (invariantGlobalization && extension == ".dat" && fileName.StartsWith("icudt")) ||
                (fileName == "dotnet" && extension == ".js") ||
                (!copySymbols && extension == ".pdb"))
            {
                return true;
            }

            return false;
        }

    }
}
