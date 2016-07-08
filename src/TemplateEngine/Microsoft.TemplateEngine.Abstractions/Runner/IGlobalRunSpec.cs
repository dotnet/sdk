using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Abstractions.Runner
{
    public interface IGlobalRunSpec
    {
        IReadOnlyList<IPathMatcher> Exclude { get; }

        IReadOnlyList<IPathMatcher> Include { get; }

        IReadOnlyList<IPathMatcher> CopyOnly { get; }

        IReadOnlyList<IOperationProvider> Operations { get; }

        IVariableCollection RootVariableCollection { get; }

        IReadOnlyDictionary<IPathMatcher, IRunSpec> Special { get; }

        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);
    }
}