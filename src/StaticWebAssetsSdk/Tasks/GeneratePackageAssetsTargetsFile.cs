// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Xml;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Generates a lightweight .targets file that adds a single StaticWebAssetPackageManifest
/// item pointing to the JSON manifest file. This replaces the heavyweight XML .props files
/// that contained all asset/endpoint data as MSBuild items.
/// </summary>
public class GeneratePackageAssetsTargetsFile : Task
{
    [Required]
    public string PackageId { get; set; }

    [Required]
    public string TargetFilePath { get; set; }

    public string PackagePathPrefix { get; set; } = "staticwebassets";

    /// <summary>
    /// The name of the JSON manifest file (e.g., "MyLib.PackageAssets.json").
    /// </summary>
    [Required]
    public string ManifestFileName { get; set; }

    public override bool Execute()
    {
        var normalizedPrefix = PackagePathPrefix.Replace("/", "\\").TrimStart('\\');

        // Build the lightweight .targets XML
        // Contents:
        // <Project>
        //   <ItemGroup>
        //     <StaticWebAssetPackageManifest Include="$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory){ManifestFileName}'))">
        //       <SourceId>{PackageId}</SourceId>
        //       <ContentRoot>$(MSBuildThisFileDirectory)..\staticwebassets\</ContentRoot>
        //       <PackageRoot>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..'))</PackageRoot>
        //     </StaticWebAssetPackageManifest>
        //   </ItemGroup>
        // </Project>

        var itemGroup = new XElement("ItemGroup");
        var manifestItem = new XElement("StaticWebAssetPackageManifest",
            new XAttribute("Include", $@"$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory){ManifestFileName}'))"),
            new XElement("SourceId", PackageId),
            new XElement("ContentRoot", $@"$(MSBuildThisFileDirectory)..\{normalizedPrefix}\"),
            new XElement("PackageRoot", @"$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..'))"));

        itemGroup.Add(manifestItem);

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
        var fileExists = File.Exists(TargetFilePath);
        var existingFileHash = fileExists ? ComputeHash(File.ReadAllBytes(TargetFilePath)) : "";

        if (!fileExists)
        {
            File.WriteAllBytes(TargetFilePath, data);
        }
        else if (!string.Equals(dataHash, existingFileHash, StringComparison.Ordinal))
        {
            File.WriteAllBytes(TargetFilePath, data);
        }
    }

    private static string ComputeHash(byte[] data)
    {
#if NET6_0_OR_GREATER
        var result = SHA256.HashData(data);
        return Convert.ToBase64String(result);
#else
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(data));
#endif
    }
}
