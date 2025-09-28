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

        Protected Overrides Function ReplaceAnyWithIsEmpty(root As SyntaxNode, node As SyntaxNode) As SyntaxNode
            Dim invocation = TryCast(node, InvocationExpressionSyntax)
            Dim memberAccess As MemberAccessExpressionSyntax
            If invocation Is Nothing Then
                memberAccess = TryCast(node, MemberAccessExpressionSyntax)
                If memberAccess Is Nothing Then
                    Return Nothing
                End If

                Dim newMemberAccess = memberAccess.WithName(
                    SyntaxFactory.IdentifierName(PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText)
                )
                Dim unaryParent = TryCast(memberAccess.Parent, UnaryExpressionSyntax)
                If unaryParent IsNot Nothing And unaryParent.IsKind(SyntaxKind.NotExpression) Then
                    Return root.ReplaceNode(unaryParent, newMemberAccess.WithTriviaFrom(unaryParent))
                End If

                Dim negatedExpression = SyntaxFactory.UnaryExpression(
                    SyntaxKind.NotExpression,
                    SyntaxFactory.Token(SyntaxKind.NotKeyword),
                    newMemberAccess
                    )

                Return root.ReplaceNode(memberAccess, negatedExpression.WithTriviaFrom(memberAccess))
            Else
                memberAccess = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
                If memberAccess Is Nothing Then
                    Return Nothing
                End If

                Dim expression = memberAccess.Expression
                If invocation.ArgumentList.Arguments.Count > 0 Then
                    expression = invocation.ArgumentList.Arguments(0).GetExpression()
                End If

                Dim newMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expression,
                    SyntaxFactory.Token(SyntaxKind.DotToken),
                    SyntaxFactory.IdentifierName(PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText)
                    )
                Dim unaryParent = TryCast(invocation.Parent, UnaryExpressionSyntax)
                If unaryParent IsNot Nothing And unaryParent.IsKind(SyntaxKind.NotExpression) Then
                    Return root.ReplaceNode(unaryParent, newMemberAccess.WithTriviaFrom(unaryParent))
                End If

                Dim negatedExpression = SyntaxFactory.UnaryExpression(
                    SyntaxKind.NotExpression,
                    SyntaxFactory.Token(SyntaxKind.NotKeyword),
                    newMemberAccess
                    )

                Return root.ReplaceNode(invocation, negatedExpression.WithTriviaFrom(invocation))
            End If
        End Function

        Protected Overrides Function ReplaceAnyWithLength(root As SyntaxNode, node As SyntaxNode) As SyntaxNode
            Return ReplaceAnyWithPropertyCheck(root, node, PreferLengthCountIsEmptyOverAnyAnalyzer.LengthText)
        End Function

        Protected Overrides Function ReplaceAnyWithCount(root As SyntaxNode, node As SyntaxNode) As SyntaxNode
            Return ReplaceAnyWithPropertyCheck(root, node, PreferLengthCountIsEmptyOverAnyAnalyzer.CountText)
        End Function

        Private Shared Function ReplaceAnyWithPropertyCheck(root As SyntaxNode, node As SyntaxNode, propertyName As String) As SyntaxNode
            Dim invocation = TryCast(node, InvocationExpressionSyntax)
            Dim memberAccess As MemberAccessExpressionSyntax
            If invocation Is Nothing Then
                memberAccess = TryCast(node, MemberAccessExpressionSyntax)
                If memberAccess Is Nothing Then
                    Return Nothing
                End If

                If memberAccess.Parent.IsKind(SyntaxKind.NotExpression) Then
                    Dim binaryExpression = GetBinaryExpression(memberAccess.Expression, propertyName, SyntaxKind.EqualsExpression)
                    Return root.ReplaceNode(memberAccess.Parent, binaryExpression.WithTriviaFrom(memberAccess.Parent))
                End If

                Return root.ReplaceNode(memberAccess, GetBinaryExpression(memberAccess.Expression, propertyName, SyntaxKind.NotEqualsExpression).WithTriviaFrom(memberAccess))
            Else
                memberAccess = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
                If memberAccess Is Nothing Then
                    Return Nothing
                End If

                Dim expression = memberAccess.Expression
                If invocation.ArgumentList.Arguments.Count > 0 Then
                    expression = invocation.ArgumentList.Arguments(0).GetExpression()
                End If

                If invocation.Parent.IsKind(SyntaxKind.NotExpression) Then
                    Dim binaryExpression = GetBinaryExpression(expression, propertyName, SyntaxKind.EqualsExpression)
                    Return root.ReplaceNode(invocation.Parent, binaryExpression.WithTriviaFrom(invocation.Parent))
                End If

                Return root.ReplaceNode(invocation, GetBinaryExpression(expression, propertyName, SyntaxKind.NotEqualsExpression).WithTriviaFrom(invocation))
            End If
        End Function

        Private Shared Function GetBinaryExpression(expression As ExpressionSyntax, member As String, expressionKind As SyntaxKind) As BinaryExpressionSyntax
            Dim tokenKind = If(expressionKind = SyntaxKind.EqualsExpression, SyntaxKind.EqualsToken, SyntaxKind.LessThanGreaterThanToken)
            return SyntaxFactory.BinaryExpression(
                expressionKind,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expression,
                    SyntaxFactory.Token(SyntaxKind.DotToken),
                    SyntaxFactory.IdentifierName(member)
                ),
                SyntaxFactory.Token(tokenKind),
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(0)
                    )
                )
        End Function
    End Class
End Namespace