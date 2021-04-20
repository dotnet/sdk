// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class BaselineInfo : IBaselineInfo
    {
        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public IReadOnlyDictionary<string, string> DefaultOverrides { get; set; }
    }
}
