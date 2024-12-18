// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

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
            .Select(package => new TaskItem(package.Attribute(IdAttributeName)!.Value, new Dictionary<string, string>
            {
                { PackageVersionAttributeName, package.Attribute(PackageVersionAttributeName)!.Value },
                { RepoOriginAttributeName, package.Attribute(RepoOriginAttributeName)?.Value ?? string.Empty },
                { NonShippingAttributeName, package.Attribute(NonShippingAttributeName)?.Value ?? string.Empty },
                { DotNetReleaseShippingAttributeName, package.Attribute(DotNetReleaseShippingAttributeName)?.Value ?? string.Empty }
            }))
            .Distinct(TaskItemManifestEqualityComparer.Instance)
            .ToArray();

        KnownBlobs = xDocuments
            .SelectMany(doc => doc.Root!.Descendants(BlobElementName))
            .Where(ShouldIncludeElement)
            .Select(blob => new TaskItem(blob.Attribute(IdAttributeName)!.Value, new Dictionary<string, string>
            {
                { RepoOriginAttributeName, blob.Attribute(RepoOriginAttributeName)?.Value ?? string.Empty },
                { NonShippingAttributeName, blob.Attribute(NonShippingAttributeName)?.Value ?? string.Empty },
                { DotNetReleaseShippingAttributeName, blob.Attribute(DotNetReleaseShippingAttributeName)?.Value ?? string.Empty }
            }))
            .Distinct(TaskItemManifestEqualityComparer.Instance)
            .ToArray();

        return true;
    }

    private bool ShouldIncludeElement(XElement element) => string.IsNullOrEmpty(RepoOrigin) || element.Attribute(RepoOriginAttributeName)?.Value == RepoOrigin;

    sealed class TaskItemManifestEqualityComparer : IEqualityComparer<TaskItem>
    {
        public static TaskItemManifestEqualityComparer Instance { get; } = new TaskItemManifestEqualityComparer();

        public bool Equals(TaskItem? x, TaskItem? y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (x is null || y is null) return false;

            // For blobs, there's no version metadata so these will be empty.
            string xVersion = x.GetMetadata(PackageVersionAttributeName);
            string yVersion = x.GetMetadata(PackageVersionAttributeName);

            return x.ItemSpec == y.ItemSpec && xVersion == yVersion;
        }

        public int GetHashCode([DisallowNull] TaskItem obj) => HashCode.Combine(obj.ItemSpec, obj.GetMetadata(PackageVersionAttributeName));
    }
}