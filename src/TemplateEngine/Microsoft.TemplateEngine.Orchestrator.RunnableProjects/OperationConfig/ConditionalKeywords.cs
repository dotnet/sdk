// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.OperationConfig
{
    internal class ConditionalKeywords
    {
        private const string DefaultPrefix = "#";
        private static readonly IReadOnlyList<string> s_defaultIfKeywords = new[] { "if" };
        private static readonly IReadOnlyList<string> s_defaultElseIfKeywords = new[] { "elseif", "elif" };
        private static readonly IReadOnlyList<string> s_defaultElseKeywords = new[] { "else" };
        private static readonly IReadOnlyList<string> s_defaultEndIfKeywords = new[] { "endif" };

        internal ConditionalKeywords()
        {
            KeywordPrefix = DefaultPrefix;
            IfKeywords = s_defaultIfKeywords;
            ElseIfKeywords = s_defaultElseIfKeywords;
            ElseKeywords = s_defaultElseKeywords;
            EndIfKeywords = s_defaultEndIfKeywords;
        }

        internal string KeywordPrefix { get; set; }

        internal IReadOnlyList<string> IfKeywords { get; set; }

        internal IReadOnlyList<string> ElseIfKeywords { get; set; }

        internal IReadOnlyList<string> ElseKeywords { get; set; }

        internal IReadOnlyList<string> EndIfKeywords { get; set; }

        // TODO: Allow the rawConfiguration elements to be either strings (as-is) or arrays of strings.
        // The code that consumes instances of this class is already setup to deal with multiple forms of each keyword type.
        internal static ConditionalKeywords FromJObject(JObject rawConfiguration)
        {
            ConditionalKeywords keywords = new ConditionalKeywords();
            string ifKeyword = rawConfiguration.ToString("ifKeyword");
            if (!string.IsNullOrWhiteSpace(ifKeyword))
            {
                keywords.IfKeywords = new[] { ifKeyword };
            }

            string elseIfKeyword = rawConfiguration.ToString("elseIfKeyword");
            if (!string.IsNullOrWhiteSpace(elseIfKeyword))
            {
                keywords.ElseIfKeywords = new[] { elseIfKeyword };
            }

            string elseKeyword = rawConfiguration.ToString("elseKeyword");
            if (!string.IsNullOrWhiteSpace(elseKeyword))
            {
                keywords.ElseKeywords = new[] { elseKeyword };
            }

            string endIfKeyword = rawConfiguration.ToString("endIfKeyword");
            if (!string.IsNullOrWhiteSpace(endIfKeyword))
            {
                keywords.EndIfKeywords = new[] { endIfKeyword };
            }

            string prefixString = rawConfiguration.ToString("keywordPrefix");
            if (prefixString != null)
            {
                // Empty string is a valid value for keywordPrefix, null is not.
                // If the "keywordPrefix" key isn't present in the config, the value will be null and not used.
                keywords.KeywordPrefix = prefixString;
            }

            return keywords;
        }
    }
}
