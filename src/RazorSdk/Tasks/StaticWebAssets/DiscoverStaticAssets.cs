// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class DiscoverStaticWebAssets : Task
    {
        [Required]
        public ITaskItem[] Candidates { get; set; }

        [Required]
        public string Pattern { get; set; }

        [Required]
        public string SourceId { get; set; }

        [Required]
        public string ContentRoot { get; set; }

        [Required]
        public string BasePath { get; set; }

        [Output]
        public ITaskItem[] DiscoveredStaticWebAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                var matcher = new Matcher().AddInclude(Pattern);
                var assets = new List<ITaskItem>();

                for (var i = 0; i < Candidates.Length; i++)
                {
                    var candidate = Candidates[i];
                    var match = matcher.Match(candidate.ItemSpec);
                    if (!match.HasMatches)
                    {
                        Log.LogMessage("Rejected asset '{0}' for pattern '{1}'", candidate.ItemSpec, Pattern);
                        continue;
                    }

                    var assetCopyOptions = ComputeAssetCopyOptions(candidate);

                    Log.LogMessage("Accepted asset '{0}' for pattern '{1}' with relative path '{2}'", candidate.ItemSpec, Pattern, match.Files.Single().Stem);
                    assets.Add(new TaskItem(candidate.ItemSpec, new Dictionary<string, string>
                    {
                        [nameof(StaticWebAsset.SourceType)] = StaticWebAsset.SourceTypes.Discovered,
                        [nameof(StaticWebAsset.SourceId)] = SourceId,
                        [nameof(StaticWebAsset.ContentRoot)] = ContentRoot,
                        [nameof(StaticWebAsset.BasePath)] = StaticWebAsset.Normalize(BasePath),
                        [nameof(StaticWebAsset.RelativePath)] = StaticWebAsset.Normalize(match.Files.Single().Stem),
                        [nameof(StaticWebAsset.AssetKind)] = assetCopyOptions.AssetKind,
                        [nameof(StaticWebAsset.AssetMode)] = StaticWebAsset.AssetModes.All,
                        [nameof(StaticWebAsset.CopyToOutputDirectory)] = assetCopyOptions.CopyToOutputDirectory,
                        [nameof(StaticWebAsset.CopyToPublishDirectory)] = assetCopyOptions.CopyToPublishDirectory,
                    }));
                }

                DiscoveredStaticWebAssets = assets.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
            }

            return !Log.HasLoggedErrors;
        }

        private static CandidateCopyOptions ComputeAssetCopyOptions(ITaskItem candidate)
        {
            var copyToOutputDirectory = candidate.GetMetadata(nameof(CandidateCopyOptions.CopyToOutputDirectory));
            var copyToPublishDirectory = candidate.GetMetadata(nameof(CandidateCopyOptions.CopyToPublishDirectory));

            return new CandidateCopyOptions()
            {
                CopyToOutputDirectory = string.Equals(copyToOutputDirectory, CandidateCopyOptions.CopyOptions.Never) ? CandidateCopyOptions.CopyOptions.Never :
                string.Equals(copyToOutputDirectory, CandidateCopyOptions.CopyOptions.PreserveNewest) ? CandidateCopyOptions.CopyOptions.PreserveNewest :
                string.Equals(copyToOutputDirectory, CandidateCopyOptions.CopyOptions.Always) ? CandidateCopyOptions.CopyOptions.Always :
                CandidateCopyOptions.CopyOptions.Never,

                CopyToPublishDirectory = string.Equals(copyToPublishDirectory, CandidateCopyOptions.CopyOptions.Never) ? CandidateCopyOptions.CopyOptions.Never :
                string.Equals(copyToPublishDirectory, CandidateCopyOptions.CopyOptions.PreserveNewest) ? CandidateCopyOptions.CopyOptions.PreserveNewest :
                string.Equals(copyToPublishDirectory, CandidateCopyOptions.CopyOptions.Always) ? CandidateCopyOptions.CopyOptions.Always :
                CandidateCopyOptions.CopyOptions.Never
            };
        }

        private struct CandidateCopyOptions
        {
            public string CopyToOutputDirectory { get; set; }

            public string CopyToPublishDirectory { get; set; }

            public string AssetKind => CopyToPublishDirectory == CopyOptions.Never ? StaticWebAsset.AssetKinds.Build :
                CopyToOutputDirectory == CopyOptions.Never ? StaticWebAsset.AssetKinds.Publish : StaticWebAsset.AssetKinds.All;

            public static class CopyOptions
            {
                public const string Never = nameof(Never);
                public const string PreserveNewest = nameof(PreserveNewest);
                public const string Always = nameof(Always);
            }
        }
    }
}
