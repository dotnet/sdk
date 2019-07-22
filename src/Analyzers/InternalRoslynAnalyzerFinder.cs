// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class InternalRoslynAnalyzerFinder : IAnalyzerFinder
    {
        public static IAnalyzerFinder Instance { get; } = new InternalRoslynAnalyzerFinder();

        public Task<ImmutableArray<DiagnosticAnalyzer>> FindAllAnalyzersAsync(ILogger logger, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ImmutableArray<CodeFixProvider>> FindAllCodeFixesAsync(ILogger logger, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
