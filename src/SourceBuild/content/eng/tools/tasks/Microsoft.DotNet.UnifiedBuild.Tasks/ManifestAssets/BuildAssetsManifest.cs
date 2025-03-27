// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets
{
    [DebuggerDisplay("{GetDebugDisplay(),nq}")]
    public class BuildAssetsManifest : ObjectWithAttributes
    {
        private List<ManifestAsset> _assets = new List<ManifestAsset>();

        public IList<ManifestAsset> Assets => _assets;

        public string? VerticalName
        {
            get { return GetAttribute(nameof(VerticalName)); }
            set { SetAttribute(nameof(VerticalName), value); }
        }

        public string? Name
        {
            get { return GetAttribute(nameof(Name)); }
            set { SetAttribute(nameof(Name), value); }
        }

        public string? BuildId
        {
            get { return GetAttribute(nameof(BuildId)); }
            set { SetAttribute(nameof(BuildId), value); }
        }

        public static BuildAssetsManifest LoadFromFile(string filePath)
        {
            var doc = XDocument.Load(filePath);
            return LoadFromDocument(doc);
        }

        public static BuildAssetsManifest LoadFromDocument(XDocument document)
        {
            BuildAssetsManifest buildAssetsManifest = new BuildAssetsManifest();
            foreach (var attribute in document.Root!.Attributes())
            {
                buildAssetsManifest.SetAttribute(attribute.Name.LocalName, attribute.Value);
            }
            buildAssetsManifest._assets.AddRange(ManifestHelper.GetAssetsFromManifestXml(document.Root));
            return buildAssetsManifest;
        }

        public void SaveToFile(string filePath)
        {
            XDocument doc = SaveToDocument();
            doc.Save(filePath);
        }

        private const string cRootElementName = "Build";

        public XDocument SaveToDocument()
        {
            XElement rootManifestElement = new XElement(cRootElementName);
            XDocument doc = new XDocument(rootManifestElement);
            foreach (var attribute in AttributeTuples)
            {
                rootManifestElement.Add(new XAttribute(attribute.name, attribute.value));
            }
            ManifestHelper.WriteAssetsToManifestXml(rootManifestElement, true, _assets);
            return doc;
        }

        private string? GetDebugDisplay()
        {
            List<string> debugAttrs = new List<string>();
            void AddAttribute(string name)
            {
                string? value = GetAttribute(name);
                if (value != null)
                {
                    debugAttrs.Add($"{name}: {value}");
                }
            }
            AddAttribute(nameof(VerticalName));
            AddAttribute(nameof(BuildId));
            AddAttribute(nameof(Name));
            if (debugAttrs.Any())
            {
                return string.Join(", ", debugAttrs);
            }
            return base.ToString();
        }
    }

    public enum ManifestAssetType
    {
        /// <summary>
        /// Blob artifact
        /// </summary>
        Blob,
        /// <summary>
        /// NuGet package
        /// </summary>
        Package
    }

    [DebuggerDisplay("{GetDebugDisplay(),nq}")]
    public class ManifestAsset : ObjectWithAttributes
    {
        /// <summary>
        /// Asset type
        /// </summary>
        public ManifestAssetType AssetType { get; set; }

        /// <summary>
        /// Asset name
        /// </summary>
        public required string Id { get; set; }

        public override void SetAttribute(string attributeName, string? value)
        {
            if (attributeName == nameof(Id))
            {
                throw new InvalidOperationException("Use direct Id property of asset!");
            }
            base.SetAttribute(attributeName, value);
        }


        /// <summary>
        /// Version
        /// </summary>
        public string? Version
        {
            get { return GetAttribute(nameof(Version)); }
            set { SetAttribute(nameof(Version), value); }
        }

        /// <summary>
        /// Non shipping asset if true
        /// </summary>
        public bool NonShipping
        {
            get { return GetAttribute(nameof(NonShipping))?.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) ?? false; }
            set { SetAttribute(nameof(NonShipping), value.ToString(CultureInfo.InvariantCulture)); }
        }

        /// <summary>
        /// Tells that asset is shipped as part of release process for .NET
        /// </summary>
        public bool DotNetReleaseShipping
        {
            get { return GetAttribute(nameof(DotNetReleaseShipping))?.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) ?? false; }
            set { SetAttribute(nameof(DotNetReleaseShipping), value.ToString(CultureInfo.InvariantCulture)); }
        }

        /// <summary>
        /// Origin repo which this asset is sourced from
        /// </summary>
        public string? RepoOrigin
        {
            get { return GetAttribute(nameof(RepoOrigin)); }
            set { SetAttribute(nameof(RepoOrigin), value); }
        }

        /// <summary>
        /// Leg by which this artifact was produced by
        /// </summary>
        public string? BuildVertical
        {
            get { return GetAttribute(nameof(BuildVertical)); }
            set { SetAttribute(nameof(BuildVertical), value); }
        }

        /// <summary>
        /// Visibility level of the asset (External, Internal)
        ///  - only External assets are going to merged manifest
        /// </summary>
        public string? Visibility
        {
            get { return GetAttribute(nameof(Visibility)); }
            set { SetAttribute(nameof(Visibility), value); }
        }

        private string GetDebugDisplay()
        {
            if (string.IsNullOrEmpty(Version))
            {
                return $"{AssetType}: {Id}";
            }
            return $"{AssetType}: {Id} [{Version}]";
        }
    }

    public class ObjectWithAttributes
    {
        private List<(string name, string value)> _attributes = new List<(string name, string value)>();

        protected List<(string name, string value)> AttributeTuples => _attributes;

        public IEnumerable<KeyValuePair<string, string>> Attributes
        {
            get
            {
                foreach (var kvp in _attributes)
                {
                    yield return new KeyValuePair<string, string>(kvp.name, kvp.value);
                }
            }
        }

        public virtual void SetAttribute(string attributeName, string? value)
        {
            (string name, string value) attrTuple = _attributes.Find(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.name, attributeName));
            if (value == null)
            {
                _attributes.Remove(attrTuple);
            }
            else
            {
                if (attrTuple.name != null)
                {
                    attrTuple.value = value;
                }
                _attributes.Add((attributeName, value));
            }
        }

        public string? GetAttribute(string attributeName)
        {
            (string name, string value) attrTuple = _attributes.Find(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.name, attributeName));
            if (attrTuple.name == null)
            {
                return null;
            }
            return attrTuple.value;
        }
    }

    public static class ManifestHelper
    {
        public static IReadOnlyList<ManifestAsset> GetAssetsFromManifestXml(XElement manifestRootElement)
        {
            var assets = new List<ManifestAsset>();
            foreach (var element in manifestRootElement.Elements())
            {
                ManifestAssetType assetType;
                if (Enum.TryParse(element.Name.LocalName, out assetType))
                {
                    XAttribute idAttribute = element.Attribute(nameof(ManifestAsset.Id))
                        ?? throw new InvalidDataException($"Asset element attribute Id is missing!"); ;
                    var asset = new ManifestAsset
                    {
                        AssetType = assetType,
                        Id = idAttribute.Value,
                    };
                    foreach (XAttribute attr in element.Attributes().Where(a => a.Name.LocalName != nameof(ManifestAsset.Id)))
                    {
                        asset.SetAttribute(attr.Name.LocalName, attr.Value);
                    }
                    assets.Add(asset);
                }
            }
            return assets;
        }

        public static void WriteAssetsToManifestXml(XElement manifestRootElement, bool append = true, params IEnumerable<ManifestAsset> assets)
        {
            foreach (var asset in assets)
            {
                string nodeName = asset.AssetType switch
                {
                    ManifestAssetType.Package => nameof(ManifestAssetType.Package),
                    ManifestAssetType.Blob => nameof(ManifestAssetType.Blob),
                    _ => throw new InvalidOperationException("Unknown asset type")
                };
                var assetElement = new XElement(nodeName);
                assetElement.Add(new XAttribute(nameof(asset.Id), asset.Id));
                foreach (var attrKvp in asset.Attributes.Where(o => o.Key != nameof(ManifestAsset.Id)))
                {
                    assetElement.Add(new XAttribute(attrKvp.Key, attrKvp.Value));
                }
                manifestRootElement.Add(assetElement);
            }
        }
    }
}
