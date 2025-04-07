// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    internal class CatalogFileEntry
    {
        const string ElementName = "File";

        internal string Path { get; set; }
        internal byte[] OriginalHash { get; set; }
        internal byte[] PoisonedHash { get; set; }

        public XElement ToXml() => new XElement(ElementName,
            new XAttribute(nameof(Path), Path),
            new XAttribute(nameof(OriginalHash), OriginalHash.ToHexString()),
            PoisonedHash == null ? null : new XAttribute(nameof(PoisonedHash), PoisonedHash.ToHexString())
        );
    }
}
