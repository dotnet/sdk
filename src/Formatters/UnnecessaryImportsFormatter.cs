// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Reflection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// UnnecessaryImportsFormatter that removes unsused imports when fixing code style errors.
    /// </summary>
    internal sealed class UnnecessaryImportsFormatter : DocumentFormatter
    {
        internal const string IDE0005 = nameof(IDE0005);
        internal const string Style = nameof(Style);

        protected override string FormatWarningDescription => Resources.Remove_unnecessary_import;

        public override FixCategory Category => FixCategory.CodeStyle;

        internal override async Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet optionSet,
            AnalyzerConfigOptions analyzerConfigOptions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // If we are fixing CodeStyle and the 'IDE0005' diagnostic is configured, then
            // see if we can remove unused imports.

            // If we are not saving files, do not make changes in this formatter. They will be
            // reported by their diagnostic id when the analyzer formatter runs.
            if (!formatOptions.SaveFormattedFiles)
            {
                return sourceText;
            }

            // If diagnostics are being filtered and IDE0005 isn't specified, then make no changes.
            if (!formatOptions.Diagnostics.IsEmpty &&
                !formatOptions.Diagnostics.Contains(IDE0005))
            {
                return sourceText;
            }

            var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            if (tree is null)
            {
                return sourceText;
            }

            var severity = analyzerConfigOptions.GetDiagnosticSeverity(document.Project, tree, IDE0005, Style);
            if (severity < formatOptions.CodeStyleSeverity)
            {
                return sourceText;
            }

            var formattedDocument = await RemoveUnnecessaryImportsHelper.RemoveUnnecessaryImportsAsync(document, cancellationToken).ConfigureAwait(false);
            if (formattedDocument is null)
            {
                return sourceText;
            }

            var isSameVersion = await IsSameDocumentAndVersionAsync(document, formattedDocument, cancellationToken).ConfigureAwait(false);
            if (isSameVersion)
            {
                return sourceText;
            }

            var formattedText = await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
            return formattedText;
        }
    }
}
