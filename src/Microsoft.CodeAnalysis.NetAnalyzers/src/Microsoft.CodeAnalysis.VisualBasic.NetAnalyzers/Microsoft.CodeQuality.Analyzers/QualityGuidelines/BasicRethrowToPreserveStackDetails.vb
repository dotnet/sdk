' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicRethrowToPreserveStackDetailsAnalyzer
        Inherits RethrowToPreserveStackDetailsAnalyzer

        Public Overrides Sub Initialize(analysisContext As AnalysisContext)
            analysisContext.EnableConcurrentExecution()
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)

            analysisContext.RegisterSyntaxNodeAction(AddressOf AnalyzeNode, SyntaxKind.ThrowStatement)
        End Sub

        Public Shared Sub AnalyzeNode(context As SyntaxNodeAnalysisContext)
            Dim node As SyntaxNode = context.Node
            Dim throwStatement = DirectCast(node, ThrowStatementSyntax)
            Dim throwExpression = throwStatement.Expression
            If throwExpression Is Nothing Then
                Return
            End If

            While node IsNot Nothing
                Select Case node.Kind
                    Case SyntaxKind.MultiLineFunctionLambdaExpression, SyntaxKind.MultiLineSubLambdaExpression,
                         SyntaxKind.SingleLineFunctionLambdaExpression, SyntaxKind.SingleLineSubLambdaExpression,
                         SyntaxKind.ClassBlock, SyntaxKind.StructureBlock, SyntaxKind.ModuleBlock

                        Return

                    Case SyntaxKind.CatchStatement
                        Dim catchStatement = DirectCast(node, CatchStatementSyntax)
                        If IsCaughtLocalThrown(context.SemanticModel, catchStatement, throwExpression) Then
                            context.ReportDiagnostic(CreateDiagnostic(throwStatement))
                            Return
                        End If

                    Case SyntaxKind.CatchBlock
                        Dim catchStatement = DirectCast(node, CatchBlockSyntax).CatchStatement
                        If IsCaughtLocalThrown(context.SemanticModel, catchStatement, throwExpression) Then
                            context.ReportDiagnostic(CreateDiagnostic(throwStatement))
                            Return
                        End If
                End Select

                node = node.Parent
            End While
        End Sub

        Private Shared Function IsCaughtLocalThrown(semanticModel As SemanticModel, catchStatement As CatchStatementSyntax, throwExpression As ExpressionSyntax) As Boolean
            Dim local = TryCast(semanticModel.GetSymbolInfo(throwExpression).Symbol, ILocalSymbol)
            Dim catchBlock = DirectCast(catchStatement.Parent, CatchBlockSyntax)
            If local Is Nothing _
                    OrElse local.Locations.IsEmpty _
                    OrElse catchBlock Is Nothing _
                    OrElse catchBlock.Statements.Count = 0 _
                    OrElse semanticModel.AnalyzeDataFlow(catchBlock.Statements.First, catchBlock.Statements.Last).WrittenInside.Contains(local) Then
                Return False
            End If

            ' if (local.LocalKind) TODO: expose LocalKind In the symbol model?
            If catchStatement.IdentifierName Is Nothing Then
                Return False
            End If

            Return catchStatement.IdentifierName.Span.Contains(local.Locations(0).SourceSpan)
        End Function
    End Class
End Namespace
