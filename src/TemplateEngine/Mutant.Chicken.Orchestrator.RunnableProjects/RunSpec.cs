using System.Collections.Generic;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.RunnableProjects
{
    internal class DemoRunSpec : IRunSpec
    {
        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations)
        {
            return sourceOperations;
        }

        public VariableCollection ProduceCollection(VariableCollection parent)
        {
            return new VariableCollection(parent, new Dictionary<string, object>
            {
                {"CHEESE", true}
            });
        }
    }
}