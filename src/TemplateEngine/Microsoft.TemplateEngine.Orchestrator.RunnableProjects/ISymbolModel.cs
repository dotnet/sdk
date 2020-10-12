using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface ISymbolModel
    {
        string Type { get; }

        string Binding { get; set; }

        string Replaces { get; set; }

        IReadOnlyList<IReplacementContext> ReplacementContexts { get; }

        string FileRename { get; set; }
    }
}
