// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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
        // Common metadata
        private const string IdAttributeName = "Id";
        private const string RepoOriginAttributeName = "RepoOrigin";
        private const string NonShippingAttributeName = "NonShipping";
        private const string DotNetReleaseShippingAttributeName = "DotNetReleaseShipping";
        
        // Package metadata
        private const string PackageElementName = "Package";
        private const string PackageVersionAttributeName = "Version";

        // Blob metadata
        private const string BlobElementName = "Blob";

        /// <summary>
        /// A list of asset manifests to read.
        /// </summary>
        [Required]
        public required ITaskItem[] AssetManifests { get; set; }

        /// <summary>
        /// If provided, only artifacts from that repository will be returned.
        /// </summary>
        public string? RepoOrigin { get; set; }

        /// <summary>
        /// The list of known packages including their versions as metadata.
        /// </summary>
        [Output]
        public ITaskItem[]? KnownPackages { get; set; }

        /// <summary>
        /// The list of known blobs.
        /// </summary>
        [Output]
        public ITaskItem[]? KnownBlobs { get; set; }

        public override bool Execute()
        {
            XDocument[] xDocuments = AssetManifests
                .Select(manifest => XDocument.Load(manifest.ItemSpec))
                .ToArray();

            KnownPackages = xDocuments
                .SelectMany(doc => doc.Root!.Descendants(PackageElementName))
                .Where(ShouldIncludeElement)
                .Distinct()
                .Select(package => new TaskItem(package.Attribute(IdAttributeName)!.Value, new Dictionary<string, string>
                {
                    { PackageVersionAttributeName, package.Attribute(PackageVersionAttributeName)!.Value },
                    { RepoOriginAttributeName, package.Attribute(RepoOriginAttributeName)?.Value ?? string.Empty },
                    { NonShippingAttributeName, package.Attribute(NonShippingAttributeName)?.Value ?? string.Empty },
                    { DotNetReleaseShippingAttributeName, package.Attribute(DotNetReleaseShippingAttributeName)?.Value ?? string.Empty }
                }))
                .ToArray();

            KnownBlobs = xDocuments
                .SelectMany(doc => doc.Root!.Descendants(BlobElementName))
                .Where(ShouldIncludeElement)
                .Distinct()
                .Select(blob => new TaskItem(blob.Attribute(IdAttributeName)!.Value, new Dictionary<string, string>
                {
                    { RepoOriginAttributeName, blob.Attribute(RepoOriginAttributeName)?.Value ?? string.Empty },
                    { NonShippingAttributeName, blob.Attribute(NonShippingAttributeName)?.Value ?? string.Empty },
                    { DotNetReleaseShippingAttributeName, blob.Attribute(DotNetReleaseShippingAttributeName)?.Value ?? string.Empty }
                }))
                .ToArray();

            return true;
        }

        private bool ShouldIncludeElement(XElement element) => RepoOrigin == null || element.Attribute(RepoOriginAttributeName)?.Value == RepoOrigin;
    }
}
