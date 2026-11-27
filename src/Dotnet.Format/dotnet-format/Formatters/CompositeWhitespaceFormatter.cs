// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// Shares document setup across whitespace formatters while preserving each formatter's
    /// diagnostic attribution and formatter-first reporting order.
    /// </summary>
    internal sealed class CompositeWhitespaceFormatter : ICodeFormatter
    {
        private static readonly ImmutableArray<DocumentFormatter> s_formatters = ImmutableArray.Create<DocumentFormatter>(
            new WhitespaceFormatter(), new FinalNewlineFormatter(), new EndOfLineFormatter(), new CharsetFormatter());

        public string Name => "WHITESPACE";

        public FixCategory Category => FixCategory.Whitespace;

        public Task<Solution> FormatAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions formatOptions,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken)
            => DocumentFormatter.FormatAsync(s_formatters, solution, formattableDocuments, formatOptions, logger, formattedFiles, cancellationToken);
    }
}
