' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' CA1820: Test for empty strings using string length
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicTestForEmptyStringsUsingStringLengthFixer
        Inherits TestForEmptyStringsUsingStringLengthFixer
        Protected Overrides Function GetExpression(node As SyntaxNode) As SyntaxNode
            Dim argumentSyntax = TryCast(node, ArgumentSyntax)
            Return If(argumentSyntax IsNot Nothing, argumentSyntax.GetExpression(), node)
        End Function

        Protected Overrides Function IsEqualsOperator(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.EqualsExpression)
        End Function

        Protected Overrides Function IsNotEqualsOperator(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.NotEqualsExpression)
        End Function

        Protected Overrides Function GetLeftOperand(binaryExpressionSyntax As SyntaxNode) As SyntaxNode
            Return DirectCast(binaryExpressionSyntax, BinaryExpressionSyntax).Left
        End Function

        Protected Overrides Function GetRightOperand(binaryExpressionSyntax As SyntaxNode) As SyntaxNode
            Return DirectCast(binaryExpressionSyntax, BinaryExpressionSyntax).Right
        End Function

        Protected Overrides Function IsFixableBinaryExpression(node As SyntaxNode) As Boolean
            Return (TypeOf node Is BinaryExpressionSyntax) AndAlso (IsEqualsOperator(node) Or IsNotEqualsOperator(node))
        End Function

        Protected Overrides Function IsFixableInvocationExpression(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.InvocationExpression)
        End Function

        Protected Overrides Function GetInvocationTarget(node As SyntaxNode) As SyntaxNode
            Dim invocationExpression = TryCast(node, InvocationExpressionSyntax)
            If invocationExpression IsNot Nothing Then
                Dim memberAccessExpression = TryCast(invocationExpression.Expression, MemberAccessExpressionSyntax)
                If memberAccessExpression IsNot Nothing Then
                    Return memberAccessExpression.Expression
                End If
            End If
            Return Nothing
        End Function
    End Class
End Namespace
