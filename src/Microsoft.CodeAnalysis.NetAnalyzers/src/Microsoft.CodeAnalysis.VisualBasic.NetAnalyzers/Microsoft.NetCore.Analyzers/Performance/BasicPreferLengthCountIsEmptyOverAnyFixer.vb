' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicPreferLengthCountIsEmptyOverAnyFixer
        Inherits PreferLengthCountIsEmptyOverAnyFixer

        Protected Overrides Function GetNodeToReplace(node As SyntaxNode) As SyntaxNode
            Dim invocation = TryCast(node, InvocationExpressionSyntax)
            Dim target As SyntaxNode
            If invocation Is Nothing Then
                If TryCast(node, MemberAccessExpressionSyntax) Is Nothing Then
                    Return Nothing
                End If

                target = node
            Else
                If TryCast(invocation.Expression, MemberAccessExpressionSyntax) Is Nothing Then
                    Return Nothing
                End If

                target = invocation
            End If

            If target.Parent.IsKind(SyntaxKind.NotExpression) Then
                Return target.Parent
            End If

            Return target
        End Function

        Protected Overrides Function ReplaceAnyWithIsEmpty(currentNode As SyntaxNode) As SyntaxNode
            Dim isNegated As Boolean
            Dim expression As ExpressionSyntax = Nothing
            If Not TrySplit(currentNode, isNegated, expression) Then
                Return Nothing
            End If

            Dim newMemberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                SyntaxFactory.Token(SyntaxKind.DotToken),
                SyntaxFactory.IdentifierName(PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText)
                )

            If isNegated Then
                Return newMemberAccess.WithTriviaFrom(currentNode)
            End If

            Return SyntaxFactory.UnaryExpression(
                SyntaxKind.NotExpression,
                SyntaxFactory.Token(SyntaxKind.NotKeyword),
                newMemberAccess
                ).WithTriviaFrom(currentNode)
        End Function

        Protected Overrides Function ReplaceAnyWithPropertyCheck(currentNode As SyntaxNode, propertyName As String) As SyntaxNode
            Dim isNegated As Boolean
            Dim expression As ExpressionSyntax = Nothing
            If Not TrySplit(currentNode, isNegated, expression) Then
                Return Nothing
            End If

            Dim expressionKind = If(isNegated, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression)
            Dim tokenKind = If(isNegated, SyntaxKind.EqualsToken, SyntaxKind.LessThanGreaterThanToken)

            Return SyntaxFactory.BinaryExpression(
                expressionKind,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expression,
                    SyntaxFactory.Token(SyntaxKind.DotToken),
                    SyntaxFactory.IdentifierName(propertyName)
                ),
                SyntaxFactory.Token(tokenKind),
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(0)
                    )
                ).WithTriviaFrom(currentNode)
        End Function

        Private Shared Function TrySplit(currentNode As SyntaxNode, ByRef isNegated As Boolean, ByRef expression As ExpressionSyntax) As Boolean
            Dim unary = TryCast(currentNode, UnaryExpressionSyntax)
            isNegated = unary IsNot Nothing AndAlso unary.IsKind(SyntaxKind.NotExpression)

            Dim operand = If(isNegated, CType(unary.Operand, SyntaxNode), currentNode)
            Dim invocation = TryCast(operand, InvocationExpressionSyntax)
            If invocation Is Nothing Then
                Dim memberAccess = TryCast(operand, MemberAccessExpressionSyntax)
                If memberAccess Is Nothing Then
                    Return False
                End If

                expression = memberAccess.Expression

                Return True
            End If

            Dim invokedMemberAccess = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
            If invokedMemberAccess Is Nothing Then
                Return False
            End If

            ' `.Any()` used like a normal static method and not like an extension method.
            If invocation.ArgumentList.Arguments.Count > 0 Then
                expression = invocation.ArgumentList.Arguments(0).GetExpression()
            Else
                expression = invokedMemberAccess.Expression
            End If

            Return True
        End Function
    End Class
End Namespace