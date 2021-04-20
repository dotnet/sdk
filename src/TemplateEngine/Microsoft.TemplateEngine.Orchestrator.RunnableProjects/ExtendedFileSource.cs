// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class ExtendedFileSource
    {
        [JsonProperty]
        internal JToken CopyOnly { get; set; }

        [JsonProperty]
        internal JToken Include { get; set; }

        [JsonProperty]
        internal JToken Exclude { get; set; }

        [JsonProperty]
        internal Dictionary<string, string> Rename { get; set; }

        [JsonProperty]
        internal string Source { get; set; }

        [JsonProperty]
        internal string Target { get; set; }

        [JsonProperty]
        internal string Condition { get; set; }

        [JsonProperty]
        internal List<SourceModifier> Modifiers { get; set; }
    }
}
