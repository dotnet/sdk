// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Cli.ToolPackage.ToolConfigurationDeserialization;

[DebuggerStepThrough]
[XmlRoot(Namespace = "", IsNullable = false)]
public class DotNetCliTool
{
    [XmlArrayItem("Command", IsNullable = false)]
    public DotNetCliToolCommand[] Commands { get; set; }

    [XmlArrayItem("RuntimeIdentifierPackage", IsNullable = false)]
    public DotNetCliToolRuntimeIdentifierPackage[] RuntimeIdentifierPackages { get; set; }

    [XmlAttribute(AttributeName = "Version")]
    public string Version { get; set; }
}
