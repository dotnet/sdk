using System.IO;
using Microsoft.TemplateEngine.Runner;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class VsTemplateOrchestrator : Runner.Orchestrator
    {
        protected override IGlobalRunSpec RunSpecLoader(Stream runSpec)
        {
            return null;
        }
    }
}