// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class CodeAnalysisResult
    {
        private readonly Dictionary<Project, List<Diagnostic>> _dictionary = new();
        private readonly ImmutableHashSet<string> _diagnostics;
        private readonly ImmutableHashSet<string> _excludeDiagnostics;

        public CodeAnalysisResult(ImmutableHashSet<string> diagnostics, ImmutableHashSet<string> excludeDiagnostics)
        {
            _diagnostics = diagnostics;
            _excludeDiagnostics = excludeDiagnostics;
        }

        internal void AddDiagnostic(Project project, Diagnostic diagnostic)
        {
            // Ignore excluded diagnostics
            if (!_excludeDiagnostics.IsEmpty && _excludeDiagnostics.Contains(diagnostic.Id))
            {
                return;
            }

            if (!_diagnostics.IsEmpty && !_diagnostics.Contains(diagnostic.Id))
            {
                return;
            }

            if (!_dictionary.ContainsKey(project))
            {
                _dictionary.Add(project, new List<Diagnostic>() { diagnostic });
            }
            else
            {
                _dictionary[project].Add(diagnostic);
            }
        }

        public IReadOnlyDictionary<Project, List<Diagnostic>> Diagnostics
            => _dictionary;
    }
}
