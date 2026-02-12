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

        Protected Overrides Function AppendElasticMarker(replacement As SyntaxNode) As SyntaxNode
            Return replacement.WithTrailingTrivia(SyntaxFactory.ElasticMarker)
        End Function

        Protected Overrides Function HandleCharStringComparisonOverload(generator As SyntaxGenerator, instance As SyntaxNode, arguments As SyntaxNode(), shouldNegate As Boolean) As SyntaxNode
            Dim charArgumentSyntax = DirectCast(arguments(0), SimpleArgumentSyntax)
            If charArgumentSyntax.Expression.IsKind(SyntaxKind.CharacterLiteralExpression) Then
                ' For 'x.IndexOf(hardCodedConstantChar, stringComparison) == 0', switch to x.StartsWith(hardCodedString, stringComparison)
                Dim charValueAsString = DirectCast(charArgumentSyntax.Expression, LiteralExpressionSyntax).Token.Value.ToString()
                arguments(0) = charArgumentSyntax.WithExpression(DirectCast(generator.LiteralExpression(charValueAsString), ExpressionSyntax))
            Else
                ' The character isn't a hard-coded constant, it's some expression. We call `.ToString()` on it.
                arguments(0) = charArgumentSyntax.WithExpression(DirectCast(generator.InvocationExpression(generator.MemberAccessExpression(charArgumentSyntax.Expression, "ToString")), ExpressionSyntax))
            End If

            Return CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate)
        End Function
    End Class
End Namespace
