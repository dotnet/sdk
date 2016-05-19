using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Runner
{
    public interface IRunSpec
    {
        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);

        IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations);

        VariableCollection ProduceCollection(VariableCollection parent);
    }
}