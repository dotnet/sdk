// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class KebabCaseValueFormModel : IValueForm
    {
        internal KebabCaseValueFormModel()
        {
        }

        internal KebabCaseValueFormModel(string name)
        {
            Name = name;
        }

        public string Identifier => "kebabCase";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new KebabCaseValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            if (value is null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            Regex pattern = new Regex(@"(?:\p{Lu}\p{M}*)?(?:\p{Ll}\p{M}*)+|(?:\p{Lu}\p{M}*)+(?!\p{Ll})|\p{N}+|[^\p{C}\p{P}\p{Z}]+|[\u2700-\u27BF]");
            return string.Join("-", pattern.Matches(value).Cast<Match>().Select(m => m.Value)).ToLowerInvariant();
        }
    }
}
