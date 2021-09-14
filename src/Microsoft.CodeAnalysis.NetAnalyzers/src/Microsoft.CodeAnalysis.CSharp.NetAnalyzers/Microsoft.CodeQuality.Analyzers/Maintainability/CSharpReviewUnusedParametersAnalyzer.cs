// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    /// <summary>
    /// CA1801: Review unused parameters
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpReviewUnusedParametersAnalyzer : ReviewUnusedParametersAnalyzer
    {
        private const SyntaxKind RecordDeclaration = (SyntaxKind)9063;

        protected override bool IsPositionalRecordPrimaryConstructor(IMethodSymbol methodSymbol)
        {
            if (methodSymbol.MethodKind != MethodKind.Constructor)
            {
                return false;
            }

            if (methodSymbol.DeclaringSyntaxReferences.Length == 0)
            {
                return false;
            }

            return methodSymbol.DeclaringSyntaxReferences[0]
                .GetSyntax()
                .IsKind(RecordDeclaration);
        }
    }
}
