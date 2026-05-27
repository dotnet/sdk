// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal interface ICodeFormatter
    {
        /// <summary>
        /// Gets the fix category this formatter belongs to.
        /// </summary>
        FixCategory Category { get; }

        /// <summary>
        /// Applies formatting and returns a formatted <see cref="Solution"/>.
        /// </summary>
        Task<Solution> FormatAsync(
            Workspace workspace,
            Solution solution,
            ImmutableArray<DocumentId> formattableDocuments,
            FormatOptions options,
            ILogger logger,
            List<FormattedFile> formattedFiles,
            CancellationToken cancellationToken);
    }
}
