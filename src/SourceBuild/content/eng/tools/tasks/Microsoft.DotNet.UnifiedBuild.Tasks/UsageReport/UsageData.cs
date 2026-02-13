// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Packaging.Core;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    public class UsageData
    {
        public string CreatedByRid { get; set; }
        public string[] ProjectDirectories { get; set; }
        public PackageIdentity[] NeverRestoredTarballPrebuilts { get; set; }
        public Usage[] Usages { get; set; }

        public XElement ToXml() => new XElement(
            nameof(UsageData),
            CreatedByRid == null ? null : new XElement(
                nameof(CreatedByRid),
                CreatedByRid),
            ProjectDirectories?.Any() != true ? null : new XElement(
                nameof(ProjectDirectories),
                ProjectDirectories
                    .Select(dir => new XElement("Dir", dir))),
            NeverRestoredTarballPrebuilts?.Any() != true ? null : new XElement(
                nameof(NeverRestoredTarballPrebuilts),
                NeverRestoredTarballPrebuilts
                    .OrderBy(id => id)
                    .Select(id => id.ToXElement())),
            Usages?.Any() != true ? null : new XElement(
                nameof(Usages),
                Usages
                    .OrderBy(u => u.PackageIdentity)
                    .ThenByOrdinal(u => u.AssetsFile)
                    .Select(u => u.ToXml())));

        public static UsageData Parse(XElement xml) => new UsageData
        {
            CreatedByRid = xml.Element(nameof(CreatedByRid))
                ?.Value,
            ProjectDirectories = xml.Element(nameof(ProjectDirectories)) == null ? new string[] { } :
                xml.Element(nameof(ProjectDirectories)).Elements()
                .Select(x => x.Value)
                .ToArray(),
            NeverRestoredTarballPrebuilts = xml.Element(nameof(NeverRestoredTarballPrebuilts)) == null ? new PackageIdentity[] { } :
                xml.Element(nameof(NeverRestoredTarballPrebuilts)).Elements()
                .Select(XmlParsingHelpers.ParsePackageIdentity)
                .ToArray(),
            Usages = xml.Element(nameof(Usages)) == null ? new Usage[] { } :
                xml.Element(nameof(Usages)).Elements()
                .Select(Usage.Parse)
                .ToArray()
        };
    }
}
