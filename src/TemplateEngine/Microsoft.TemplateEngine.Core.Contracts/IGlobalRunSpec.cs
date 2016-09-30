using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IGlobalRunSpec
    {
        IReadOnlyList<IPathMatcher> Exclude { get; }

        IReadOnlyList<IPathMatcher> Include { get; }

        IReadOnlyList<IPathMatcher> CopyOnly { get; }

        IReadOnlyList<IOperationProvider> Operations { get; }

        IVariableCollection RootVariableCollection { get; }

        IReadOnlyList<KeyValuePair<IPathMatcher, IRunSpec>> Special { get; }

        string PlaceholderFilename { get; }

        bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath);
    }
}