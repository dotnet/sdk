// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Cli.NugetPackageDownloader;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public class FileList
{
    private FileListFile[] fileField;

    [XmlElement("File")]
    public FileListFile[] File
    {
        get => fileField;
        set => fileField = value;
    }

    public static FileList Deserialize(string pathToXml)
    {
        var files = new List<FileListFile>();
        using var fs = new FileStream(pathToXml, FileMode.Open);
        using var reader = XmlReader.Create(fs);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "File")
            {
                files.Add(new FileListFile
                {
                    Path = reader.GetAttribute("Path"),
                    Permission = reader.GetAttribute("Permission")
                });
            }
        }
        return new FileList { File = files.ToArray() };
    }
}

public class FileListFile
{
    private string pathField;

    private string permissionField;

    [XmlAttribute]
    public string Path
    {
        get => pathField;
        set => pathField = value;
    }

    [XmlAttribute]
    public string Permission
    {
        get => permissionField;
        set => permissionField = value;
    }
}
