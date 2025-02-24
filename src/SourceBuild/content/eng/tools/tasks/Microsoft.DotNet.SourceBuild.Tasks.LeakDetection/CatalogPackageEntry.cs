// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    internal class CatalogPackageEntry
    {
        const string ElementName = "Package";

        internal string Path { get; set; }
        internal string Id { get; set; }
        internal string Version { get; set; }
        internal byte[] OriginalHash { get; set; }
        internal byte[] PoisonedHash { get; set; }
        internal List<CatalogFileEntry> Files { get; }

        public CatalogPackageEntry()
        {
            this.Files = new List<CatalogFileEntry>();
        }

        public XElement ToXml() => new XElement(ElementName,
            new XAttribute(nameof(Path), Path),
            new XAttribute(nameof(Id), Id),
            new XAttribute(nameof(Version), Version),
            new XAttribute(nameof(OriginalHash), OriginalHash.ToHexString()),
            PoisonedHash == null ? null : new XAttribute(nameof(PoisonedHash), PoisonedHash.ToHexString()),
            Files.Select(f => f.ToXml())
        );
    }
}
