' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public Class BasicUseOrdinalStringComparisonFixer
        Inherits UseOrdinalStringComparisonFixerBase

        Protected Overrides Function IsInArgumentContext(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.SimpleArgument) AndAlso
                   Not DirectCast(node, SimpleArgumentSyntax).IsNamed AndAlso
                   DirectCast(node, SimpleArgumentSyntax).Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression)
        End Function

        Protected Overrides Sub FixArgument(argument As SyntaxNode, editor As SyntaxEditor)
            Dim memberAccess = TryCast(DirectCast(argument, SimpleArgumentSyntax).Expression, MemberAccessExpressionSyntax)
            If memberAccess Is Nothing Then
                Return
            End If

            ' preserve the "IgnoreCase" suffix if present
            Dim isIgnoreCase = memberAccess.Name.GetText().ToString().EndsWith(UseOrdinalStringComparisonAnalyzer.IgnoreCaseText, StringComparison.Ordinal)
            Dim newOrdinalText = If(isIgnoreCase, UseOrdinalStringComparisonAnalyzer.OrdinalIgnoreCaseText, UseOrdinalStringComparisonAnalyzer.OrdinalText)

            editor.ReplaceNode(
                memberAccess,
                Function(currentMemberAccess, generator) DirectCast(currentMemberAccess, MemberAccessExpressionSyntax).
                    WithName(CType(generator.IdentifierName(newOrdinalText), SimpleNameSyntax)).
                    WithAdditionalAnnotations(Formatter.Annotation))
        End Sub

        Protected Overrides Function IsInIdentifierNameContext(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.IdentifierName) AndAlso
                   GetInvocation(node) IsNot Nothing
        End Function

        Protected Overrides Function GetInvocation(identifier As SyntaxNode) As SyntaxNode
            Return identifier.Parent?.FirstAncestorOrSelf(Of InvocationExpressionSyntax)()
        End Function

        Protected Overrides Function AddArgument(invocation As SyntaxNode, argument As SyntaxNode) As SyntaxNode
            Return DirectCast(invocation, InvocationExpressionSyntax).
                AddArgumentListArguments(CType(argument, ArgumentSyntax)).
                WithAdditionalAnnotations(Formatter.Annotation)
        End Function
    End Class
End Namespace
