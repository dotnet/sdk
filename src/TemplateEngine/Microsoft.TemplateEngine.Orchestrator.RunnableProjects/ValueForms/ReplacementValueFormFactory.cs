// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class ReplacementValueFormSettings
    {
        public ReplacementValueFormSettings(Regex match, string? replacement)
        {
            Match = match;
            Replacement = replacement;
        }

        internal Regex Match { get; }

        internal string? Replacement { get; }
    }

    internal class ReplacementValueFormFactory : ConfigurableValueFormFactory<ReplacementValueFormSettings>
    {
        internal const string FormIdentifier = "replace";

        internal ReplacementValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value, ReplacementValueFormSettings? configuration)
        {
            if (configuration == null)
            {
                return value;
            }
            return configuration.Match.Replace(value, configuration.Replacement);
        }

        protected override ReplacementValueFormSettings ReadConfiguration(JObject jObject)
        {
            return new ReplacementValueFormSettings(new Regex(jObject.ToString("pattern")), jObject.ToString("replacement"));
        }
    }
}
