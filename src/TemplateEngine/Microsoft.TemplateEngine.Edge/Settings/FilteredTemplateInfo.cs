using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class FilteredTemplateInfo : IFilteredTemplateInfo
    {
        public FilteredTemplateInfo(ITemplateInfo info, FilterResult matchDisposition)
        {
            Info = info;
            MatchDisposition = matchDisposition;
        }

        public ITemplateInfo Info { get; }

        public FilterResult MatchDisposition { get; set; }

        public static bool IsAnyMatchType(FilterResult matchDisposition)
        {
            return matchDisposition == FilterResult.Match
                || matchDisposition == FilterResult.SubstringMatch
                || matchDisposition == FilterResult.AliasMatch
                || matchDisposition == FilterResult.ClassificationMatch;
        }
    }
}
