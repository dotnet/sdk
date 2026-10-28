// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.TraversalRules
{
    /// <summary>
    /// Filters identifiers based on whether they exactly match a given string.
    /// </summary>
    internal sealed class StringFilteredTraversalRule : TraversalRule
    {
        private readonly string _identifierToMatch;

        /// <summary>
        /// Creates an instance of <see cref="StringFilteredTraversalRule"/>.
        /// </summary>
        /// <param name="identifierToMatch">The exact json element name that this rule will accept and not filter out.</param>
        /// <param name="keyCreator"><see cref="IJsonKeyCreator"/> to be used when creating a key for the match elements.</param>
        public StringFilteredTraversalRule(string identifierToMatch, IJsonKeyCreator? keyCreator = default)
            : base(keyCreator)
        {
            _identifierToMatch = identifierToMatch;
        }

        /// <inheritdoc/>
        public override bool AllowsTraversalOfIdentifier(string identifier)
        {
            return identifier == _identifierToMatch;
        }
    }
}
