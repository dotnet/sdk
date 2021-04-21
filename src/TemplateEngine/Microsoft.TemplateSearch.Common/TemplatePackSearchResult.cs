// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplatePackSearchResult
    {
        private readonly List<ITemplateMatchInfo> _templateMatches;

        public TemplatePackSearchResult(PackInfo packInfo)
        {
            PackInfo = packInfo;
            _templateMatches = new List<ITemplateMatchInfo>();
        }

        public PackInfo PackInfo { get; }

        public IReadOnlyList<ITemplateMatchInfo> TemplateMatches => _templateMatches;

        public void AddMatch(ITemplateMatchInfo match)
        {
            _templateMatches.Add(match);
        }
    }
}
