' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicUseSpanBasedStringConcatFixer : Inherits UseSpanBasedStringConcatFixer

        Private Protected Overrides Function ReplaceInvocationMethodName(generator As SyntaxGenerator, invocationSyntax As SyntaxNode, newName As String) As SyntaxNode

            Dim cast = DirectCast(invocationSyntax, InvocationExpressionSyntax)
            Dim memberAccessSyntax = DirectCast(cast.Expression, MemberAccessExpressionSyntax)
            Dim oldNameSyntax = memberAccessSyntax.Name
            Dim newNameSyntax = generator.IdentifierName(newName).WithTriviaFrom(oldNameSyntax)
            Return invocationSyntax.ReplaceNode(oldNameSyntax, newNameSyntax)
        End Function

        Private Protected Overrides Function GetOperatorToken(binaryOperation As IBinaryOperation) As SyntaxToken

            Dim syntax = DirectCast(binaryOperation.Syntax, BinaryExpressionSyntax)
            Return syntax.OperatorToken
        End Function

        Private Protected Overrides Function IsSystemNamespaceImported(namespaceImports As IReadOnlyList(Of SyntaxNode)) As Boolean

            For Each node As SyntaxNode In namespaceImports
                Dim importsStatement = TryCast(node, ImportsStatementSyntax)
                If importsStatement Is Nothing Then
                    Continue For
                End If
                For Each importsClause As ImportsClauseSyntax In importsStatement.ImportsClauses
                    Dim simpleClause = TryCast(importsClause, SimpleImportsClauseSyntax)
                    Dim identifierName = TryCast(simpleClause?.Name, IdentifierNameSyntax)
                    If identifierName Is Nothing Then
                        Continue For
                    End If
                    If identifierName.Identifier.ValueText = NameOf(System) Then
                        Return True
                    End If
                Next
            Next
            Return False
        End Function

        Private Protected Overrides Function IsNamedArgument(argument As IArgumentOperation) As Boolean
            Return DirectCast(argument.Syntax, ArgumentSyntax).IsNamed
        End Function

        Private Protected Overrides Function CreateConditionalToStringInvocation(receiverExpression As SyntaxNode) As SyntaxNode

            Dim expression = DirectCast(receiverExpression, ExpressionSyntax)
            Dim memberAccessExpression = SyntaxFactory.SimpleMemberAccessExpression(SyntaxFactory.IdentifierName(ToStringName))
            Dim invocationExpression = SyntaxFactory.InvocationExpression(memberAccessExpression)
            Return SyntaxFactory.ConditionalAccessExpression(expression.WithoutTrivia(), invocationExpression).WithTriviaFrom(expression)
        End Function
    End Class
End Namespace

