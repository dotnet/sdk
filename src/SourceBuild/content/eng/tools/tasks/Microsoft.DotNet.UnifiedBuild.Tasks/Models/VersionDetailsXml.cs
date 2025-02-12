// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Xml.Serialization;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.Models
{
    [XmlRoot("Dependencies")]
    public class VersionDetails
    {
        [XmlArray("ToolsetDependencies")]
        public Dependency[] ToolsetDependencies { get; set; }
        [XmlArray("ProductDependencies")]
        public Dependency[] ProductDependencies { get; set; }
    }

    public class Dependency
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string Version { get; set; }
        [XmlAttribute]
        public string CoherentParentDependency { get; set; }
        [XmlAttribute]
        public bool Pinned { get; set; }
        // Uri type isn't serializable, so use a string instead
        public string Uri { get; set; }
        public string Sha { get; set; }
        [XmlElement("RepoName")]
        public string[] RepoNames { get; set; }

        public override string ToString()
        {
            return $"{Name}@{Version} ({Uri}@{Sha})";
        }
    }
}
