// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if NET45
using System.Linq;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge
{
    public static class TemplateListFilter
    {
        /// <summary>
        /// Returns the templates from the templateList which pass the matchFilter based on the results of filters.
        /// </summary>
        /// <param name="templateList">The list of templates to check</param>
        /// <param name="matchFilter">Templates this func returns true on are included in the return list.</param>
        /// <param name="filters">These check conditions of the template, adding appropriate MatchInfo to the template's results.</param>
        /// <returns></returns>
        public static IReadOnlyCollection<IFilteredTemplateInfo> FilterTemplates(IReadOnlyList<ITemplateInfo> templateList, Func<IFilteredTemplateInfo, bool> matchFilter, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            HashSet<IFilteredTemplateInfo> matchingTemplates = new HashSet<IFilteredTemplateInfo>(FilteredTemplateEqualityComparer.Default);

            foreach (ITemplateInfo template in templateList)
            {
                FilteredTemplateInfo info = new FilteredTemplateInfo(template);

                foreach (Func<ITemplateInfo, MatchInfo?> filter in filters)
                {
                    MatchInfo? result = filter(template);

                    if (result.HasValue)
                    {
                        info.AddDisposition(result.Value);
                    }
                }

                if (matchFilter(info))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NET45
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }

        public static IReadOnlyCollection<ITemplateMatchInfo> GetTemplateMatchInfo(IReadOnlyList<ITemplateInfo> templateList, bool exactMatchesOnly, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            HashSet<ITemplateMatchInfo> matchingTemplates = new HashSet<ITemplateMatchInfo>(TemplateMatchInfoEqualityComparer.Default);

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

                ITemplateMatchInfo info = new TemplateMatchInfo(template, matchInformation);
                if (info.IsMatch || (!exactMatchesOnly && info.IsPartialMatch))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NET45
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }

        public static Func<IFilteredTemplateInfo, bool> ExactMatchFilter = x => x.IsMatch;

        public static Func<IFilteredTemplateInfo, bool> PartialMatchFilter = x => x.IsPartialMatch;
    }
}
