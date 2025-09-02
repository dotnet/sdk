' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    ''' <summary>
    ''' CA1827: Do not use Count()/LongCount() when Any() can be used.
    ''' CA1828: Do not use CountAsync()/LongCountAsync() when AnyAsync() can be used.
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicDoNotUseCountWhenAnyCanBeUsedFixer
        Inherits DoNotUseCountWhenAnyCanBeUsedFixer

        ''' <summary>
        ''' Tries to get a fixer for the specified <paramref name="node" />.
        ''' </summary>
        ''' <param name="node">The node to get a fixer for.</param>
        ''' <param name="operation">The operation to get the fixer from.</param>
        ''' <param name="isAsync"><see langword="true" /> if it's an asynchronous method <see langword="false" /> otherwise.</param>
        ''' <param name="expression">If this method returns <see langword="true" />, contains the expression to be used to invoke <c>Any</c>.</param>
        ''' <param name="arguments">If this method returns <see langword="true" />, contains the arguments from <c>Any</c> to be used on <c>Count</c>.</param>
        ''' <returns><see langword="true" /> if a fixer was found., <see langword="false" /> otherwise.</returns>
        Protected Overrides Function TryGetFixer(node As SyntaxNode, operation As String, isAsync As Boolean, ByRef expression As SyntaxNode, ByRef arguments As IEnumerable(Of SyntaxNode)) As Boolean

            Select Case operation

                Case UseCountProperlyAnalyzer.OperationEqualsInstance

                    Dim invocation = TryCast(node, InvocationExpressionSyntax)

                    If invocation IsNot Nothing Then

                        Dim member = TryCast(invocation.Expression, MemberAccessExpressionSyntax)

                        If member IsNot Nothing Then

                            Return TryGetExpressionAndInvocationArguments(
                                sourceExpression:=member.Expression,
                                isAsync:=isAsync,
                                expression:=expression,
                                arguments:=arguments)

                        End If

                    End If

                Case UseCountProperlyAnalyzer.OperationEqualsArgument

                    Dim invocation = TryCast(node, InvocationExpressionSyntax)

                    If invocation IsNot Nothing AndAlso invocation.ArgumentList.Arguments.Count = 1 Then

                        Return TryGetExpressionAndInvocationArguments(
                            sourceExpression:=invocation.ArgumentList.Arguments(0).GetExpression(),
                            isAsync:=isAsync,
                            expression:=expression,
                            arguments:=arguments)

                    End If

                Case UseCountProperlyAnalyzer.OperationBinaryLeft

                    Dim binary = TryCast(node, BinaryExpressionSyntax)

                    If binary IsNot Nothing Then

                        Return TryGetExpressionAndInvocationArguments(
                            sourceExpression:=binary.Left,
                            isAsync:=isAsync,
                            expression:=expression,
                            arguments:=arguments)
                    End If

                Case UseCountProperlyAnalyzer.OperationBinaryRight

                    Dim binary = TryCast(node, BinaryExpressionSyntax)

                    If binary IsNot Nothing Then

                        Return TryGetExpressionAndInvocationArguments(
                            sourceExpression:=binary.Right,
                            isAsync:=isAsync,
                            expression:=expression,
                            arguments:=arguments)

                    End If

            End Select

            Return False

        End Function

        Private Shared Function TryGetExpressionAndInvocationArguments(sourceExpression As ExpressionSyntax, isAsync As Boolean, ByRef expression As SyntaxNode, ByRef arguments As IEnumerable(Of SyntaxNode)) As Boolean

            Dim parenthesizedExpression = TryCast(sourceExpression, ParenthesizedExpressionSyntax)

            While parenthesizedExpression IsNot Nothing

                sourceExpression = parenthesizedExpression.Expression
                parenthesizedExpression = TryCast(sourceExpression, ParenthesizedExpressionSyntax)

            End While

            If isAsync Then

                Dim awaitExpressionSyntax = TryCast(sourceExpression, AwaitExpressionSyntax)

                If awaitExpressionSyntax IsNot Nothing Then

                    sourceExpression = awaitExpressionSyntax.Expression

                End If

            End If

            Dim invocationExpression As InvocationExpressionSyntax = TryCast(sourceExpression, InvocationExpressionSyntax)

            If invocationExpression IsNot Nothing Then

                expression = DirectCast(invocationExpression.Expression, MemberAccessExpressionSyntax).Expression
                arguments = invocationExpression.ArgumentList.ChildNodes()
                Return True

            End If

            '  Try to get source expression as MemberAccessExpression which is only legal on VB.
            Dim memberAccessExpression = TryCast(sourceExpression, MemberAccessExpressionSyntax)

            ' This case happens for something like: x = GetData().Count where Count method is called without parentheses.
            If memberAccessExpression IsNot Nothing Then
                expression = memberAccessExpression.Expression
                arguments = Enumerable.Empty(Of SyntaxNode)()
                Return True
            End If

            expression = Nothing
            arguments = Nothing
            Return False
        End Function

    End Class

End Namespace
