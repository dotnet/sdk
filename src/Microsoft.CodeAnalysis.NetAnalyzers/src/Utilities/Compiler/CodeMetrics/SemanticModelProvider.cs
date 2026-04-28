// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if HAS_IOPERATION

using System.Collections.Concurrent;

namespace Microsoft.CodeAnalysis.CodeMetrics
{
    internal sealed class SemanticModelProvider
    {
        private readonly ConcurrentDictionary<SyntaxTree, SemanticModel> _semanticModelMap;
        public SemanticModelProvider(Compilation compilation)
        {
            Compilation = compilation;
            _semanticModelMap = new ConcurrentDictionary<SyntaxTree, SemanticModel>();
        }

        public Compilation Compilation { get; }

        public SemanticModel GetSemanticModel(SyntaxNode node)
            => _semanticModelMap.GetOrAdd(node.SyntaxTree, tree => Compilation.GetSemanticModel(node.SyntaxTree));
    }
}

#endif
