// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Xml.Serialization;

namespace Microsoft.DotNet.Cli.ToolPackage.ToolConfigurationDeserialization;

[Serializable]
[DebuggerStepThrough]
[XmlType(AnonymousType = true)]
public class DotNetCliToolRuntimeIdentifierPackage
{
    [XmlAttribute]
    public string? RuntimeIdentifier { get; set; }

    [XmlAttribute]
    public string? Id { get; set; }
}
