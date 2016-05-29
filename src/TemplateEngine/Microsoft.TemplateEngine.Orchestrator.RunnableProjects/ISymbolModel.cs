using System.IO;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface ISymbolModel
    {
        string Type { get; }

        string Binding { get; set; }

        string Replaces { get; set; }
    }
}