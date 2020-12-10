// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        // Deprecating. ITemplateMatchInfo will eventually fully replace IFilteredTemplateInfo
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

        public static IReadOnlyCollection<ITemplateMatchInfo> GetTemplateMatchInfo(IReadOnlyList<ITemplateInfo> templateList, Func<ITemplateMatchInfo, bool> matchFilter, params Func<ITemplateInfo, MatchInfo?>[] filters)
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

        public static Func<ITemplateMatchInfo, bool> ExactMatchFilter = x => x.IsMatch;

        public static Func<ITemplateMatchInfo, bool> PartialMatchFilter = x => x.IsPartialMatch;
    }
}
