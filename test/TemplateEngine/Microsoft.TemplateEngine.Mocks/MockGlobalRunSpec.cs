// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

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
            Special = new List<KeyValuePair<IPathMatcher, IRunSpec>>();
            LocalizationOperations = new Dictionary<string, IReadOnlyList<IOperationProvider>>();
            Rename = new Dictionary<string, string>();
            IgnoreFileNames = new[] { "-.-", "_._" };
        }

        public IReadOnlyList<IPathMatcher> Exclude { get; set; }

        public IReadOnlyList<IPathMatcher> Include { get; set; }

        public IReadOnlyList<IPathMatcher> CopyOnly { get; set; }

        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        public IVariableCollection RootVariableCollection { get; set; }

        public IReadOnlyList<KeyValuePair<IPathMatcher, IRunSpec>> Special { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyList<IOperationProvider>> LocalizationOperations { get; set; }

        public IReadOnlyList<string> IgnoreFileNames { get; set; }

        public IReadOnlyDictionary<string, string> Rename { get; set; }

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return Rename.TryGetValue(sourceRelPath, out targetRelPath);
        }
    }
}
