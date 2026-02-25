// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal struct AnalyzersAndFixers
    {
        public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; }
        public ImmutableArray<CodeFixProvider> Fixers { get; }

        public AnalyzersAndFixers(ImmutableArray<DiagnosticAnalyzer> analyzers, ImmutableArray<CodeFixProvider> fixers)
        {
            Analyzers = analyzers;
            Fixers = fixers;
        }

        public void Deconstruct(
            out ImmutableArray<DiagnosticAnalyzer> analyzers,
            out ImmutableArray<CodeFixProvider> fixers)
        {
            analyzers = Analyzers;
            fixers = Fixers;
        }
    }
}
