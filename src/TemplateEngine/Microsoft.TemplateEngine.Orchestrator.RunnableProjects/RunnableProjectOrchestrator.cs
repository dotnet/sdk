using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class RunnableProjectOrchestrator : IOrchestrator, IOrchestrator2
    {
        private readonly IOrchestrator2 _basicOrchestrator;

        public RunnableProjectOrchestrator(IOrchestrator2 basicOrchestrator)
        {
            _basicOrchestrator = basicOrchestrator;
        }

        public IReadOnlyList<IFileChange2> GetFileChanges(string runSpecPath, IDirectory sourceDir, string targetDir)
        {
            return _basicOrchestrator.GetFileChanges(runSpecPath, sourceDir, targetDir);
        }

        public IReadOnlyList<IFileChange2> GetFileChanges(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir)
        {
            return _basicOrchestrator.GetFileChanges(spec, sourceDir, targetDir);
        }

        IReadOnlyList<IFileChange> IOrchestrator.GetFileChanges(string runSpecPath, IDirectory sourceDir, string targetDir)
        {
            return GetFileChanges(runSpecPath, sourceDir, targetDir);
        }

        IReadOnlyList<IFileChange> IOrchestrator.GetFileChanges(IGlobalRunSpec spec, IDirectory sourceDir, string targetDir)
        {
            return GetFileChanges(spec, sourceDir, targetDir);
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
