// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class AnalyzerFormatter : ICodeFormatter
    {
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
            if (!options.FormatType.HasFlag(FormatType.CodeStyle))
            {
                return solution;
            }

            var analyzers = await _finder.FindAllAnalyzersAsync(logger, cancellationToken);
<<<<<<< HEAD
            var result = await _runner.RunCodeAnalysisAsync(analyzers, formattableDocuments, logger, cancellationToken);
||||||| parent of 817ae5b... Added FormatType and--fix - style flag to options
             var result = await _runner.RunCodeAnalysisAsync(analyzers, formattableDocuments, logger, cancellationToken);
=======
            var result = await _runner.RunCodeAnalysisAsync(solution, analyzers, formattableDocuments, logger, cancellationToken);
>>>>>>> 817ae5b... Added FormatType and --fix-style flag to options
            var codefixes = await _finder.FindAllCodeFixesAsync(logger, cancellationToken);

            return await _applier.ApplyCodeFixesAsync(solution, result, codefixes, formattableDocuments, logger, cancellationToken);
        }
    }
}
