using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IBaselineInfo
    {
        string Description { get; }

        IReadOnlyDictionary<string, string> DefaultOverrides { get; }
    }
}
