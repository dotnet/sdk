using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Runner;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunSpec : IRunSpec
    {
        private readonly IReadOnlyList<IOperationProvider> _overrides;
        private readonly IVariableCollection _vars;

        public RunSpec(IReadOnlyList<IOperationProvider> operationOverrides, IVariableCollection vars)
        {
            _overrides = operationOverrides;
            _vars = vars ?? new VariableCollection();
        }

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations)
        {
            return _overrides ?? sourceOperations;
        }

        public IVariableCollection ProduceCollection(IVariableCollection parent)
        {
            return _vars;
        }
    }
}