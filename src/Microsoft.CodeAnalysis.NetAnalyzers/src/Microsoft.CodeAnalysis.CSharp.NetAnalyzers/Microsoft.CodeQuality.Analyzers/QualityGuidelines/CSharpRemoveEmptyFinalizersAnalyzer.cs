// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class CSharpRemoveEmptyFinalizersAnalyzer : AbstractRemoveEmptyFinalizersAnalyzer
    {
        protected override bool IsEmptyFinalizer(SyntaxNode methodBody, CodeBlockAnalysisContext analysisContext)
        {
            var destructorDeclaration = (DestructorDeclarationSyntax)methodBody;
            if (destructorDeclaration.Body != null)
            {
                // Bail out for error case where both expression and block body are non-null.
                if (destructorDeclaration.ExpressionBody != null)
                {
                    return false;
                }

                return IsEmptyBlockBody(destructorDeclaration.Body, analysisContext.SemanticModel);
            }

            if (destructorDeclaration.ExpressionBody != null)
            {
                return IsEmptyExpressionBody(destructorDeclaration.ExpressionBody, analysisContext.SemanticModel);
            }

            // Return false for error case where both expression and block body are null.
            return false;
        }

        private static bool IsEmptyBlockBody(BlockSyntax blockBody, SemanticModel semanticModel)
        {
            switch (blockBody.Statements.Count)
            {
                case 0:
                    return true;

                case 1:
                    var body = blockBody.Statements[0];
                    switch (body.Kind())
                    {
                        case SyntaxKind.ThrowStatement:
                            return true;

                        case SyntaxKind.ExpressionStatement:
                            return ((ExpressionStatementSyntax)body).Expression is InvocationExpressionSyntax invocationExpr &&
                                IsConditionalInvocation(invocationExpr, semanticModel);
                    }

                    break;
            }

            return false;
        }

        private static bool IsEmptyExpressionBody(ArrowExpressionClauseSyntax expressionBody, SemanticModel semanticModel)
        {
            return (expressionBody.Expression.Kind()) switch
            {
                SyntaxKind.ThrowExpression => true,

                SyntaxKind.InvocationExpression => IsConditionalInvocation((InvocationExpressionSyntax)expressionBody.Expression, semanticModel),

                _ => false,
            };
        }

        private static bool IsConditionalInvocation(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel)
        {
            if (!(semanticModel.GetSymbolInfo(invocationExpr).Symbol is IMethodSymbol invocationSymbol))
            {
                // Presumably, if the user has typed something but it doesn't have a symbol yet, the body won't be empty
                // once all compile errors are corrected, so we return false here.
                return false;
            }

            var conditionalAttributeSymbol = semanticModel.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsConditionalAttribute);
            return InvocationIsConditional(invocationSymbol, conditionalAttributeSymbol);
        }
    }
}
