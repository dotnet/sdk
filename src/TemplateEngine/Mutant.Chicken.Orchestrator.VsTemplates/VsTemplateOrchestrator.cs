using System.IO;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    internal class VsTemplateOrchestrator : Runner.Orchestrator
    {
        protected override IGlobalRunSpec RunSpecLoader(Stream runSpec)
        {
            return null;
        }
    }
}