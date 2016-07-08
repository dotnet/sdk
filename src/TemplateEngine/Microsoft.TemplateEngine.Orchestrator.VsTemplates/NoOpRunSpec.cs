using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Runner;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class NoOpRunSpec : IRunSpec
    {
        private static readonly IReadOnlyList<IOperationProvider> NoOperations = new IOperationProvider[0];

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations)
        {
            return NoOperations;
        }

        public IVariableCollection ProduceCollection(IVariableCollection parent)
        {
            return new VariableCollection();
        }
    }
}