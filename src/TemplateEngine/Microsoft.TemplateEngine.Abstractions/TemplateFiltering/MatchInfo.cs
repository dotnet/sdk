// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Abstractions.TemplateFiltering
{
    /// <summary>
    /// Represents match information for the filter applied to template.
    /// </summary>
    public class MatchInfo
    {
        /// <summary>
        /// Creates <see cref="MatchInfo"/> instance.
        /// </summary>
        /// <param name="name">the name for the match. See default names in <see cref="BuiltIn"/>.</param>
        /// <param name="value">the value matched for.</param>
        /// <param name="kind">the match kind between <see cref="ITemplateInfo"/> value and <paramref name="value"/>.</param>
        public MatchInfo(string name, string? value, MatchKind kind)
        {
            _ = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException($"{nameof(name)} should not be null or empty") : false;

            Name = name;
            Value = value;
            Kind = kind;
        }

        /// <summary>
        /// Defines the match status.
        /// </summary>
        public MatchKind Kind { get; }

        /// <summary>
        /// Gets the name of the match.
        /// For default filter names, see <see cref="BuiltIn"/>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value provided to match for.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Frequently used filter names. They are also used by <see cref="WellKnownSearchFilters"/>.
        /// </summary>
        public static class BuiltIn
        {
            /// <summary>
            /// Template name <see cref="ITemplateInfo.Name"/>.
            /// </summary>
            public const string Name = "Name";

            /// <summary>
            /// Template short names <see cref="ITemplateInfo.ShortNameList"/>.
            /// </summary>
            public const string ShortName = "ShortName";

            /// <summary>
            /// Template classifications <see cref="ITemplateInfo.Classifications"/>.
            /// </summary>
            public const string Classification = "Classification";

            /// <summary>
            /// Template language (<see cref="ITemplateInfo.Tags"/> named "language").
            /// </summary>
            public const string Language = "Language";

            /// <summary>
            /// Template type (<see cref="ITemplateInfo.Tags"/> named "type").
            /// </summary>
            public const string Type = "Type";

            /// <summary>
            /// Template baseline names <see cref="ITemplateInfo.BaselineInfo"/>.
            /// </summary>
            public const string Baseline = "Baseline";

            /// <summary>
            /// Template author <see cref="ITemplateInfo.Author"/>.
            /// </summary>
            public const string Author = "Author";
        }
    }
}
