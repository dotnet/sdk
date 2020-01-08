' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Analyzer.Utilities.Extensions
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicRemoveEmptyFinalizersAnalyzer
        Inherits AbstractRemoveEmptyFinalizersAnalyzer

        Protected Overrides Function IsEmptyFinalizer(methodBody As SyntaxNode, analysisContext As CodeBlockAnalysisContext) As Boolean
            Dim destructorStatement = DirectCast(methodBody, MethodStatementSyntax)
            Dim destructorBlock = DirectCast(destructorStatement.Parent, MethodBlockSyntax)

            If (destructorBlock.Statements.Count = 0) Then
                Return True
            ElseIf (destructorBlock.Statements.Count = 1) Then
                If (destructorBlock.Statements(0).Kind() = CodeAnalysis.VisualBasic.SyntaxKind.ThrowStatement) Then
                    Return True
                End If

                If (destructorBlock.Statements(0).Kind() = CodeAnalysis.VisualBasic.SyntaxKind.ExpressionStatement) Then
                    Dim destructorExpression = DirectCast(destructorBlock.Statements(0), ExpressionStatementSyntax)
                    If (destructorExpression.Expression.Kind() = CodeAnalysis.VisualBasic.SyntaxKind.InvocationExpression) Then
                        Dim invocationSymbol = DirectCast(analysisContext.SemanticModel.GetSymbolInfo(destructorExpression.Expression).Symbol, IMethodSymbol)
                        If (invocationSymbol Is Nothing) Then
                            ' Presumably, if the user has typed something but hasn't completed it yet, they're not going to have an empty body,
                            ' so we return False here
                            Return False
                        End If

                        Dim conditionalAttributeSymbol = analysisContext.SemanticModel.Compilation.GetOrCreateTypeByMetadataName(GetType(ConditionalAttribute).FullName)
                        Return InvocationIsConditional(invocationSymbol, conditionalAttributeSymbol)
                    End If
                End If
            End If
            Return False
        End Function
    End Class
End Namespace
