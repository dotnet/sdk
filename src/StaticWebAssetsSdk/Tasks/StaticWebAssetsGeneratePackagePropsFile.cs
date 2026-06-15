// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Xml;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[MSBuildMultiThreadableTask]
public class StaticWebAssetsGeneratePackagePropsFile : Task, IMultiThreadableTask
{
    [Required]
    public string PropsFileImport { get; set; }

    public ITaskItem[] AdditionalImports { get; set; }

    [Required]
    public string BuildTargetPath { get; set; }

    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

    public override bool Execute()
    {
        AbsolutePath buildTargetPath = TaskEnvironment.GetAbsolutePath(BuildTargetPath);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
        var elements = (AdditionalImports ?? []).Select(e => e.ItemSpec).Prepend(PropsFileImport)
            .OrderBy(id => id, StringComparer.Ordinal);
        var root = new XElement("Project");
        foreach (var element in elements)
        {
            root.Add(new XElement("Import", new XAttribute("Project", element)));
        }

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
        WriteFile(data, buildTargetPath);

        return !Log.HasLoggedErrors;
    }

    private void WriteFile(byte[] data, string buildTargetPath)
    {
        var dataHash = ComputeHash(data);
        var fileExists = File.Exists(buildTargetPath);
        var existingFileHash = fileExists ? ComputeHash(File.ReadAllBytes(buildTargetPath)) : "";

        if (!fileExists)
        {
            Log.LogMessage(MessageImportance.Low, $"Creating file '{BuildTargetPath}' because it does not exist.");
            File.WriteAllBytes(buildTargetPath, data);
        }
        else if (!string.Equals(dataHash, existingFileHash, StringComparison.Ordinal))
        {
            Log.LogMessage(MessageImportance.Low, $"Updating '{BuildTargetPath}' file because the hash '{dataHash}' is different from existing file hash '{existingFileHash}'.");
            File.WriteAllBytes(buildTargetPath, data);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, $"Skipping file update because the hash '{dataHash}' has not changed.");
        }
    }

    private static string ComputeHash(byte[] data)
    {
#if !NET9_0_OR_GREATER
        using var sha256 = SHA256.Create();
        var result = sha256.ComputeHash(data);
#else
        var result = SHA256.HashData(data);
#endif
        return Convert.ToBase64String(result);
    }
}
