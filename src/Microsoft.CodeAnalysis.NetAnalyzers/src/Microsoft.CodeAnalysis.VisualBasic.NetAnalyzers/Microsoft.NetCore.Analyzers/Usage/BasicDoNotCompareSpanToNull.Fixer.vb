' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Analyzer.Utilities
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Tasks
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public Class BasicDoNotCompareSpanToNullFixer
        Inherits DoNotCompareSpanToNullFixer

        Public Overrides Async Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim root = Await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)
            Dim condition = root.FindNode(context.Span, getInnermostNodeForTie:=True)
            Dim binaryExpression = TryCast(condition, BinaryExpressionSyntax)
            If binaryExpression Is Nothing Then
                Return
            End If

            Dim memberAccess As ExpressionSyntax = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GetComparatorExpression(binaryExpression).WithoutTrailingTrivia(),
                SyntaxFactory.Token(SyntaxKind.DotToken),
                SyntaxFactory.IdentifierName(IsEmpty)
            )

            If binaryExpression.IsKind(SyntaxKind.NotEqualsExpression) Then
                memberAccess = SyntaxFactory.NotExpression(memberAccess)
            End If

            Dim useIsEmptyCodeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.DoNotCompareSpanToNullIsEmptyCodeFixTitle,
                Function(ct) Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(binaryExpression, memberAccess))),
                MicrosoftNetCoreAnalyzersResources.DoNotCompareSpanToNullIsEmptyCodeFixTitle
            )
            context.RegisterCodeFix(useIsEmptyCodeAction, context.Diagnostics)
        End Function

        Private Shared Function GetComparatorExpression(binaryExpression As BinaryExpressionSyntax) As ExpressionSyntax
            If binaryExpression.Left.IsKind(SyntaxKind.NothingLiteralExpression) Then
                Return binaryExpression.Right
            Else
                Return binaryExpression.Left
            End If
        End Function
    End Class
End Namespace