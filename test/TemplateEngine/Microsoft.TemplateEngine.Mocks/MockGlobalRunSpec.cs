using System.Collections.Generic;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using System;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockGlobalRunSpec : IGlobalRunSpec
    {
        public MockGlobalRunSpec()
        {
            Exclude = new List<IPathMatcher>();
            Include = new List<IPathMatcher>();
            CopyOnly = new List<IPathMatcher>();
            Operations = new List<IOperationProvider>();
            RootVariableCollection = new VariableCollection();
            Special = new List<KeyValuePair<IPathMatcher, IRunSpec>>();
            LocalizationOperations = new Dictionary<string, IReadOnlyList<IOperationProvider>>();
            TargetRelativePaths = new Dictionary<string, string>();
        }

        public IReadOnlyList<IPathMatcher> Exclude { get; set; }

        public IReadOnlyList<IPathMatcher> Include { get; set; }

        public IReadOnlyList<IPathMatcher> CopyOnly { get; set; }

        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        public IVariableCollection RootVariableCollection { get; set; }

        public IReadOnlyList<KeyValuePair<IPathMatcher, IRunSpec>> Special { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyList<IOperationProvider>> LocalizationOperations { get; set; }

        public string PlaceholderFilename { get; set; }

        public Dictionary<string, string> TargetRelativePaths { get; set; }

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return TargetRelativePaths.TryGetValue(sourceRelPath, out targetRelPath);
        }

        public void SetupFileSource(FileSourceMatchInfo source)
        {
            FileSourceHierarchicalPathMatcher matcher = new FileSourceHierarchicalPathMatcher(source);
            Include = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Include, matcher) };
            Exclude = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.Exclude, matcher) };
            CopyOnly = new List<IPathMatcher>() { new FileSourceStateMatcher(FileDispositionStates.CopyOnly, matcher) };
        }
    }
}
