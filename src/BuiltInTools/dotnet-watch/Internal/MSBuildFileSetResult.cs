﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.Watcher.Internal
{
    [DataContract]
    internal sealed class MSBuildFileSetResult
    {
        [DataMember]
        public string RunCommand { get; init; }

        [DataMember]
        public string RunArguments { get; init; }

        [DataMember]
        public string RunWorkingDirectory { get; init; }

        [DataMember]
        public bool IsNetCoreApp { get; init; }

        [DataMember]
        public string TargetFrameworkVersion { get; init; }

        [DataMember]
        public string RuntimeIdentifier { get; init; }

        [DataMember]
        public string DefaultAppHostRuntimeIdentifier { get; init; }

        [DataMember]
        public Dictionary<string, ProjectItems> Projects { get; init; }
    }

    [DataContract]
    internal sealed class ProjectItems
    {
        [DataMember]
        public List<string> Files { get; init; } = new();

        [DataMember]
        public List<StaticFileItem> StaticFiles { get; init; } = new();
    }

    [DataContract]
    internal sealed class StaticFileItem
    {
        [DataMember]
        public string FilePath { get; init; }

        [DataMember]
        public string StaticWebAssetPath { get; init; }
    }
}
