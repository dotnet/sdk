// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveApphostShimFromFrameworkReferences : TaskBase
    {
        public string TargetingPackRoot { get; set; }

        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public ITaskItem[] PackAsToolShimAppHostRuntimeIdentifiers { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; }

        [Required]
        public ITaskItem[] FrameworkReferenceWithApphost { get; set; }

        public ITaskItem[] KnownFrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        [Output]
        public ITaskItem[] PackagesToDownload { get; set; }

        [Output]
        public ITaskItem[] PackAsToolShimAppHosts { get; set; }

        protected override void ExecuteCore()
        {
            List<ITaskItem> packagesToDownload = new List<ITaskItem>();

            if (FrameworkReferenceWithApphost.Length == 0)
            {
                return;
            }
            else if (FrameworkReferenceWithApphost.Length > 1)
            {
                throw new ArgumentException($"{nameof(FrameworkReferenceWithApphost)} should contain no more than 1 item.");
            }

            Dictionary<string, KnownFrameworkReference> knownFrameworkReferences =
                KnownFrameworkReference.GetKnownFrameworkReferenceDictionary(
                    KnownFrameworkReferences,
                    TargetFrameworkIdentifier,
                    TargetFrameworkVersion);

            var knownFrameworkReference = knownFrameworkReferences[FrameworkReferenceWithApphost[0].ItemSpec];

            ApphostResolver apphostResolver =
             new ApphostResolver(
                knownFrameworkReference.AppHostPackNamePattern,
                knownFrameworkReference.AppHostRuntimeIdentifiers,
                knownFrameworkReference.LatestRuntimeFrameworkVersion,
                TargetingPackRoot,
                new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath),
                DotNetAppHostExecutableNameWithoutExtension,
                Log);

            if (PackAsToolShimAppHostRuntimeIdentifiers != null)
            {
                List<ITaskItem> packAsToolShimAppHostsList = new List<ITaskItem>();
                foreach (var packAsToolShimAppHostRuntimeIdentifier in PackAsToolShimAppHostRuntimeIdentifiers)
                {
                    var shimApphostAndPackage
                        = apphostResolver.GetAppHostItem(
                            packAsToolShimAppHostRuntimeIdentifier.ItemSpec,
                            "PackAsToolShimAppHost");

                    var PackAsToolShimAppHosts = shimApphostAndPackage.AppHost;
                    packagesToDownload.AddRange(shimApphostAndPackage.AdditionalPackagesToDownload);

                    if (PackAsToolShimAppHosts != null)
                    {
                        packAsToolShimAppHostsList.AddRange(PackAsToolShimAppHosts);
                    }
                }

                PackAsToolShimAppHosts = packAsToolShimAppHostsList.ToArray();
                PackagesToDownload = packagesToDownload.ToArray();
            }
        }
    }
}
