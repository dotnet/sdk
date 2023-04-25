// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    internal class PoisonedFileEntry
    {
        const string ElementName = "File";

        internal byte[] Hash { get; set; }
        internal string Path { get; set; }
        internal PoisonType Type { get; set; }
        internal List<PoisonMatch> Matches { get; }

        internal PoisonedFileEntry()
        {
            this.Matches = new List<PoisonMatch>();
        }

        public XElement ToXml() => this.ToXml(ElementName);

        protected XElement ToXml(string myElementName) => new XElement(myElementName,
            new XAttribute(nameof(Path), Path),
            new XElement(nameof(Hash), Hash.ToHexString()),
            new XElement(nameof(Type), Type.ToString()),
            Matches.Select(m => m.ToXml())
        );
    }
}
