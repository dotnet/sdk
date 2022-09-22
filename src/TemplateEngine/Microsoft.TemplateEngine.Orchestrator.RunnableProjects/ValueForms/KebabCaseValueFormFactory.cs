// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class KebabCaseValueFormFactory : ActionableValueFormFactory
    {
        internal const string FormIdentifier = "kebabCase";

        internal KebabCaseValueFormFactory() : base(FormIdentifier) { }

        protected override string Process(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            Regex pattern = new(@"(?:\p{Lu}\p{M}*)?(?:\p{Ll}\p{M}*)+|(?:\p{Lu}\p{M}*)+(?!\p{Ll})|\p{N}+|[^\p{C}\p{P}\p{Z}]+|[\u2700-\u27BF]");
            return string.Join("-", pattern.Matches(value).Cast<Match>().Select(m => m.Value)).ToLowerInvariant();
        }
    }
}
