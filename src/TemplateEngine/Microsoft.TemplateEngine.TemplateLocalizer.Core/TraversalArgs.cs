// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.TemplateLocalizer.Core.TraversalRules;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    /// <summary>
    /// Helper type to simplify the signature of method
    /// <see cref="TemplateStringExtractor.TraverseJsonElements(System.Text.Json.JsonElement, string, string, TraversalArgs)"/>.
    /// </summary>
    internal struct TraversalArgs
    {
        public TraversalArgs(
            string identifierPrefix,
            string keyPrefix,
            IEnumerable<TraversalRule> rules,
            List<TemplateString> extractedStrings,
            HashSet<string> extractedStringIds)
        {
            IdentifierPrefix = identifierPrefix;
            KeyPrefix = keyPrefix;
            Rules = rules;
            ExtractedStrings = extractedStrings;
            ExtractedStringIds = extractedStringIds;
        }

        /// <summary>
        /// Gets or sets the prefix to be put in front of the json element identifier.
        /// </summary>
        public string IdentifierPrefix { get; set; }

        /// <summary>
        /// Gets or sets the prefix to be put in front of the localizable string key.
        /// </summary>
        public string KeyPrefix { get; set; }

        /// <summary>
        /// Gets or sets the rules to be used while traversing the json data.
        /// </summary>
        public IEnumerable<TraversalRule> Rules { get; set; }

        /// <summary>
        /// Gets the list of strings that were extracted up to this point in the execution.
        /// </summary>
        public List<TemplateString> ExtractedStrings { get; }

        /// <summary>
        /// Gets a set of identifiers that were extracted up to this point in the execution.
        /// This property is used to ensure the uniqueness of identifiers.
        /// </summary>
        public HashSet<string> ExtractedStringIds { get; }
    }
}
