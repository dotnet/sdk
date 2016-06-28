using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Runner;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunSpec : IRunSpec
    {
        private readonly IReadOnlyList<IOperationProvider> _overrides;
        private readonly VariableCollection _vars;

        public RunSpec(IReadOnlyList<IOperationProvider> operationOverrides, VariableCollection vars)
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

        public VariableCollection ProduceCollection(VariableCollection parent)
        {
            return _vars;
        }
    }
}