// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Tasks
{
    internal class RuntimeOptions
    {
        public string Tfm { get; set; }

        public string RollForward { get; set; }

        public RuntimeConfigFramework Framework { get; set; }

        public List<RuntimeConfigFramework> Frameworks { get; set; }

        public List<RuntimeConfigFramework> IncludedFrameworks { get; set; }

        public List<string> AdditionalProbingPaths { get; set; }

        public IDictionary<string, JsonNode> RawOptions { get; } = new Dictionary<string, JsonNode>();

        public RuntimeOptions()
        {
        }
    }
}
