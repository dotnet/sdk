// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using NonBlocking;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class CodeAnalysisResult
    {
        private readonly ConcurrentDictionary<DocumentId, IList<Diagnostic>> _dictionary
            = new ConcurrentDictionary<DocumentId, IList<Diagnostic>>();

        internal void AddDiagnostic(Document document, Diagnostic diagnostic)
        {
            _ = _dictionary.AddOrUpdate(document.Id,
                addValueFactory: (key) =>
                 {
                     var list = new List<Diagnostic> { diagnostic };
                     return list;
                 },
                updateValueFactory: (key, list) =>
                {
                    list.Add(diagnostic);
                    return list;
                });
        }

        public IReadOnlyDictionary<DocumentId, ImmutableArray<Diagnostic>> Diagnostics
            => new Dictionary<DocumentId, ImmutableArray<Diagnostic>>(
                _dictionary.Select(
                    x => KeyValuePair.Create(
                        x.Key,
                        x.Value.ToImmutableArray())));
    }
}
