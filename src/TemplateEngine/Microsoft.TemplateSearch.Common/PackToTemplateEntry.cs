using System.Collections.Generic;

namespace Microsoft.TemplateSearch.Common
{
    public class PackToTemplateEntry
    {
        public PackToTemplateEntry(string version, List<TemplateIdentificationEntry> templateinfo)
        {
            Version = version;
            TemplateIdentificationEntry = templateinfo;
        }

        public string Version { get; }

        public IReadOnlyList<TemplateIdentificationEntry> TemplateIdentificationEntry { get; }
    }
}
