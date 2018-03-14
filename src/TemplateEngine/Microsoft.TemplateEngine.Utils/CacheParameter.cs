using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class CacheParameter : ICacheParameter, IAllowDefaultIfOptionWithoutValue
    {
        public string DataType { get; set; }

        public string DefaultValue { get; set; }

        public string Description { get; set; }

        public string DefaultIfOptionWithoutValue { get; set; }

        public bool ShouldSerializeDefaultIfOptionWithoutValue()
        {
            return !string.IsNullOrEmpty(DefaultIfOptionWithoutValue);
        }
    }
}
