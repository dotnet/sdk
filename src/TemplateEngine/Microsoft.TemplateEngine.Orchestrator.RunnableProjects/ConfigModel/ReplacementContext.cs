// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    /// <summary>
    /// Defines the replacement context for the symbol.
    /// </summary>
    public sealed class ReplacementContext
    {
        internal ReplacementContext(string? before, string? after)
        {
            OnlyIfBefore = before;
            OnlyIfAfter = after;
        }

        /// <summary>
        /// Gets the context that should be present before the symbol, in order to replacement to be applied.
        /// Corresponds to "onlyIf.before" JSON property.
        /// </summary>
        public string? OnlyIfBefore { get; }

        /// <summary>
        /// Gets the context that should be present after the symbol, in order to replacement to be applied.
        /// Corresponds to "onlyIf.after" JSON property.
        /// </summary>
        public string? OnlyIfAfter { get; }

        internal static IReadOnlyList<ReplacementContext> FromJObject(JsonObject jObject)
        {
            JsonArray? onlyIf = jObject.Get<JsonArray>("onlyIf");

            if (onlyIf != null)
            {
                List<ReplacementContext> contexts = new List<ReplacementContext>();
                foreach (JsonNode? entry in onlyIf)
                {
                    if (entry is not JsonObject)
                    {
                        continue;
                    }

                    string? before = entry.ToString("before");
                    string? after = entry.ToString("after");
                    contexts.Add(new ReplacementContext(before, after));
                }

                return contexts;
            }
            else
            {
                return [];
            }
        }
    }
}
