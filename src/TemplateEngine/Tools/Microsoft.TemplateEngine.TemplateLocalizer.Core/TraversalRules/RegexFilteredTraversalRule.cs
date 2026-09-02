// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.TraversalRules
{
    /// <summary>
    /// Filters identifiers based on whether they match with a given regex.
    /// </summary>
    internal sealed class RegexFilteredTraversalRule : TraversalRule
    {
        private readonly Regex _regex;

        /// <summary>
        /// Creates an instance of <see cref="RegexFilteredTraversalRule"/>.
        /// </summary>
        /// <param name="regexPattern">Regex pattern string that will be used to match json element names.</param>
        /// <param name="keyCreator"><see cref="IJsonKeyCreator"/> to be used when creating a key for the match elements.</param>
        public RegexFilteredTraversalRule(string regexPattern, IJsonKeyCreator? keyCreator = default)
            : this(new Regex(regexPattern), keyCreator)
        { }

        /// <summary>
        /// Creates an instance of <see cref="RegexFilteredTraversalRule"/>.
        /// </summary>
        /// <param name="regex">Regex pattern that will be used to match json element names.</param>
        /// <param name="keyCreator"><see cref="IJsonKeyCreator"/> to be used when creating a key for the match elements.</param>
        public RegexFilteredTraversalRule(Regex regex, IJsonKeyCreator? keyCreator = default)
            : base(keyCreator)
        {
            _regex = regex;
        }

        /// <inheritdoc/>
        public override bool AllowsTraversalOfIdentifier(string identifier)
        {
            Match match = _regex.Match(identifier);
            return match.Success && match.Length == identifier.Length;
        }
    }
}
