// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

#nullable enable

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("Use " + nameof(Microsoft.TemplateEngine.Utils.WellKnownSearchFilters) + " instead")]
    public static class WellKnownSearchFilters
    {
        public static Func<ITemplateInfo, MatchInfo?> NameFilter(string name)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                int nameIndex = template.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                if (nameIndex == 0 && template.Name.Length == name.Length)
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact };
                }

                bool hasShortNamePartialMatch = false;

                foreach (string shortName in template.ShortNameList)
                {
                    int shortNameIndex = shortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                    if (shortNameIndex == 0 && shortName.Length == name.Length)
                    {
                        return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Exact };
                    }

                    hasShortNamePartialMatch |= shortNameIndex > -1;
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                if (hasShortNamePartialMatch)
                {
                    return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Partial };
                }

                return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch };
            };
        }

        // This being case-insensitive depends on the dictionaries on the cache tags being declared as case-insensitive
        public static Func<ITemplateInfo, MatchInfo?> ContextFilter(string inputContext)
        {
            string? context = inputContext?.ToLowerInvariant();

            return (template) =>
            {
                if (string.IsNullOrEmpty(context))
                {
                    return null;
                }
                if (template.GetTemplateType()?.Equals(context, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    return new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact };
                }
                else
                {
                    return new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch };
                }
            };
        }

        /// <summary>
        /// Creates predicate for matching the template and given tag value.
        /// If the template contains the tag <paramref name="tagFilter"/>, it is exact match, otherwise mismatch.
        /// If the template has no tags defined, it is a mismatch.
        /// If <paramref name="tagFilter"/> is <see cref="null"/> or empty the method returns <see cref="null"/>.
        /// </summary>
        /// <param name="tagFilter">tag to filter by.</param>
        /// <returns>A predicate that returns if the given template matches <paramref name="tagFilter"/>.</returns>
        public static Func<ITemplateInfo, MatchInfo?> TagFilter(string tagFilter)
        {
            return (template) =>
            {
                if (string.IsNullOrWhiteSpace(tagFilter))
                {
                    return null;
                }
                if (template.Classifications?.Contains(tagFilter, StringComparer.OrdinalIgnoreCase) ?? false)
                {
                    return new MatchInfo { Location = MatchLocation.Classification, Kind = MatchKind.Exact };
                }
                return new MatchInfo { Location = MatchLocation.Classification, Kind = MatchKind.Mismatch };
            };
        }

        // This being case-insensitive depends on the dictionaries on the cache tags being declared as case-insensitive
        // Note: This is specifically designed to provide match info against a user-input language.
        //      All dealings with the host-default language should be separate from this.
        public static Func<ITemplateInfo, MatchInfo?> LanguageFilter(string language)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(language))
                {
                    return null;
                }

                if (template.GetLanguage()?.Equals(language, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    return new MatchInfo { Location = MatchLocation.Language, Kind = MatchKind.Exact };
                }
                else
                {
                    return new MatchInfo { Location = MatchLocation.Language, Kind = MatchKind.Mismatch };
                }
            };
        }

        public static Func<ITemplateInfo, MatchInfo?> BaselineFilter(string baselineName)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(baselineName))
                {
                    return null;
                }

                if (template.BaselineInfo != null && template.BaselineInfo.ContainsKey(baselineName))
                {
                    return new MatchInfo { Location = MatchLocation.Baseline, Kind = MatchKind.Exact };
                }
                else
                {
                    return new MatchInfo { Location = MatchLocation.Baseline, Kind = MatchKind.Mismatch };
                }
            };
        }

        [Obsolete("Use TagsFilter instead")]
        public static Func<ITemplateInfo, MatchInfo?> ClassificationsFilter(string name)
        {
            return (template) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                string[] parts = name.Split('/');

                if (template.Classifications != null)
                {
                    bool allParts = true;
                    bool anyParts = false;

                    foreach (string part in parts)
                    {
                        if (!template.Classifications.Contains(part, StringComparer.OrdinalIgnoreCase))
                        {
                            allParts = false;
                        }
                        else
                        {
                            anyParts = true;
                        }
                    }

                    anyParts &= parts.Length == template.Classifications.Count;

                    if (allParts || anyParts)
                    {
                        return new MatchInfo { Location = MatchLocation.Classification, Kind = allParts ? MatchKind.Exact : MatchKind.Partial };
                    }
                }

                return null;
            };
        }

        /// <summary>
        /// Creates predicate for matching the template and given author value.
        /// </summary>
        /// <param name="author">author to use for match.</param>
        /// <returns>A predicate that returns if the given template matches defined author.</returns>
        public static Func<ITemplateInfo, MatchInfo?> AuthorFilter(string author)
        {
            return (template) =>
            {
                if (string.IsNullOrWhiteSpace(author))
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(template.Author))
                {
                    return new MatchInfo { Location = MatchLocation.Author, Kind = MatchKind.Mismatch };
                }

                int authorIndex = template.Author.IndexOf(author, StringComparison.OrdinalIgnoreCase);

                if (authorIndex == 0 && template.Author.Length == author.Length)
                {
                    return new MatchInfo { Location = MatchLocation.Author, Kind = MatchKind.Exact };
                }

                if (authorIndex > -1)
                {
                    return new MatchInfo { Location = MatchLocation.Author, Kind = MatchKind.Partial };
                }

                return new MatchInfo { Location = MatchLocation.Author, Kind = MatchKind.Mismatch };
            };
        }

    }
}
