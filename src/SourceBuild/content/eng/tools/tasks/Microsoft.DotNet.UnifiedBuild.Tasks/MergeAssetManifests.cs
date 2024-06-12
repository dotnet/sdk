// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public class MergeAssetManifests : Task
    {
        /// <summary>
        /// AssetManifest paths
        /// </summary>
        [Required]
        public required ITaskItem[] AssetManifest { get; init; }

        /// <summary>
        /// Merged asset manifest output path
        /// </summary>
        [Required]
        public required string MergedAssetManifestOutputPath { get; init; }

        /// <summary>
        /// Azure DevOps build number
        /// </summary>
        public string VmrBuildNumber { get; set; } = string.Empty;

        private static readonly string _buildIdAttribute = "BuildId";
        private static readonly string _azureDevOpsBuildNumberAttribute = "AzureDevOpsBuildNumber";
        private static readonly string[] _ignoredAttributes = [_buildIdAttribute, _azureDevOpsBuildNumberAttribute, "IsReleaseOnlyPackageVersion"];

        public override bool Execute()
        {
            List<XDocument> assetManifestXmls = AssetManifest.Select(xmlPath => XDocument.Load(xmlPath.ItemSpec)).ToList();

            VerifyAssetManifests(assetManifestXmls);

            XElement mergedManifestRoot = assetManifestXmls.First().Root 
                ?? throw new ArgumentException("The root element of the asset manifest is null.");

            // Set the BuildId and AzureDevOpsBuildNumber attributes to the value of VmrBuildNumber
            mergedManifestRoot.SetAttributeValue(_buildIdAttribute, VmrBuildNumber);
            mergedManifestRoot.SetAttributeValue(_azureDevOpsBuildNumberAttribute, VmrBuildNumber);

            List<XElement> packageElements = new();
            List<XElement> blobElements = new();

            foreach (var assetManifestXml in assetManifestXmls)
            {
                packageElements.AddRange(assetManifestXml.Descendants("Package"));
                blobElements.AddRange(assetManifestXml.Descendants("Blob"));
            }
            
            packageElements = packageElements.OrderBy(packageElement => packageElement.Attribute("Id")?.Value).ToList();
            blobElements = blobElements.OrderBy(blobElement => blobElement.Attribute("Id")?.Value).ToList();

            XDocument verticalManifest = new(new XElement(mergedManifestRoot.Name, mergedManifestRoot.Attributes(), packageElements, blobElements));

            File.WriteAllText(MergedAssetManifestOutputPath, verticalManifest.ToString());

            return !Log.HasLoggedErrors;
        }

        private static void VerifyAssetManifests(IReadOnlyList<XDocument> assetManifestXmls)
        {
            if (assetManifestXmls.Count == 0)
            {
                throw new ArgumentException("No asset manifests were provided.");
            }

            HashSet<string> rootAttributes = assetManifestXmls
                .First()
                .Root?
                .Attributes()
                .Select(attribute => attribute.ToString())
                .ToHashSet() 
                ?? throw new ArgumentException("The root element of the asset manifest is null.");

            if (assetManifestXmls.Skip(1).Any(manifest => manifest.Root?.Attributes().Count() != rootAttributes.Count))
            {
                throw new ArgumentException("The asset manifests do not have the same number of root attributes.");
            }

            if (assetManifestXmls.Skip(1).Any(assetManifestXml => 
                    !assetManifestXml.Root?.Attributes().Select(attribute => attribute.ToString())
                        .All(attribute =>
                            // Ignore BuildId and AzureDevOpsBuildNumber attributes, they're different for different repos, 
                            // TODO this should be fixed with https://github.com/dotnet/source-build/issues/3934
                            _ignoredAttributes.Any(ignoredAttribute => attribute.StartsWith(ignoredAttribute)) || rootAttributes.Contains(attribute))
                        ?? false))
            {
                throw new ArgumentException("The asset manifests do not have the same root attributes.");
            }
        }
    }
}
