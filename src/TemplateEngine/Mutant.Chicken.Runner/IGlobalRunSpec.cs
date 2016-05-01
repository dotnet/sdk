using System.Collections.Generic;

namespace Mutant.Chicken.Runner
{
    public interface IGlobalRunSpec
    {
        IReadOnlyList<IPathMatcher> Exclude { get; }

        IReadOnlyList<IPathMatcher> Include { get; }

        IReadOnlyList<IPathMatcher> CopyOnly { get; }

        IReadOnlyList<IOperationProvider> Operations { get; }

        VariableCollection RootVariableCollection { get; }

        IReadOnlyDictionary<IPathMatcher, IRunSpec> Special { get; }

        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);
    }
}