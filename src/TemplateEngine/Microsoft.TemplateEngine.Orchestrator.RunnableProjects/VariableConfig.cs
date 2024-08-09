// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class VariableConfig : IVariableConfig
    {
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

        public IReadOnlyDictionary<string, string> Sources { get; }

        public IReadOnlyList<string> Order { get; }

        public string? FallbackFormat { get; }

        public bool Expand { get; }

        internal static IVariableConfig Default { get; } =
                new VariableConfig(
                new Dictionary<string, string>
                {
                    { "user", "{0}" }
                },
                new[] { "user" },
                null);
    }
}
