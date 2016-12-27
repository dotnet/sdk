using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class FilteredTemplateInfo : IFilteredTemplateInfo
    {
        public FilteredTemplateInfo(ITemplateInfo info, IReadOnlyList<MatchInfo> matchDisposition)
        {
            Info = info;
            MatchDisposition = matchDisposition;
        }

        public ITemplateInfo Info { get; }

        public IReadOnlyList<MatchInfo> MatchDisposition { get; set; }

        public bool IsMatch => MatchDisposition.Count > 0 && !MatchDisposition.Any(x => x.Kind == MatchKind.Mismatch);

        public bool IsPartialMatch => MatchDisposition.Any(x => x.Kind != MatchKind.Mismatch);
    }
}
