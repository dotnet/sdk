// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;

namespace Microsoft.DotNet.Watch
{
    [DataContract]
    internal sealed class MSBuildFileSetResult
    {
        [DataMember]
        public required Dictionary<string, ProjectItems> Projects { get; init; }
    }

    [DataContract]
    internal sealed class ProjectItems
    {
        public HashSet<string> FileSetBuilder { get; init; } = [];
        public Dictionary<string, string> StaticFileSetBuilder { get; init; } = [];

        [DataMember]
        public List<string> Files { get; init; } = [];

        [DataMember]
        public List<StaticFileItem> StaticFiles { get; init; } = [];

        public void PrepareForSerialization()
        {
            Files.AddRange(FileSetBuilder);
            StaticFiles.AddRange(StaticFileSetBuilder
                .Select(entry => new StaticFileItem() { FilePath = entry.Key, StaticWebAssetPath = entry.Value }));
        }
    }

    [DataContract]
    internal sealed class StaticFileItem
    {
        [DataMember]
        public required string FilePath { get; init; }

        [DataMember]
        public required string StaticWebAssetPath { get; init; }
    }
}
