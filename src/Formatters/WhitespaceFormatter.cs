// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CodeFormatter that uses the <see cref="Formatter"/> to format document whitespace.
    /// </summary>
    internal sealed class WhitespaceFormatter : DocumentFormatter
    {
        protected override async Task<SourceText> FormatFileAsync(
            Document document,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocument = await Formatter.FormatAsync(document, options, cancellationToken).ConfigureAwait(false);
            return await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
