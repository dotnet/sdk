' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance

    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicUseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix
        Inherits UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix

        Protected Overrides Function GetIndexOfInvocation(comparison As SyntaxNode) As SyntaxNode
            Dim binaryExpression = TryCast(comparison, BinaryExpressionSyntax)
            If binaryExpression Is Nothing Then
                Return Nothing
            End If

            Dim invocation = If(TryCast(binaryExpression.Left, InvocationExpressionSyntax), TryCast(binaryExpression.Right, InvocationExpressionSyntax))
            If invocation Is Nothing Then
                Return Nothing
            End If

            ' Every overload the fix handles is called with simple arguments; anything else is declined.
            For Each argument In invocation.ArgumentList.Arguments
                If TryCast(argument, SimpleArgumentSyntax) Is Nothing Then
                    Return Nothing
                End If
            Next

            Return invocation
        End Function

        Protected Overrides Function GetInstance(invocation As SyntaxNode) As SyntaxNode
            Return DirectCast(DirectCast(invocation, InvocationExpressionSyntax).Expression, MemberAccessExpressionSyntax).Expression
        End Function

        Protected Overrides Function GetArguments(invocation As SyntaxNode) As SyntaxNode()
            Return DirectCast(invocation, InvocationExpressionSyntax).ArgumentList.Arguments.ToArray()
        End Function

        Protected Overrides Function GetArgumentExpression(argument As SyntaxNode) As SyntaxNode
            Return DirectCast(argument, SimpleArgumentSyntax).Expression
        End Function

        Protected Overrides Function AppendElasticMarker(replacement As SyntaxNode) As SyntaxNode
            Return replacement.WithTrailingTrivia(SyntaxFactory.ElasticMarker)
        End Function

        Protected Overrides Function HandleCharStringComparisonOverload(generator As SyntaxGenerator, instance As SyntaxNode, arguments As SyntaxNode(), shouldNegate As Boolean) As SyntaxNode
            Dim index = GetCharacterArgumentIndex(arguments)
            Dim charArgumentSyntax = DirectCast(arguments(index), SimpleArgumentSyntax)
            If charArgumentSyntax.Expression.IsKind(SyntaxKind.CharacterLiteralExpression) Then
                ' For 'x.IndexOf(hardCodedConstantChar, stringComparison) == 0', switch to x.StartsWith(hardCodedString, stringComparison)
                Dim charValueAsString = DirectCast(charArgumentSyntax.Expression, LiteralExpressionSyntax).Token.Value.ToString()
                arguments(index) = charArgumentSyntax.WithExpression(DirectCast(generator.LiteralExpression(charValueAsString), ExpressionSyntax))
            Else
                ' The character isn't a hard-coded constant, it's some expression. We call `.ToString()` on it.
                arguments(index) = charArgumentSyntax.WithExpression(DirectCast(generator.InvocationExpression(generator.MemberAccessExpression(charArgumentSyntax.Expression, "ToString")), ExpressionSyntax))
            End If

            Return CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate)
        End Function

        Private Shared Function GetCharacterArgumentIndex(arguments As SyntaxNode()) As Integer
            Dim firstArgument = DirectCast(arguments(0), SimpleArgumentSyntax)
            If firstArgument.NameColonEquals Is Nothing OrElse firstArgument.NameColonEquals.Name.Identifier.ValueText = "value" Then
                Return 0
            End If

            Return 1
        End Function
    End Class
End Namespace
