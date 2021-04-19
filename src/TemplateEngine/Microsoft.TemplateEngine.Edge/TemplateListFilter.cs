// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
#if NETFULL
using System.Linq;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge
{
    public static class TemplateListFilter
    {
        [Obsolete("Use " + nameof(Utils.WellKnownSearchFilters.MatchesAllCriteria) + " instead")]
        /// <summary>
        /// Exact match criteria - the templates should match all filters.
        /// </summary>
        /// <seealso cref="GetTemplateMatchInfo"/>
        public static Func<ITemplateMatchInfo, bool> ExactMatchFilter => x => x.IsMatch;

        [Obsolete("Use " + nameof(Utils.WellKnownSearchFilters.MatchesAtLeastOneCriteria) + " instead")]
        /// <summary>
        /// Partial match criteria - the templates should match at least one of the filters.
        /// </summary>
        /// <seealso cref="GetTemplateMatchInfo"/>
        public static Func<ITemplateMatchInfo, bool> PartialMatchFilter => x => x.IsPartialMatch;

        [Obsolete("Use " + nameof(ISettingsLoader.GetTemplatesAsync) + " instead")]
        public static IReadOnlyCollection<IFilteredTemplateInfo> FilterTemplates(IReadOnlyList<ITemplateInfo> templateList, bool exactMatchesOnly, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            HashSet<IFilteredTemplateInfo> matchingTemplates = new HashSet<IFilteredTemplateInfo>(FilteredTemplateEqualityComparer.Default);

            foreach (ITemplateInfo template in templateList)
            {
                List<MatchInfo> matchInformation = new List<MatchInfo>();

                foreach (Func<ITemplateInfo, MatchInfo?> filter in filters)
                {
                    MatchInfo? result = filter(template);

                    if (result.HasValue)
                    {
                        matchInformation.Add(result.Value);
                    }
                }

                FilteredTemplateInfo info = new FilteredTemplateInfo(template, matchInformation);

                if (info.IsMatch || (!exactMatchesOnly && info.IsPartialMatch))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NETFULL
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }

        /// <summary>
        /// Gets matching information for templates for provided filters.
        /// </summary>
        /// <param name="templateList">The templates to be filtered.</param>
        /// <param name="matchFilter">The criteria of template to be filtered.</param>
        /// <param name="filters">The list of filters to be applied.</param>
        /// <returns>The filtered list of templates with matches information.</returns>
        /// <example>
        /// <c>GetTemplateMatchInfo(templates, TemplateListFilter.ExactMatchFilter, WellKnownSearchFilters.NameFilter("myname")</c> - returns the templates which name or short name contains "myname". <br/>
        /// <c>GetTemplateMatchInfo(templates, TemplateListFilter.PartialMatchFilter, WellKnownSearchFilters.NameFilter("myname"), WellKnownSearchFilters.NameFilter("othername")</c> - returns the templates which name or short name contains "myname" or "othername".<br/>
        /// </example>
        [Obsolete("Use " + nameof(ISettingsLoader.GetTemplatesAsync) + " instead")]
        public static IReadOnlyCollection<ITemplateMatchInfo> GetTemplateMatchInfo(IReadOnlyList<ITemplateInfo> templateList, Func<ITemplateMatchInfo, bool> matchFilter, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            HashSet<ITemplateMatchInfo> matchingTemplates = new HashSet<ITemplateMatchInfo>(Template.TemplateMatchInfoEqualityComparer.Default);

            foreach (ITemplateInfo template in templateList)
            {
                List<MatchInfo> matchInformation = new List<MatchInfo>();

                foreach (Func<ITemplateInfo, MatchInfo?> filter in filters)
                {
                    MatchInfo? result = filter(template);

                    if (result.HasValue)
                    {
                        matchInformation.Add(result.Value);
                    }
                }

                ITemplateMatchInfo info = new TemplateMatchInfoEx(template, matchInformation);
                if (matchFilter(info))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NETFULL
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }

        //TODO: we cannot remove the method below as CLI needs it due to it changes ITemplateInfo before filtering. Once CLI is refactored, this method can be removed.
        [Obsolete("Use " + nameof(ISettingsLoader.GetTemplatesAsync) + " instead")]
        public static IReadOnlyCollection<Abstractions.TemplateFiltering.ITemplateMatchInfo> GetTemplateMatchInfo(IReadOnlyList<ITemplateInfo> templateList, Func<Abstractions.TemplateFiltering.ITemplateMatchInfo, bool> matchFilter, params Func<ITemplateInfo, Abstractions.TemplateFiltering.MatchInfo?>[] filters)
        {
            HashSet<Abstractions.TemplateFiltering.ITemplateMatchInfo> matchingTemplates = new HashSet<Abstractions.TemplateFiltering.ITemplateMatchInfo>(TemplateMatchInfoEqualityComparer.Default);

            foreach (ITemplateInfo template in templateList)
            {
                List<Abstractions.TemplateFiltering.MatchInfo> matchInformation = new List<Abstractions.TemplateFiltering.MatchInfo>();

                foreach (Func<ITemplateInfo, Abstractions.TemplateFiltering.MatchInfo?> filter in filters)
                {
                    Abstractions.TemplateFiltering.MatchInfo? result = filter(template);
                    if (result != null)
                    {
                        matchInformation.Add(result);
                    }
                }

                Abstractions.TemplateFiltering.ITemplateMatchInfo info = new TemplateMatchInfo(template, matchInformation);
                if (matchFilter(info))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NETFULL
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }

        private class TemplateMatchInfoEqualityComparer : IEqualityComparer<Abstractions.TemplateFiltering.ITemplateMatchInfo>
        {
            internal static IEqualityComparer<Abstractions.TemplateFiltering.ITemplateMatchInfo> Default { get; } = new TemplateMatchInfoEqualityComparer();

            public bool Equals(Abstractions.TemplateFiltering.ITemplateMatchInfo x, Abstractions.TemplateFiltering.ITemplateMatchInfo y)
            {
                return ReferenceEquals(x?.Info, y?.Info) || (x != null && y != null && x?.Info != null && y?.Info != null && string.Equals(x?.Info?.Identity, y?.Info?.Identity, StringComparison.Ordinal));
            }

            public int GetHashCode(Abstractions.TemplateFiltering.ITemplateMatchInfo obj)
            {
                return obj?.Info?.Identity?.GetHashCode() ?? 0;
            }
        }
    }
}
