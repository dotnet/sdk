' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferStreamAsyncMemoryOverloadsFixer

        Inherits PreferStreamAsyncMemoryOverloadsFixer

        Protected Overrides Function GetArgumentByPositionOrName(args As ImmutableArray(Of IArgumentOperation), index As Integer, name As String, ByRef isNamed As Boolean) As IArgumentOperation
            isNamed = False
            If index >= args.Length Then
                Return Nothing
            Else
                Dim argNode = TryCast(args(index).Syntax, SimpleArgumentSyntax)
                If argNode IsNot Nothing AndAlso argNode.NameColonEquals Is Nothing Then
                    Return args(index)
                Else
                    isNamed = True
                    Return args.FirstOrDefault(
                        Function(argOperation)
                            argNode = TryCast(argOperation.Syntax, SimpleArgumentSyntax)
                            Return argNode.NameColonEquals?.Name?.Identifier.ValueText = name
                        End Function)
                End If
            End If
        End Function

        Protected Overrides Function IsSystemNamespaceImported(importList As IReadOnlyList(Of SyntaxNode)) As Boolean

            For Each import As SyntaxNode In importList

                Dim importsStatement As ImportsStatementSyntax = TryCast(import, ImportsStatementSyntax)
                If importsStatement IsNot Nothing Then

                    For Each clause As ImportsClauseSyntax In importsStatement.ImportsClauses

                        Dim simpleClause As SimpleImportsClauseSyntax = TryCast(clause, SimpleImportsClauseSyntax)
                        If simpleClause IsNot Nothing Then

                            Dim identifier As IdentifierNameSyntax = TryCast(simpleClause.Name, IdentifierNameSyntax)
                            If identifier IsNot Nothing AndAlso identifier.Identifier.Text = "System" Then
                                Return True
                            End If

                        End If
                    Next
                End If
            Next

            Return False

        End Function

    End Class
End Namespace
