// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.TemplateLocalizer.Core.KeyCreators;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.TraversalRules
{
    /// <summary>
    /// Represents a rule that defines whether a given identifier should be further traversed.
    /// Also includes the preferred <see cref="IJsonKeyCreator"/> to be used for creating keys for elements.
    /// </summary>
    internal abstract class TraversalRule
    {
        private readonly List<TraversalRule> _childRules = new ();

        protected TraversalRule(IJsonKeyCreator? keyCreator = default)
        {
            KeyCreator = keyCreator;
        }

        /// <summary>
        /// Key extractor to be used when calculating the key for the elements complying with this rule.
        /// </summary>
        public IJsonKeyCreator? KeyCreator { get; }

        /// <summary>
        /// Gets the rules that the children of this json element should comply with.
        /// </summary>
        public IReadOnlyList<TraversalRule> ChildRules => _childRules;

        /// <summary>
        /// Returns if the given identifier complies with this rule.
        /// </summary>
        /// <param name="identifier">Identifier of the element.</param>
        /// <returns>True if element complies with the rule. False if the element is filtered out.</returns>
        public abstract bool AllowsTraversalOfIdentifier(string identifier);

        /// <summary>
        /// Adds the given rule to the child ruls list.
        /// </summary>
        /// <returns>Returns <see langword="this"/>.</returns>
        public TraversalRule WithChild(TraversalRule childRule)
        {
            _childRules.Add(childRule);
            return this;
        }

        /// <summary>
        /// Adds the given set of rules to the child rules list.
        /// </summary>
        /// <returns>Returns <see langword="this"/>.</returns>
        public TraversalRule WithChildren(params TraversalRule[] childRules)
        {
            _childRules.AddRange(childRules);
            return this;
        }
    }
}
