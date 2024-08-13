// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public class ReadNuGetPackageInfos : Task
    {
        [Required]
        public string[] PackagePaths { get; set; }

        /// <summary>
        /// %(Identity): Path to the original nupkg.
        /// %(PackageId): Identity of the package.
        /// %(PackageVersion): Version of the package.
        /// </summary>
        [Output]
        public ITaskItem[] PackageInfoItems { get; set; }

        public override bool Execute()
        {
            PackageInfoItems = PackagePaths
                .Select(p =>
                {
                    PackageIdentity identity = ReadIdentity(p);
                    return new TaskItem(
                        p,
                        new Dictionary<string, string>
                        {
                            ["PackageId"] = identity.Id,
                            ["PackageVersion"] = identity.Version.OriginalVersion
                        });
                })
                .ToArray();

            return !Log.HasLoggedErrors;
        }

        public static PackageIdentity ReadIdentity(string nupkgFile)
        {
            using (var reader = new PackageArchiveReader(nupkgFile))
            {
                return reader.GetIdentity();
            }
        }
    }
}
