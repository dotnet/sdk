' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Analyzer.Utilities
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    ''' <summary>
    ''' CA1836: Prefer IsEmpty over Count when available.
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicPreferIsEmptyOverCountFixer
        Inherits PreferIsEmptyOverCountFixer

        Protected Overrides Function GetObjectExpressionFromOperation(node As SyntaxNode, operationKey As String) As SyntaxNode
            Dim countNode As SyntaxNode = Nothing

            Select Case operationKey
                Case UseCountProperlyAnalyzer.OperationBinaryLeft
                    Dim binaryExpression = TryCast(node, BinaryExpressionSyntax)
                    If binaryExpression IsNot Nothing Then
                        countNode = binaryExpression.Left
                    End If

                Case UseCountProperlyAnalyzer.OperationBinaryRight
                    Dim binaryExpression = TryCast(node, BinaryExpressionSyntax)
                    If binaryExpression IsNot Nothing Then
                        countNode = binaryExpression.Right
                    End If

                Case UseCountProperlyAnalyzer.OperationEqualsArgument
                    Dim invocationExpression = TryCast(node, InvocationExpressionSyntax)
                    If invocationExpression IsNot Nothing Then
                        countNode = invocationExpression.ArgumentList.Arguments(0).GetExpression()
                    End If

                Case UseCountProperlyAnalyzer.OperationEqualsInstance
                    Dim invocationExpression2 = TryCast(node, InvocationExpressionSyntax)
                    If invocationExpression2 IsNot Nothing Then
                        Dim equalsMemberAccess = invocationExpression2.Expression

                        Dim memberAccess = TryCast(equalsMemberAccess, MemberAccessExpressionSyntax)
                        If memberAccess IsNot Nothing Then
                            countNode = memberAccess.Expression
                        End If
                    End If

            End Select

            RoslynDebug.Assert(countNode IsNot Nothing)

            Dim isParenthesizedOrCastExpression As Boolean
            Do
                isParenthesizedOrCastExpression = True

                If TypeOf countNode Is ParenthesizedExpressionSyntax Then
                    countNode = CType(countNode, ParenthesizedExpressionSyntax).Expression

                ElseIf TypeOf countNode Is CastExpressionSyntax Then
                    countNode = CType(countNode, CastExpressionSyntax).Expression

                Else
                    isParenthesizedOrCastExpression = False
                End If
            Loop While isParenthesizedOrCastExpression

            Dim invocationExpression3 = TryCast(countNode, InvocationExpressionSyntax)
            If invocationExpression3 IsNot Nothing Then
                countNode = invocationExpression3.Expression
            End If

            Dim objectNode As SyntaxNode = Nothing

            Dim memberAccess2 = TryCast(countNode, MemberAccessExpressionSyntax)
            If memberAccess2 IsNot Nothing Then
                objectNode = memberAccess2.Expression
            End If

            RoslynDebug.Assert(objectNode IsNot Nothing OrElse TypeOf countNode Is IdentifierNameSyntax)

            Return objectNode
        End Function

    End Class
End Namespace
