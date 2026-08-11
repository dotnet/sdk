// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal interface ICodeFixApplier
    {
        Task<Solution> ApplyCodeFixesAsync(
            Solution solution,
            CodeAnalysisResult result,
            CodeFixProvider codefixes,
            string diagnosticId,
            ILogger logger,
            CancellationToken cancellationToken);
    }
}
