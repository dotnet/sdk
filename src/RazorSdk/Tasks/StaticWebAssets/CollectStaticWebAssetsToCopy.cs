// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{

    public class CollectStaticWebAssetsToCopy : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Output]
        public ITaskItem[] AssetsToCopy { get; set; }

        public override bool Execute()
        {
            var copyToOutputFolder = new List<ITaskItem>();
            var normalizedOutputPath = StaticWebAsset.NormalizeContentRootPath(Path.GetFullPath(OutputPath));
            try
            {
                foreach (var asset in Assets.Select(a => StaticWebAsset.FromTaskItem(a)))
                {
                    string fileOutputPath = null;                    
                    if (!(asset.IsDiscovered() || asset.IsComputed()))
                    {
                        Log.LogMessage("Skipping asset '{0}' since source type is '{1}'", asset.Identity, asset.SourceType);
                        continue;
                    }

                    if (asset.IsForReferencedProjectsOnly())
                    {
                        Log.LogMessage("Skipping asset '{0}' since asset mode is '{1}'", asset.Identity, asset.AssetMode);
                    }

                    if (asset.ShouldCopyToOutputDirectory())
                    {
                        // We have an asset we want to copy to the output folder.
                        fileOutputPath = Path.Combine(normalizedOutputPath, asset.RelativePath);
                        copyToOutputFolder.Add(new TaskItem(asset.Identity, new Dictionary<string, string>
                        {
                            ["TargetPath"] = fileOutputPath,
                            ["CopyToOutputDirectory"] = asset.CopyToOutputDirectory
                        }));
                    }
                    else
                    {
                        Log.LogMessage("Skipping asset '{0}' since copy to output directory option is '{1}'", asset.Identity, asset.CopyToOutputDirectory);
                    }
                }

                AssetsToCopy = copyToOutputFolder.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}
