// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageValidation
{
    public class GetLastStablePackage : TaskBase
    {
        [Required]
        public string PackageId { get; set; }

        public string[] NugetFeeds { get; set; }

        public string LocalPackagesPath { get; set; }

        public bool UseLocalPackagesPath { get; set; }

        [Output]
        public string LastStableVersion { get; set; }

        protected override void ExecuteCore()
        {
            NuGetVersion version = null;
            if (UseLocalPackagesPath)
            {
                IEnumerable<NuGetVersion> versions = null;
                versions = GetLatestPackageFromLocalFeed(LocalPackagesPath);
                NuGetVersion packageVersion = versions?.Where(t => !t.IsPrerelease).OrderByDescending(t => t.Version).FirstOrDefault();
                LastStableVersion = packageVersion?.ToNormalizedString();
                return;
            }

            foreach (string nugetFeed in NugetFeeds)
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(nugetFeed);
                FindPackageByIdResource resource = repository.GetResource<FindPackageByIdResource>();
                SourceCacheContext cache = new SourceCacheContext();
                IEnumerable<NuGetVersion> versions = resource.GetAllVersionsAsync(PackageId, cache, NullLogger.Instance, CancellationToken.None).Result;

                NuGetVersion packageVersion = versions?.Where(t => !t.IsPrerelease).OrderByDescending(t => t.Version).FirstOrDefault();

                if (packageVersion != null)
                {
                    if (version == null)
                    {
                        version = packageVersion;
                    }
                    if (packageVersion > version)
                    {
                        version = packageVersion;
                    }
                }
            }
            LastStableVersion = version?.ToNormalizedString();
        }

        private IEnumerable<NuGetVersion> GetLatestPackageFromLocalFeed(string nugetArtifactsDirectory)
        {
            List<NuGetVersion> nugetVersions = new();
            foreach (string file in Directory.GetDirectories(nugetArtifactsDirectory))
            {
                DirectoryInfo di = new DirectoryInfo(file);
                nugetVersions.Add(new NuGetVersion(di.Name));
            }
            return nugetVersions;
        }
    }
}
