using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Runner;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class VsTemplateOrchestrator : IOrchestrator
    {
        private readonly IOrchestrator _basicOrchestrator;

        public VsTemplateOrchestrator(IOrchestrator basicOrchestrator)
        {
            _basicOrchestrator = basicOrchestrator;
        }

        public void Run(string runSpecPath, IDirectory sourceDir, string targetDir)
        {
            _basicOrchestrator.Run(runSpecPath, sourceDir, targetDir);
        }

        public void Run(IGlobalRunSpec runSpec, IDirectory directoryInfo, string target)
        {
            _basicOrchestrator.Run(runSpec, directoryInfo, target);
        }
    }
}