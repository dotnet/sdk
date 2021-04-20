// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SourceModifier
    {
        [JsonProperty]
        internal string Condition { get; set; }

        [JsonProperty]
        internal JToken CopyOnly { get; set; }

        [JsonProperty]
        internal JToken Include { get; set; }

        [JsonProperty]
        internal JToken Exclude { get; set; }

        [JsonProperty]
        internal JObject Rename { get; set; }
    }
}
