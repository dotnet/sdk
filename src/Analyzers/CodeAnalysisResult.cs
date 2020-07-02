// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class CodeAnalysisResult
    {
        private readonly Dictionary<Project, List<Diagnostic>> _dictionary
            = new Dictionary<Project, List<Diagnostic>>();

        internal void AddDiagnostic(Project project, Diagnostic diagnostic)
        {
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
