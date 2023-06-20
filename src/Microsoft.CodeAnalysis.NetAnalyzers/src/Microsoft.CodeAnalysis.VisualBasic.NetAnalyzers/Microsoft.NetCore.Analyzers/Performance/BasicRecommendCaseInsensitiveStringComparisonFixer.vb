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

        Protected Overrides Function GetNewArguments(generator As SyntaxGenerator, mainInvocationOperation As IInvocationOperation,
            stringComparisonType As INamedTypeSymbol, ByRef mainInvocationInstance As SyntaxNode) As List(Of SyntaxNode)

            Dim paramName As String = RecommendCaseInsensitiveStringComparisonAnalyzer.StringParameterName

            Dim arguments As New List(Of SyntaxNode)
            Dim isAnyArgumentNamed As Boolean = False

            Dim invocationExpression As InvocationExpressionSyntax = DirectCast(mainInvocationOperation.Syntax, InvocationExpressionSyntax)

            Dim caseChangingApproachName As String = ""
            Dim isChangingCaseInArgument As Boolean = False

            Dim memberAccessExpression As MemberAccessExpressionSyntax = TryCast(invocationExpression.Expression, MemberAccessExpressionSyntax)

            If memberAccessExpression IsNot Nothing Then

                Dim internalExpression As ExpressionSyntax

                Dim parenthesizedExpression As ParenthesizedExpressionSyntax = TryCast(memberAccessExpression.Expression, ParenthesizedExpressionSyntax)

                internalExpression = If(parenthesizedExpression IsNot Nothing,
                    parenthesizedExpression.Expression,
                    memberAccessExpression.Expression)

                Dim internalInvocationExpression As InvocationExpressionSyntax = TryCast(internalExpression, InvocationExpressionSyntax)
                Dim internalMemberAccessExpression As MemberAccessExpressionSyntax = Nothing

                If internalInvocationExpression IsNot Nothing Then
                    internalMemberAccessExpression = TryCast(internalInvocationExpression.Expression, MemberAccessExpressionSyntax)
                End If

                If internalMemberAccessExpression IsNot Nothing Then
                    mainInvocationInstance = internalMemberAccessExpression.Expression
                    caseChangingApproachName = GetCaseChangingApproach(internalMemberAccessExpression.Name.Identifier.ValueText)
                Else
                    mainInvocationInstance = memberAccessExpression.Expression
                    isChangingCaseInArgument = True
                End If

            End If

            For Each node As SimpleArgumentSyntax In invocationExpression.ArgumentList.Arguments

                Dim argumentName As String = node.NameColonEquals?.Name.Identifier.ValueText
                isAnyArgumentNamed = isAnyArgumentNamed Or argumentName IsNot Nothing

                Dim argumentParenthesizedExpression As ParenthesizedExpressionSyntax = TryCast(node.Expression, ParenthesizedExpressionSyntax)

                Dim argumentExpression As ExpressionSyntax = If(argumentParenthesizedExpression IsNot Nothing,
                    argumentParenthesizedExpression.Expression,
                    node.Expression)

                Dim argumentMemberAccessExpression As MemberAccessExpressionSyntax = Nothing
                Dim argumentInvocationExpression As InvocationExpressionSyntax = TryCast(argumentExpression, InvocationExpressionSyntax)

                If argumentInvocationExpression IsNot Nothing Then
                    argumentMemberAccessExpression = TryCast(argumentInvocationExpression.Expression, MemberAccessExpressionSyntax)
                    If argumentMemberAccessExpression IsNot Nothing Then
                        caseChangingApproachName = GetCaseChangingApproach(argumentMemberAccessExpression.Name.Identifier.ValueText)
                    End If
                End If

                Dim newArgumentNode As SyntaxNode
                If isChangingCaseInArgument Then
                    If argumentMemberAccessExpression IsNot Nothing Then

                        newArgumentNode = If(argumentName = paramName,
                            generator.Argument(paramName, RefKind.None, argumentMemberAccessExpression.Expression),
                            generator.Argument(argumentMemberAccessExpression.Expression))

                    Else

                        newArgumentNode = node

                    End If
                Else

                    newArgumentNode = node

                End If

                arguments.Add(newArgumentNode)

            Next

            Debug.Assert(caseChangingApproachName IsNot Nothing)
            Debug.Assert(mainInvocationInstance IsNot Nothing)

            Dim stringComparisonArgument As SyntaxNode = GetNewStringComparisonArgument(generator, stringComparisonType, caseChangingApproachName, isAnyArgumentNamed)

            arguments.Add(stringComparisonArgument)

            Return arguments

        End Function

    End Class

End Namespace