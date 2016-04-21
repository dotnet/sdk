using System.Collections.Generic;

namespace Mutant.Chicken.Runner
{
    public interface IRunSpec
    {
        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);

        IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations);

        VariableCollection ProduceCollection(VariableCollection parent);
    }
}