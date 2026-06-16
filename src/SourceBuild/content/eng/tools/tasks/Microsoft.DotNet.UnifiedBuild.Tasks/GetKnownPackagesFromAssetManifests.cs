// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    /// <summary>
    /// Get a list of MSBuild Items that represent the packages described in the asset manifests.
    /// </summary>
    public sealed class GetKnownArtifactsFromAssetManifests : Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] AssetManifests { get; set; }

        [Output]
        public ITaskItem[] KnownPackages { get; set; }

        [Output]
        public ITaskItem[] KnownBlobs { get; set; }

        public override bool Execute()
        {
            XDocument[] xDocuments = AssetManifests
                .Select(manifest => XDocument.Load(manifest.ItemSpec))
                .ToArray();

            KnownPackages = xDocuments
                .SelectMany(doc => doc.Root!.Descendants("Package"))
                .Select(package => new TaskItem(package.Attribute("Id")!.Value, new Dictionary<string, string> { { "Version", package.Attribute("Version")!.Value } }))
                .ToArray();

            KnownBlobs = xDocuments
                .SelectMany(doc => doc.Root!.Descendants("Blob"))
                .Select(asset => new TaskItem(asset.Attribute("Id")!.Value))
                .ToArray();

            return true;
        }
    }
}
