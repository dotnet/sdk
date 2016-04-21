using System.Collections.Generic;

namespace Mutant.Chicken.Runner
{
    public interface IRunSpec
    {
        string GetTargetRelativePath(string sourcePath, string sourceFile);

        IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations);

        VariableCollection ProduceCollection(VariableCollection parent);
    }
}