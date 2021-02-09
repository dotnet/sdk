' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferAsSpanOverSubstringFixer : Inherits PreferAsSpanOverSubstringFixer

        Private Protected Overrides Sub ReplaceInvocationMethodName(editor As SyntaxEditor, memberInvocation As SyntaxNode, newName As String)

            Dim cast = DirectCast(memberInvocation, InvocationExpressionSyntax)
            Dim memberAccessSyntax = DirectCast(cast.Expression, MemberAccessExpressionSyntax)
            Dim newNameSyntax = SyntaxFactory.IdentifierName(newName)
            editor.ReplaceNode(memberAccessSyntax.Name, newNameSyntax)
        End Sub

        Private Protected Overrides Sub ReplaceNamedArgumentName(editor As SyntaxEditor, invocation As SyntaxNode, oldArgumentName As String, newArgumentName As String)

            Dim cast = DirectCast(invocation, InvocationExpressionSyntax)
            Dim argumentToReplace = cast.ArgumentList.Arguments.FirstOrDefault(
                Function(x)
                    If Not x.IsNamed Then Return False
                    Dim simpleArgumentSyntax = TryCast(x, SimpleArgumentSyntax)
                    If simpleArgumentSyntax Is Nothing Then Return False
                    Return simpleArgumentSyntax.NameColonEquals.Name.Identifier.ValueText = oldArgumentName
                End Function)
            If argumentToReplace Is Nothing Then Return
            Dim oldNameSyntax = DirectCast(argumentToReplace, SimpleArgumentSyntax).NameColonEquals.Name
            Dim newNameSyntax = SyntaxFactory.IdentifierName(newArgumentName)
            editor.ReplaceNode(oldNameSyntax, newNameSyntax)
        End Sub

        Private Protected Overrides Function IsNamespaceImported(editor As DocumentEditor, namespaceName As String) As Boolean

            Dim options = TryCast(editor.OriginalDocument.Project.CompilationOptions, VisualBasicCompilationOptions)
            If options IsNot Nothing AndAlso options.GlobalImports.Any(Function(x) x.Name = namespaceName) Then
                Return True
            End If

            Dim clauses = editor.Generator.GetNamespaceImports(editor.OriginalRoot).SelectMany(
                Function(x)
                    Dim importsStatement = TryCast(x, ImportsStatementSyntax)
                    If importsStatement Is Nothing Then Return Enumerable.Empty(Of SimpleImportsClauseSyntax)
                    Return importsStatement.ImportsClauses.OfType(Of SimpleImportsClauseSyntax)
                End Function)
            Return clauses.Any(Function(x) x.Name.ToString() = namespaceName)
        End Function
    End Class
End Namespace
