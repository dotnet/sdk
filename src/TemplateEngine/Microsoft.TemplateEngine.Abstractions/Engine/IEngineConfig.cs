using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IEngineConfig
    {
        IReadOnlyList<string> LineEndings { get; }

        string VariableFormatString { get; }

        IVariableCollection Variables { get; }

        IReadOnlyList<string> Whitespaces { get; }

        IDictionary<string, bool> Flags { get; }
    }
}