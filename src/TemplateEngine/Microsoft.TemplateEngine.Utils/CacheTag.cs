using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class CacheTag : ICacheTag
    {
        public string Description { get; set; }

        public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; set; }

        public string DefaultValue { get; set; }
    }
}
