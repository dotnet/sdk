// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    internal class PoisonMatch
    {
        const string ElementName = "Match";

        internal string Package { get; set; }
        internal string File { get; set; }
        internal string PackageId { get; set; }
        internal string PackageVersion { get; set; }

        public XElement ToXml() => new XElement(ElementName,
            Package == null ? null : new XAttribute(nameof(Package), Package),
            PackageId == null ? null : new XAttribute(nameof(PackageId), PackageId),
            PackageVersion == null ? null : new XAttribute(nameof(PackageVersion), PackageVersion),
            File == null ? null: new XAttribute(nameof(File), File)
        );
    }
}
