' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Tasks
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public Class BasicDoNotCompareSpanToNullFixer
        Inherits DoNotCompareSpanToNullFixer

        Protected Overrides Function MakeIsEmptyCheck(comparison As SyntaxNode) As SyntaxNode
            Dim binaryExpression = TryCast(comparison, BinaryExpressionSyntax)
            If binaryExpression Is Nothing Then
                Return Nothing
            End If

            Dim memberAccess As ExpressionSyntax = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GetComparatorExpression(binaryExpression).WithoutTrailingTrivia(),
                SyntaxFactory.Token(SyntaxKind.DotToken),
                SyntaxFactory.IdentifierName(IsEmpty)
            )

            If binaryExpression.IsKind(SyntaxKind.NotEqualsExpression) Then
                Return SyntaxFactory.NotExpression(memberAccess)
            End If

            Return memberAccess
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