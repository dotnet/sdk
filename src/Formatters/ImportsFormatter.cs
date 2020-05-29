// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// ImportsFormatter that uses the <see cref="Formatter"/> to format document import directives.
    /// </summary>
    internal sealed class ImportsFormatter : DocumentFormatter
    {
        protected override string FormatWarningDescription => Resources.Fix_imports_ordering;
        private readonly DocumentFormatter _endOfLineFormatter = new EndOfLineFormatter();

        internal override async Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions? analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var organizedDocument = await Formatter.OrganizeImportsAsync(document, cancellationToken);
            if (organizedDocument == document)
            {
                return sourceText;
            }

            // Because the Formatter does not abide the `end_of_line` option we have to fix up the ends of the organized lines.
            var organizedSourceText = await organizedDocument.GetTextAsync(cancellationToken);
            return await _endOfLineFormatter.FormatFileAsync(organizedDocument, organizedSourceText, optionSet, analyzerConfigOptions, formatOptions, logger, cancellationToken);
        }
    }
}

