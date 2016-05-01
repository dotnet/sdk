using System.Collections.Generic;
using Mutant.Chicken.Core;

namespace Mutant.Chicken.Runner
{
    public interface IRunSpec
    {
        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);

        IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations);

        VariableCollection ProduceCollection(VariableCollection parent);
    }
}