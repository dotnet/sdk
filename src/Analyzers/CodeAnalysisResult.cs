// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;

using NonBlocking;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class CodeAnalysisResult
    {
        private readonly ConcurrentDictionary<Project, List<Diagnostic>> _dictionary
            = new ConcurrentDictionary<Project, List<Diagnostic>>();

        internal void AddDiagnostic(Project project, Diagnostic diagnostic)
        {
            _ = _dictionary.AddOrUpdate(project,
                addValueFactory: (key) => new List<Diagnostic>() { diagnostic },
                updateValueFactory: (key, list) =>
                {
                    list.Add(diagnostic);
                    return list;
                });
        }

        public IReadOnlyDictionary<Project, List<Diagnostic>> Diagnostics
            => _dictionary;
    }
}
