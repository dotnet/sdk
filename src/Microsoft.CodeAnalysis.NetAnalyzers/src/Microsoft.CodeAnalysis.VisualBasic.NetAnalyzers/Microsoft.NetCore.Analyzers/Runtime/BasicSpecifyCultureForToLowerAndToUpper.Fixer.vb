' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public Class BasicSpecifyCultureForToLowerAndToUpperFixer
        Inherits SpecifyCultureForToLowerAndToUpperFixerBase

        Protected Overrides Function ShouldFix(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.IdentifierName) AndAlso
                Nullable.Equals(node.Parent?.IsKind(SyntaxKind.SimpleMemberAccessExpression), True)
        End Function

        Protected Overrides Function GetNodeToSpecifyCurrentCultureOn(node As SyntaxNode, model As SemanticModel, cancellationToken As CancellationToken) As SyntaxNode
            If Not ShouldFix(node) Then
                Return Nothing
            End If

            Dim memberAccess = DirectCast(node.Parent, MemberAccessExpressionSyntax)

            If memberAccess.Parent Is Nothing OrElse Not memberAccess.Parent.IsKind(SyntaxKind.InvocationExpression) Then
                Return memberAccess
            End If

            Dim invocation = DirectCast(memberAccess.Parent, InvocationExpressionSyntax)
            If invocation.ArgumentList Is Nothing Then
                Return invocation
            End If

            Dim methodSymbol = TryCast(model.GetSymbolInfo(node, cancellationToken).Symbol, IMethodSymbol)
            Return If(methodSymbol IsNot Nothing AndAlso methodSymbol.Parameters.Length = 0, invocation, Nothing)
        End Function

        Protected Overrides Function SpecifyCurrentCulture(currentNode As SyntaxNode, currentCultureArgument As SyntaxNode, generator As SyntaxGenerator) As SyntaxNode
            Dim argument = currentCultureArgument.WithAdditionalAnnotations(Formatter.Annotation)
            Dim invocation = TryCast(currentNode, InvocationExpressionSyntax)

            If invocation IsNot Nothing AndAlso invocation.ArgumentList IsNot Nothing Then
                Return invocation.AddArgumentListArguments(DirectCast(argument, ArgumentSyntax)).WithAdditionalAnnotations(Formatter.Annotation)
            End If

            Dim target = If(invocation IsNot Nothing, invocation.Expression, currentNode)
            Return generator.InvocationExpression(target.WithoutTrailingTrivia(), argument).WithAdditionalAnnotations(Formatter.Annotation)
        End Function

        Protected Overrides Function GetMemberAccessToMakeInvariant(node As SyntaxNode) As SyntaxNode
            Return If(ShouldFix(node), node.Parent, Nothing)
        End Function

        Protected Overrides Function UseInvariantVersion(currentMemberAccess As SyntaxNode, generator As SyntaxGenerator) As SyntaxNode
            Dim memberAccess = DirectCast(currentMemberAccess, MemberAccessExpressionSyntax)
            Dim replacementMethodName = GetReplacementMethodName(memberAccess.Name.Identifier.Text)
            Return memberAccess.WithName(DirectCast(generator.IdentifierName(replacementMethodName), SimpleNameSyntax)).WithAdditionalAnnotations(Formatter.Annotation)
        End Function
    End Class
End Namespace
