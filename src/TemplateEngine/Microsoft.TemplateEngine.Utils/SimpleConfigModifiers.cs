using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class SimpleConfigModifiers : ISimpleConfigModifiers
    {
        public string BaselineName { get; set; }
    }
}
