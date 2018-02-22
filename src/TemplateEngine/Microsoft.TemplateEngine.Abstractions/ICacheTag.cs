using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ICacheTag
    {
        string Description { get; }

        IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }

        string DefaultValue { get; }
    }
}
