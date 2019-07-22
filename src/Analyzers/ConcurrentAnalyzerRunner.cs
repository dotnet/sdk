// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class ConcurrentAnalyzerRunner : IAnalyzerRunner
    {
        private const string NoFormattableDocuments = "Unable to find solution when running code analysis.";

        public static IAnalyzerRunner Instance { get; } = new ConcurrentAnalyzerRunner();

        public async Task<CodeAnalysisResult> RunCodeAnalysisAsync(
            Solution solution,
            ImmutableArray<DiagnosticAnalyzer> analyzers,
            ImmutableArray<DocumentId> formattableDocuments,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (solution is null)
            {
                logger.LogError(NoFormattableDocuments);
                throw new InvalidOperationException(NoFormattableDocuments);
            }

            var documents = formattableDocuments.Select(id => solution.GetDocument(id)).OfType<Document>().ToList();
            var result = new CodeAnalysisResult();
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation is null)
                {
                    continue;
                }

                // TODO: generate option set to ensure the analyzers run
                // TODO: Ensure that the coding conventions snapshop gets passed to the analyzers somehow
                var analyzerCompilation = compilation.WithAnalyzers(analyzers, options: null, cancellationToken);
                var diagnosticResult = await analyzerCompilation.GetAllDiagnosticsAsync(cancellationToken);
                foreach (var diagnostic in diagnosticResult)
                {
                    var doc = documents.Find(d => d.FilePath == diagnostic.Location.GetLineSpan().Path);
                    if (doc != null)
                    {
                        result.AddDiagnostic(doc, diagnostic);
                    }
                }
            }

            return result;
        }
    }
}
