using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IExtendedTemplateParameter : ITemplateParameter
    {
        string FileRename { get; }

        IReadOnlyDictionary<string, IReadOnlyList<string>> Forms { get; }
    }
}
