using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplatePackSearchResult
    {
        public TemplatePackSearchResult(PackInfo packInfo)
        {
            PackInfo = packInfo;
            _templateMatches = new List<ITemplateMatchInfo>();
        }

        public PackInfo PackInfo { get; }

        public void AddMatch(ITemplateMatchInfo match)
        {
            _templateMatches.Add(match);
        }

        private readonly List<ITemplateMatchInfo> _templateMatches;

        public IReadOnlyList<ITemplateMatchInfo> TemplateMatches => _templateMatches;
    }
}
