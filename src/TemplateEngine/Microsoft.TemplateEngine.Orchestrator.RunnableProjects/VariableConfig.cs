// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class VariableConfig : IVariableConfig
    {
        public IReadOnlyDictionary<string, string> Sources { get; set; }

        public IReadOnlyList<string> Order { get; set; }

        public string FallbackFormat { get; set; }

        public bool Expand { get; set; }

        internal static IVariableConfig DefaultVariableSetup(string fallbackFormat)
        {
            IVariableConfig config = new VariableConfig
            {
                Sources = new Dictionary<string, string>
                {
                    { "environment", "env_{0}" },
                    { "user", "usr_{0}" }
                },
                Order = new List<string>() { "environment", "user" },
                FallbackFormat = fallbackFormat ?? "{0}",
                Expand = false
            };

            return config;
        }

        internal static IVariableConfig FromJObject(JObject configData)
        {
            Dictionary<string, string> sourceFormats = new Dictionary<string, string>();
            List<string> order = new List<string>();

            if (configData.TryGetValue("sources", System.StringComparison.OrdinalIgnoreCase, out JToken sourcesData))
            {
                foreach (JObject source in (JArray)sourcesData)
                {
                    string name = source.ToString("name");
                    string format = source.ToString("format");
                    sourceFormats[name] = format;
                    order.Add(name);
                }
            }

            IVariableConfig config = new VariableConfig
            {
                Sources = sourceFormats,
                Order = order,
                FallbackFormat = configData.ToString(nameof(FallbackFormat)),
                Expand = configData.ToBool(nameof(Expand))
            };

            return config;
        }
    }
}
