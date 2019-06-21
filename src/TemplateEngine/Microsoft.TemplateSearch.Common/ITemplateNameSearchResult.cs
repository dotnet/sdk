using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    public interface ITemplateNameSearchResult
    {
        ITemplateInfo Template { get; }

        PackInfo PackInfo { get; }
    }
}
