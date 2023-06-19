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

        Protected Overrides Async Function SpecifyCurrentCultureAsync(document As Document, generator As SyntaxGenerator, root As SyntaxNode, node As SyntaxNode, cancellationToken As CancellationToken) As Task(Of Document)
            If ShouldFix(node) Then
                Dim memberAccess = DirectCast(node.Parent, MemberAccessExpressionSyntax)

                If memberAccess.Parent Is Nothing OrElse Not memberAccess.Parent.IsKind(SyntaxKind.InvocationExpression) Then
                    Return Await SpecifyCurrentCultureWhenTheresNoArgumentListAsync(document, generator, root, memberAccess, memberAccess, cancellationToken).ConfigureAwait(False)
                End If

                Dim invocation = DirectCast(memberAccess.Parent, InvocationExpressionSyntax)
                If invocation.ArgumentList Is Nothing Then
                    Return Await SpecifyCurrentCultureWhenTheresNoArgumentListAsync(document, generator, root, memberAccess, invocation, cancellationToken).ConfigureAwait(False)
                End If

                Dim model = Await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(False)
                Dim symbolInfo = model.GetSymbolInfo(node, cancellationToken).Symbol
                Dim methodSymbol = TryCast(symbolInfo, IMethodSymbol)

                If methodSymbol IsNot Nothing And methodSymbol.Parameters.Length = 0 Then
                    Dim newArg = generator.Argument(CreateCurrentCultureMemberAccess(generator, model)).WithAdditionalAnnotations(Formatter.Annotation)
                    Dim newInvocation = invocation.AddArgumentListArguments(DirectCast(newArg, ArgumentSyntax)).WithAdditionalAnnotations(Formatter.Annotation)
                    Dim newRoot = root.ReplaceNode(invocation, newInvocation)
                    Return document.WithSyntaxRoot(newRoot)
                End If
            End If

            Return document
        End Function

        Private Shared Async Function SpecifyCurrentCultureWhenTheresNoArgumentListAsync(document As Document, generator As SyntaxGenerator, root As SyntaxNode, memberAccess As MemberAccessExpressionSyntax, nodeToReplace As SyntaxNode, cancellationToken As CancellationToken) As Task(Of Document)
            Dim model = Await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(False)
            Dim newArg = generator.Argument(CreateCurrentCultureMemberAccess(generator, model)).WithAdditionalAnnotations(Formatter.Annotation)
            Dim invocation = generator.InvocationExpression(memberAccess.WithoutTrailingTrivia(), newArg).WithAdditionalAnnotations(Formatter.Annotation)
            Dim newRoot = root.ReplaceNode(nodeToReplace, invocation)
            Return document.WithSyntaxRoot(newRoot)
        End Function

        Protected Overrides Function UseInvariantVersionAsync(document As Document, generator As SyntaxGenerator, root As SyntaxNode, node As SyntaxNode) As Task(Of Document)
            If ShouldFix(node) Then
                Dim memberAccess = DirectCast(node.Parent, MemberAccessExpressionSyntax)
                Dim replacementMethodName = GetReplacementMethodName(memberAccess.Name.Identifier.Text)
                Dim newMemberAccess = memberAccess.WithName(DirectCast(generator.IdentifierName(replacementMethodName), SimpleNameSyntax)).WithAdditionalAnnotations(Formatter.Annotation)
                Dim newRoot = root.ReplaceNode(memberAccess, newMemberAccess)
                Return Task.FromResult(document.WithSyntaxRoot(newRoot))
            End If

            Return Task.FromResult(document)
        End Function
    End Class
End Namespace
