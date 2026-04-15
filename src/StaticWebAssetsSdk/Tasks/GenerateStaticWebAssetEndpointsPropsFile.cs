// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class GenerateStaticWebAssetEndpointsPropsFile : Task
    {
        [Required]
        public string TargetPropsFilePath { get; set; }

        public string PackagePathPrefix { get; set; } = "staticwebassets";

        [Required]
        public ITaskItem[] StaticWebAssets { get; set; }

        [Required]
        public ITaskItem[] StaticWebAssetEndpoints { get; set; }

        public override bool Execute()
        {
            var endpoints = StaticWebAssetEndpoint.FromItemGroup(StaticWebAssetEndpoints);
            var assets = StaticWebAssets.Select(StaticWebAsset.FromTaskItem).ToDictionary(a => a.Identity, a => a);
            if (!ValidateArguments(endpoints, assets))
            {
                return false;
            }

            return ExecuteCore(endpoints, assets);
        }

        private bool ExecuteCore(StaticWebAssetEndpoint[] endpoints, Dictionary<string, StaticWebAsset> assets)
        {
            if (endpoints.Length == 0)
            {
                return !Log.HasLoggedErrors;
            }

            var itemGroup = new XElement("ItemGroup");
            var orderedAssets = endpoints.OrderBy(e => e.Route, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.AssetFile, StringComparer.OrdinalIgnoreCase);

            foreach (var element in orderedAssets)
            {
                var asset = assets[element.AssetFile];
                var path = asset.ReplaceTokens(asset.RelativePath, StaticWebAssetTokenResolver.Instance);
                var fullPathExpression = $"""$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..\{StaticWebAsset.Normalize(PackagePathPrefix)}\{StaticWebAsset.Normalize(path).Replace("/","\\")}'))""";

                itemGroup.Add(new XElement(nameof(StaticWebAssetEndpoint),
                    new XAttribute("Include", element.Route),
                    new XElement(nameof(StaticWebAssetEndpoint.AssetFile), fullPathExpression),
                    new XElement(nameof(StaticWebAssetEndpoint.Selectors), new XCData(StaticWebAssetEndpointSelector.ToMetadataValue(element.Selectors))),
                    new XElement(nameof(StaticWebAssetEndpoint.EndpointProperties), new XCData(StaticWebAssetEndpointProperty.ToMetadataValue(element.EndpointProperties))),
                    new XElement(nameof(StaticWebAssetEndpoint.ResponseHeaders), new XCData(StaticWebAssetEndpointResponseHeader.ToMetadataValue(element.ResponseHeaders)))));
            }

            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement("Project", itemGroup);

            document.Add(root);

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = false,
                Async = true
            };

            using var memoryStream = new MemoryStream();
            using (var xmlWriter = XmlWriter.Create(memoryStream, settings))
            {
                document.WriteTo(xmlWriter);
            }

            var data = memoryStream.ToArray();
            WriteFile(data);

            return !Log.HasLoggedErrors;
        }

        private void WriteFile(byte[] data)
        {
            var dataHash = ComputeHash(data);
            var fileExists = File.Exists(TargetPropsFilePath);
            var existingFileHash = fileExists ? ComputeHash(File.ReadAllBytes(TargetPropsFilePath)) : "";

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating file '{TargetPropsFilePath}' does not exist.");
                File.WriteAllBytes(TargetPropsFilePath, data);
            }
            else if (!string.Equals(dataHash, existingFileHash, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating '{TargetPropsFilePath}' file because the hash '{dataHash}' is different from existing file hash '{existingFileHash}'.");
                File.WriteAllBytes(TargetPropsFilePath, data);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping file update because the hash '{dataHash}' has not changed.");
            }
        }

        private static string ComputeHash(byte[] data)
        {
            using var sha256 = SHA256.Create();

            var result = sha256.ComputeHash(data);
            return Convert.ToBase64String(result);
        }

        private bool ValidateArguments(StaticWebAssetEndpoint[] endpoints, Dictionary<string, StaticWebAsset> asset)
        {
            var valid = true;
            foreach (var endpoint in endpoints)
            {
                if (!asset.ContainsKey(endpoint.AssetFile))
                {
                    Log.LogError($"The asset file '{endpoint.AssetFile}' specified in the endpoint '{endpoint.Route}' does not exist.");
                    valid = false;
                }
            }

            return valid;
        }
    }
}
