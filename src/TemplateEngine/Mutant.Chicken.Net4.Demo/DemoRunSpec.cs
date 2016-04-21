using System;
using System.Collections.Generic;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Demo
{
    internal class DemoRunSpec : IRunSpec
    {
        public string GetTargetRelativePath(string sourcePath, string sourceFile)
        {
            throw new NotImplementedException();
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