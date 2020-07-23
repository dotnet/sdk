' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        ''' Tries the get a fixer the specified <paramref name="node" />.
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

                    If Not invocation Is Nothing Then

                        Dim member = TryCast(invocation.Expression, MemberAccessExpressionSyntax)

                        If Not member Is Nothing Then

                            GetExpressionAndInvocationArguments(
                                sourceExpression:=member.Expression,
                                isAsync:=isAsync,
                                expression:=expression,
                                arguments:=arguments)

                            Return True

                        End If

                    End If

                Case UseCountProperlyAnalyzer.OperationEqualsArgument

                    Dim invocation = TryCast(node, InvocationExpressionSyntax)

                    If Not invocation Is Nothing AndAlso invocation.ArgumentList.Arguments.Count = 1 Then

                        GetExpressionAndInvocationArguments(
                            sourceExpression:=invocation.ArgumentList.Arguments(0).GetExpression(),
                            isAsync:=isAsync,
                            expression:=expression,
                            arguments:=arguments)

                        Return True

                    End If

                Case UseCountProperlyAnalyzer.OperationBinaryLeft

                    Dim binary = TryCast(node, BinaryExpressionSyntax)

                    If Not binary Is Nothing Then

                        GetExpressionAndInvocationArguments(
                            sourceExpression:=binary.Left,
                            isAsync:=isAsync,
                            expression:=expression,
                            arguments:=arguments)

                        Return True

                    End If

                Case UseCountProperlyAnalyzer.OperationBinaryRight

                    Dim binary = TryCast(node, BinaryExpressionSyntax)

                    If Not binary Is Nothing Then

                        GetExpressionAndInvocationArguments(
                            sourceExpression:=binary.Right,
                            isAsync:=isAsync,
                            expression:=expression,
                            arguments:=arguments)

                        Return True

                    End If


            End Select

            Return False

        End Function

        Private Shared Sub GetExpressionAndInvocationArguments(sourceExpression As ExpressionSyntax, isAsync As Boolean, ByRef expression As SyntaxNode, ByRef arguments As IEnumerable(Of SyntaxNode))

            Dim parenthesizedExpression = TryCast(sourceExpression, ParenthesizedExpressionSyntax)

            While Not parenthesizedExpression Is Nothing

                sourceExpression = parenthesizedExpression.Expression
                parenthesizedExpression = TryCast(sourceExpression, ParenthesizedExpressionSyntax)

            End While

            Dim invocationExpression As InvocationExpressionSyntax = Nothing

            If isAsync Then

                Dim awaitExpressionSyntax = TryCast(sourceExpression, AwaitExpressionSyntax)

                If Not awaitExpressionSyntax Is Nothing Then

                    invocationExpression = TryCast(awaitExpressionSyntax.Expression, InvocationExpressionSyntax)

                End If

            Else

                invocationExpression = TryCast(sourceExpression, InvocationExpressionSyntax)

            End If

            If invocationExpression Is Nothing Then

                expression = Nothing
                arguments = Nothing
                Return

            End If

            expression = DirectCast(invocationExpression.Expression, MemberAccessExpressionSyntax).Expression
            arguments = invocationExpression.ArgumentList.ChildNodes()

        End Sub

    End Class

End Namespace
