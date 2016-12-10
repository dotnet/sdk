using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    public class HostTemplateModel : IHostTemplateModel
    {
        public IReadOnlyDictionary<string, string> ParameterMap { get; set; }
    }
}
