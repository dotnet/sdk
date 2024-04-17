// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Xml.Linq;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.UsageReport
{
    public class AnnotatedUsage
    {
        public Usage Usage { get; set; }

        public string Project { get; set; }
        public string SourceBuildPackageIdCreator { get; set; }
        public string ProdConPackageIdCreator { get; set; }
        public bool EndsUpInOutput { get; set; }
        public bool TestProjectByHeuristic { get; set; }
        public bool TestProjectOnlyByHeuristic { get; set; }
        public bool IsDirectDependency { get; set; }
        public bool IsAutoReferenced { get; set; }

        public XElement ToXml() => new XElement(
            nameof(AnnotatedUsage),
            Usage.ToXml().Attributes(),
            Project.ToXAttributeIfNotNull(nameof(Project)),
            SourceBuildPackageIdCreator.ToXAttributeIfNotNull(nameof(SourceBuildPackageIdCreator)),
            ProdConPackageIdCreator.ToXAttributeIfNotNull(nameof(ProdConPackageIdCreator)),
            TestProjectByHeuristic.ToXAttributeIfTrue(nameof(TestProjectByHeuristic)),
            TestProjectOnlyByHeuristic.ToXAttributeIfTrue(nameof(TestProjectOnlyByHeuristic)),
            IsDirectDependency.ToXAttributeIfTrue(nameof(IsDirectDependency)),
            IsAutoReferenced.ToXAttributeIfTrue(nameof(IsAutoReferenced)),
            EndsUpInOutput.ToXAttributeIfTrue(nameof(EndsUpInOutput)));
    }
}
