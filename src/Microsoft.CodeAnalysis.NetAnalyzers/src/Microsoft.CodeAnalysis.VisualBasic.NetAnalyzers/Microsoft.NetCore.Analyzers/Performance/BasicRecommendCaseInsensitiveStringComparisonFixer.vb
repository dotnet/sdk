' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance

    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicRecommendCaseInsensitiveStringComparisonFixer
        Inherits RecommendCaseInsensitiveStringComparisonFixer

        Protected Overrides Function GetNewArgumentsForInvocation(generator As SyntaxGenerator,
                caseChangingApproachValue As String, mainInvocationOperation As IInvocationOperation, stringComparisonType As INamedTypeSymbol,
                leftOffendingMethod As String, rightOffendingMethod As String, ByRef mainInvocationInstance As SyntaxNode) As IEnumerable(Of SyntaxNode)

            Dim invocationExpression As InvocationExpressionSyntax = TryCast(mainInvocationOperation.Syntax, InvocationExpressionSyntax)

            mainInvocationInstance = Nothing

            Dim memberAccessExpression As MemberAccessExpressionSyntax = TryCast(invocationExpression.Expression, MemberAccessExpressionSyntax)
            If memberAccessExpression IsNot Nothing Then
                Dim internalExpression As ExpressionSyntax = memberAccessExpression.Expression

                Dim parenthesizedExpression As ParenthesizedExpressionSyntax = TryCast(internalExpression, ParenthesizedExpressionSyntax)
                While parenthesizedExpression IsNot Nothing
                    internalExpression = parenthesizedExpression.Expression
                    parenthesizedExpression = TryCast(internalExpression, ParenthesizedExpressionSyntax)
                End While

                Dim internalInvocationExpression As InvocationExpressionSyntax = TryCast(internalExpression, InvocationExpressionSyntax)
                Dim internalMemberAccessExpression As MemberAccessExpressionSyntax = Nothing
                If internalInvocationExpression IsNot Nothing Then
                    internalMemberAccessExpression = TryCast(internalInvocationExpression.Expression, MemberAccessExpressionSyntax)
                End If

                If leftOffendingMethod IsNot Nothing AndAlso
                   internalInvocationExpression IsNot Nothing AndAlso
                   internalMemberAccessExpression IsNot Nothing AndAlso
                   internalMemberAccessExpression.Name IsNot Nothing AndAlso
                   internalMemberAccessExpression.Name.Identifier.Text IsNot Nothing AndAlso
                   internalMemberAccessExpression.Name.Identifier.Text = leftOffendingMethod Then

                    mainInvocationInstance = internalMemberAccessExpression.Expression
                Else
                    mainInvocationInstance = memberAccessExpression.Expression
                End If

            End If

            Dim arguments As New List(Of SyntaxNode)
            Dim isAnyArgumentNamed As Boolean = False

            For Each arg As IArgumentOperation In mainInvocationOperation.Arguments

                Dim newArgumentNode As SyntaxNode

                Dim actualArgumentNode As SyntaxNode = arg.Syntax

                Dim argumentSyntaxNode As SimpleArgumentSyntax = TryCast(actualArgumentNode, SimpleArgumentSyntax)
                While argumentSyntaxNode Is Nothing
                    actualArgumentNode = actualArgumentNode.Parent
                    argumentSyntaxNode = TryCast(actualArgumentNode, SimpleArgumentSyntax)
                End While

                If actualArgumentNode IsNot Nothing Then
                    argumentSyntaxNode = TryCast(actualArgumentNode, SimpleArgumentSyntax)
                End If

                Dim argumentName As String = Nothing
                If argumentSyntaxNode IsNot Nothing AndAlso
                   argumentSyntaxNode.NameColonEquals IsNot Nothing AndAlso
                   argumentSyntaxNode.NameColonEquals.Name IsNot Nothing AndAlso
                   argumentSyntaxNode.NameColonEquals.Name.Identifier.ValueText IsNot Nothing Then
                    argumentName = argumentSyntaxNode.NameColonEquals.Name.Identifier.ValueText
                End If

                isAnyArgumentNamed = isAnyArgumentNamed Or argumentName IsNot Nothing

                If rightOffendingMethod IsNot Nothing And arg.Parameter.Type.Name = StringTypeName Then
                    Dim desiredExpression As ExpressionSyntax = Nothing

                    Dim argumentExpression As SimpleArgumentSyntax = TryCast(arg.Syntax, SimpleArgumentSyntax)
                    Dim argumentInvocationExpression As InvocationExpressionSyntax = TryCast(arg.Syntax, InvocationExpressionSyntax)

                    If argumentExpression IsNot Nothing Then

                        desiredExpression = argumentExpression.Expression
                        Dim parenthesizedExpression As ParenthesizedExpressionSyntax = TryCast(desiredExpression, ParenthesizedExpressionSyntax)
                        While parenthesizedExpression IsNot Nothing
                            desiredExpression = parenthesizedExpression.Expression
                            parenthesizedExpression = TryCast(desiredExpression, ParenthesizedExpressionSyntax)
                        End While

                    ElseIf argumentInvocationExpression IsNot Nothing Then

                        desiredExpression = argumentInvocationExpression

                    End If

                    Dim invocation As InvocationExpressionSyntax = TryCast(desiredExpression, InvocationExpressionSyntax)
                    Dim argumentMemberAccessExpression As MemberAccessExpressionSyntax = Nothing

                    If invocation IsNot Nothing Then
                        argumentMemberAccessExpression = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
                    End If

                    If invocation IsNot Nothing And argumentMemberAccessExpression IsNot Nothing Then

                        If argumentName = RecommendCaseInsensitiveStringComparisonAnalyzer.StringParameterName Then
                            newArgumentNode = generator.Argument(RecommendCaseInsensitiveStringComparisonAnalyzer.StringParameterName, RefKind.None, argumentMemberAccessExpression.Expression)
                        Else
                            newArgumentNode = generator.Argument(argumentMemberAccessExpression.Expression)
                        End If

                    Else
                        newArgumentNode = arg.Syntax
                    End If
                Else
                    newArgumentNode = arg.Syntax
                End If

                arguments.Add(newArgumentNode.WithTriviaFrom(arg.Syntax))

            Next

            Debug.Assert(mainInvocationInstance IsNot Nothing)

            Dim stringComparisonArgument As SyntaxNode = GetNewStringComparisonArgument(generator, stringComparisonType, caseChangingApproachValue, isAnyArgumentNamed)

            arguments.Add(stringComparisonArgument)

            Return arguments

        End Function

        Protected Overrides Function GetNewArgumentsForBinary(generator As SyntaxGenerator, rightNode As SyntaxNode, typeMemberAccess As SyntaxNode) As IEnumerable(Of SyntaxNode)

            Return New List(Of SyntaxNode) From
            {
                generator.Argument(rightNode.WithoutTrivia()),' Need To remove any trivia because otherwise an unexpected newline is added
                generator.Argument(typeMemberAccess)
            }

        End Function
    End Class

End Namespace