using System.Collections.Generic;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    public class ScraperConfig
    {
        public string BasePath { get; set; }

        public int PageSize { get; set; }

        public bool SaveCandidatePacks { get; set; }

        public bool RunOnlyOnePage { get; set; }

        public bool IncludePreviewPacks { get; set; }

        public string PreviousRunBasePath { get; set; }

        public bool DontFilterOnTemplateJson { get; set; }

        public List<string> Providers { get; } = new List<string>();
    }
}
