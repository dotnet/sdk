// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Microsoft.DotNet.ToolPackage.ToolConfigurationDeserialization
{
    [Serializable]
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true)]
    public class DotNetCliToolCommand
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string EntryPoint { get; set; }

        [XmlAttribute]
        public string Runner { get; set; }
    }
}
