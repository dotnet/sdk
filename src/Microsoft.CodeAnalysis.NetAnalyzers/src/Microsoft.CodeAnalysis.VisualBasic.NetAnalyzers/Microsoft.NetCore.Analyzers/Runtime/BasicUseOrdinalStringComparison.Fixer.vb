' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.Runtime
Imports System.Threading
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

        Protected Overrides Function FixArgumentAsync(document As Document, generator As SyntaxGenerator, root As SyntaxNode, argument As SyntaxNode) As Task(Of Document)
            Dim memberAccess = TryCast(TryCast(argument, SimpleArgumentSyntax)?.Expression, MemberAccessExpressionSyntax)
            If memberAccess IsNot Nothing Then
                ' preserve the "IgnoreCase" suffix if present
                Dim isIgnoreCase = memberAccess.Name.GetText().ToString().EndsWith(UseOrdinalStringComparisonAnalyzer.IgnoreCaseText, StringComparison.Ordinal)
                Dim newOrdinalText = If(isIgnoreCase, UseOrdinalStringComparisonAnalyzer.OrdinalIgnoreCaseText, UseOrdinalStringComparisonAnalyzer.OrdinalText)
                Dim newIdentifier = generator.IdentifierName(newOrdinalText)
                Dim newMemberAccess = memberAccess.WithName(CType(newIdentifier, SimpleNameSyntax)).WithAdditionalAnnotations(Formatter.Annotation)
                Dim newRoot = root.ReplaceNode(memberAccess, newMemberAccess)
                Return Task.FromResult(document.WithSyntaxRoot(newRoot))
            End If
            Return Task.FromResult(document)
        End Function

        Protected Overrides Function IsInIdentifierNameContext(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.IdentifierName) AndAlso
                   node?.Parent?.FirstAncestorOrSelf(Of InvocationExpressionSyntax)() IsNot Nothing
        End Function

        Protected Overrides Async Function FixIdentifierNameAsync(document As Document, generator As SyntaxGenerator, root As SyntaxNode, identifier As SyntaxNode, cancellationToken As CancellationToken) As Task(Of Document)
            Dim invokeParent = identifier.Parent?.FirstAncestorOrSelf(Of InvocationExpressionSyntax)()
            If invokeParent IsNot Nothing Then
                Dim model = Await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(False)
                Dim methodSymbol = TryCast(model.GetSymbolInfo(identifier, cancellationToken).Symbol, IMethodSymbol)
                If methodSymbol IsNot Nothing AndAlso CanAddStringComparison(methodSymbol, model) Then
                    ' append a New StringComparison.Ordinal argument
                    Dim newArg = generator.Argument(CreateOrdinalMemberAccess(generator, model)).
                                WithAdditionalAnnotations(Formatter.Annotation)
                    Dim newInvoke = invokeParent.AddArgumentListArguments(CType(newArg, ArgumentSyntax)).WithAdditionalAnnotations(Formatter.Annotation)
                    Dim newRoot = root.ReplaceNode(invokeParent, newInvoke)
                    Return document.WithSyntaxRoot(newRoot)
                End If
            End If
            Return document
        End Function
    End Class
End Namespace
