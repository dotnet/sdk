// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class VariableConfig : IVariableConfig
    {
        private VariableConfig(JObject configData)
        {
            Dictionary<string, string> sourceFormats = new Dictionary<string, string>();
            List<string> order = new List<string>();

            if (configData.TryGetValue("sources", System.StringComparison.OrdinalIgnoreCase, out JToken? sourcesData))
            {
                foreach (JObject source in (JArray)sourcesData)
                {
                    string? name = source.ToString("name");
                    string? format = source.ToString("format");

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(format))
                    {
                        sourceFormats[name!] = format!;
                        order.Add(name!);
                    }
                }
            }

            Sources = sourceFormats;
            Order = order;
            FallbackFormat = configData.ToString(nameof(FallbackFormat));
            Expand = configData.ToBool(nameof(Expand));
        }

        private VariableConfig(
            IReadOnlyDictionary<string, string> sources,
            IReadOnlyList<string> order,
            string? fallbackFormat = "{0}",
            bool expand = false)
        {
            Sources = sources;
            Order = order;
            FallbackFormat = fallbackFormat;
            Expand = expand;
        }

        public IReadOnlyDictionary<string, string> Sources { get; private init; }

        public IReadOnlyList<string> Order { get; private init; }

        public string? FallbackFormat { get; private init; }

        public bool Expand { get; private init; }

        internal static IVariableConfig DefaultVariableSetup(string fallbackFormat = "{0}")
        {
            return new VariableConfig(
                new Dictionary<string, string>
                {
                    { "environment", "env_{0}" },
                    { "user", "usr_{0}" }
                },
                new List<string>() { "environment", "user" },
                fallbackFormat ?? "{0}");
        }

        internal static IVariableConfig FromJObject(JObject configData)
        {
            return new VariableConfig(configData);
        }
    }
}
