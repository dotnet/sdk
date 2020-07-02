// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpRethrowToPreserveStackDetailsAnalyzer : RethrowToPreserveStackDetailsAnalyzer
    {
        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSyntaxNodeAction<SyntaxKind>(AnalyzeNode, SyntaxKind.ThrowStatement);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var throwStatement = (ThrowStatementSyntax)context.Node;
            ExpressionSyntax expr = throwStatement.Expression;
            if (expr == null)
            {
                return;
            }

            for (SyntaxNode syntax = throwStatement; syntax != null; syntax = syntax.Parent)
            {
                switch (syntax.Kind())
                {
                    case SyntaxKind.CatchClause:
                        {
                            if (syntax is CatchClauseSyntax catchClause &&
                                catchClause.Declaration != null &&
                                catchClause.Declaration.Identifier.RawKind != 0)
                            {
                                if (!(context.SemanticModel.GetSymbolInfo(expr).Symbol is ILocalSymbol local)
                                    || local.Locations.IsEmpty
                                    || context.SemanticModel.AnalyzeDataFlow(catchClause.Block).WrittenInside.Contains(local))
                                {
                                    return;
                                }

                                // if (local.LocalKind != LocalKind.Catch) return; // TODO: expose LocalKind in the symbol model?

                                if (catchClause.Declaration.Span.Contains(local.Locations[0].SourceSpan))
                                {
                                    context.ReportDiagnostic(CreateDiagnostic(throwStatement));
                                    return;
                                }
                            }
                        }

                        break;

                    case SyntaxKind.ParenthesizedLambdaExpression:
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.AnonymousMethodExpression:
                    case SyntaxKind.ClassDeclaration:
                    case SyntaxKind.StructDeclaration:
                        return;
                }
            }
        }
    }
}
