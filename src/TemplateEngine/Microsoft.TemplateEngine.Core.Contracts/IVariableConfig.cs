using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IVariableConfig
    {
        IReadOnlyDictionary<string, string> Sources { get; }

        IReadOnlyList<string> Order { get; }

        string FallbackFormat { get; }

        bool Expand { get; }
    }
}
