// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Xml.Serialization;

namespace Microsoft.DotNet.ToolPackage.ToolConfigurationDeserialization
{
    [DebuggerStepThrough]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public class DotNetCliTool
    {
        [XmlArrayItem("Command", IsNullable = false)]
        public DotNetCliToolCommand[] Commands { get; set; }

        [XmlAttribute(AttributeName = "Version")]
        public string Version { get; set; }
    }
}
