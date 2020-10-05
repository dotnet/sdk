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

                Dim importsStatement = TryCast(import, ImportsStatementSyntax)
                If importsStatement IsNot Nothing Then

                    For Each clause As ImportsClauseSyntax In importsStatement.ImportsClauses

                        Dim simpleClause = TryCast(clause, SimpleImportsClauseSyntax)
                        If simpleClause IsNot Nothing Then

                            Dim identifier = TryCast(simpleClause.Name, IdentifierNameSyntax)
                            If identifier IsNot Nothing AndAlso identifier.Identifier.Text = "System" Then
                                Return True
                            End If

                        End If
                    Next
                End If
            Next

            Return False

        End Function

        Protected Overrides Function IsPassingZeroAndBufferLength(model As SemanticModel, bufferValueNode As SyntaxNode, offsetValueNode As SyntaxNode, countValueNode As SyntaxNode) As Boolean

            ' First argument should be an identifier name node
            Dim firstArgumentIdentifierName = TryCast(bufferValueNode, IdentifierNameSyntax)
            If firstArgumentIdentifierName Is Nothing Then
                Return False
            End If

            ' Second argument should be a literal expression node with a constant value...
            Dim optionalValue As [Optional](Of Object) = model.GetConstantValue(offsetValueNode)

            ' And must be an integer...
            If Not optionalValue.HasValue Or TypeOf optionalValue.Value IsNot Integer Then
                Return False
            End If

            ' with a value of zero
            Dim value = DirectCast(optionalValue.Value, Integer)
            If value <> 0 Then
                Return False
            End If

            ' Third argument should be a member access node...
            Dim thirdArgumentMemberAccessExpression = TryCast(countValueNode, MemberAccessExpressionSyntax)
            If thirdArgumentMemberAccessExpression Is Nothing Then
                Return False
            End If

            ' whose identifier is an identifier name node, and its value is the same as the value of first argument, and the member name is `Length`
            Dim thirdArgumentIdentifierName = TryCast(thirdArgumentMemberAccessExpression.Expression, IdentifierNameSyntax)
            If thirdArgumentIdentifierName IsNot Nothing And
                thirdArgumentIdentifierName.Identifier.Text = firstArgumentIdentifierName.Identifier.Text And
                thirdArgumentMemberAccessExpression.Name.Identifier.Text = WellKnownMemberNames.LengthPropertyName Then
                Return True
            End If

            Return False

        End Function

    End Class
End Namespace
