
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// ImportsFormatter that uses the <see cref="Formatter"/> to format document import directives.
    /// </summary>
    internal sealed class ImportsFormatter : DocumentFormatter
    {
        // TODO: Use warning from Resources.
        protected override string FormatWarningDescription => "Fix imports.";

        protected override async Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocument = await Formatter.OrganizeImportsAsync(document, cancellationToken).ConfigureAwait(false);
            return await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

