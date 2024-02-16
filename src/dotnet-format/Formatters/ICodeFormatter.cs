// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
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
