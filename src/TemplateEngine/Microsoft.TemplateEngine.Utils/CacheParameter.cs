using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class CacheParameter : ICacheParameter
    {
        public string DataType { get; set; }

        public string DefaultValue { get; set; }

        public string Description { get; set; }
    }
}
