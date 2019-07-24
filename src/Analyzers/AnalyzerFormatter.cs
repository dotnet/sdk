// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerFormatter : ICodeFormatter
    {
        public FormatType FormatType => FormatType.CodeStyle;

        private readonly IAnalyzerFinder _finder;
        private readonly IAnalyzerRunner _runner;
        private readonly ICodeFixApplier _applier;

        public AnalyzerFormatter(
            IAnalyzerFinder finder,
            IAnalyzerRunner runner,
            ICodeFixApplier applier)
        {
            _finder = finder;
            _runner = runner;
            _applier = applier;
        }

        public async Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions options,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
        {
            var paths = formattableDocuments.Select(id => solution.GetDocument(id)?.FilePath)
                .OfType<string>().ToImmutableArray();

            var pairs = _finder.GetAnalyzersAndFixers();
            foreach (var (analyzer, codefix) in pairs)
            {
                var result = new CodeAnalysisResult();
                await solution.Projects.ForEachAsync(async (project, token) =>
                {
                    await _runner.RunCodeAnalysisAsync(result, analyzer, project, paths, logger, token);
                }, cancellationToken);

                if (codefix is object)
                {
                    solution = await _applier.ApplyCodeFixesAsync(solution, result, codefix, logger, cancellationToken);
                }
            }

            return solution;
        }
    }
}
