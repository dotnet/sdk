using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IHostTemplateModel
    {
        // standard template parameter -> host specific template parameter
        IReadOnlyDictionary<string, string> ParameterMap { get; }
    }
}
