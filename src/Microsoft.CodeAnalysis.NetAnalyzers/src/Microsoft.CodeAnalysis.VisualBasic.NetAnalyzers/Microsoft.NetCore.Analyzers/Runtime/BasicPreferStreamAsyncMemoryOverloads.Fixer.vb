' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferStreamAsyncMemoryOverloadsFixer

        Inherits PreferStreamAsyncMemoryOverloadsFixer

        Protected Overrides Function GetArgumentByPositionOrName(invocation As IInvocationOperation, index As Integer, name As String, ByRef isNamed As Boolean) As SyntaxNode
            isNamed = False
            If index < invocation.Arguments.Length Then
                Dim args = invocation.Arguments
                Dim argNode = TryCast(args(index).Syntax, SimpleArgumentSyntax)
                If argNode IsNot Nothing AndAlso argNode.NameColonEquals Is Nothing Then
                    'If the argument in the specified index does not have a name, then it is in its expected position
                    Return args(index).Syntax
                Else
                    'Otherwise, find it by name
                    Dim operation = args.FirstOrDefault(
                        Function(argOperation)
                            argNode = TryCast(argOperation.Syntax, SimpleArgumentSyntax)
                            Return String.Equals(argNode.NameColonEquals?.Name?.Identifier.ValueText, name, StringComparison.OrdinalIgnoreCase)
                        End Function)
                    If operation IsNot Nothing Then
                        isNamed = True
                        Return operation.Syntax
                    End If
                End If
            End If
            Return Nothing
        End Function

        Protected Overrides Function IsSystemNamespaceImported(importList As IReadOnlyList(Of SyntaxNode)) As Boolean
            For Each import As SyntaxNode In importList
                Dim importsStatement = TryCast(import, ImportsStatementSyntax)
                If importsStatement IsNot Nothing Then
                    For Each clause As ImportsClauseSyntax In importsStatement.ImportsClauses
                        Dim simpleClause = TryCast(clause, SimpleImportsClauseSyntax)
                        If simpleClause IsNot Nothing Then
                            Dim identifier = TryCast(simpleClause.Name, IdentifierNameSyntax)
                            If identifier IsNot Nothing AndAlso String.Equals(identifier.Identifier.Text, "System", StringComparison.OrdinalIgnoreCase) Then
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
            Dim arg1 = TryCast(bufferValueNode, ArgumentSyntax)
            If arg1 Is Nothing Then
                Return False
            End If
            Dim firstArgumentIdentifierName = TryCast(arg1.GetExpression(), IdentifierNameSyntax)
            If firstArgumentIdentifierName Is Nothing Then
                Return False
            End If
            ' Second argument should be a literal expression node with a constant value...
            Dim arg2 = TryCast(offsetValueNode, ArgumentSyntax)
            If arg2 Is Nothing Then
                Return False
            End If
            Dim literal = TryCast(arg2.GetExpression(), LiteralExpressionSyntax)
            If literal Is Nothing Then
                Return False
            End If
            ' And must be an integer...
            If TypeOf literal.Token.Value IsNot Integer Then
                Return False
            End If
            ' with a value of zero
            Dim value = DirectCast(literal.Token.Value, Integer)
            If value <> 0 Then
                Return False
            End If
            ' Third argument should be a member access node...
            Dim arg3 = TryCast(countValueNode, ArgumentSyntax)
            If arg3 Is Nothing Then
                Return False
            End If
            Dim thirdArgumentMemberAccessExpression = TryCast(arg3.GetExpression(), MemberAccessExpressionSyntax)
            If thirdArgumentMemberAccessExpression Is Nothing Then
                Return False
            End If
            ' whose identifier is an identifier name node, and its value is the same as the value of first argument, and the member name is `Length`
            Dim thirdArgumentIdentifierName = TryCast(thirdArgumentMemberAccessExpression.Expression, IdentifierNameSyntax)
            If thirdArgumentIdentifierName IsNot Nothing And
                String.Equals(thirdArgumentIdentifierName.Identifier.Text, firstArgumentIdentifierName.Identifier.Text, StringComparison.OrdinalIgnoreCase) And
                String.Equals(thirdArgumentMemberAccessExpression.Name.Identifier.Text, WellKnownMemberNames.LengthPropertyName, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
            Return False
        End Function

        Protected Overrides Function GetNodeWithNullability(invocation As IInvocationOperation) As SyntaxNode
            ' VB does not have nullability, return the syntax node untouched
            Return invocation.Instance.Syntax
        End Function

        Protected Overrides Function GetNamedArgument(generator As SyntaxGenerator, node As SyntaxNode, isNamed As Boolean, newName As String) As SyntaxNode
            If isNamed Then
                Dim actualNode = node
                Dim argument = TryCast(node, ArgumentSyntax)
                If argument IsNot Nothing Then
                    actualNode = argument.GetExpression()
                End If
                Return generator.Argument(newName, RefKind.None, actualNode)
            End If
            Return node
        End Function

        Protected Overrides Function GetNamedMemberInvocation(generator As SyntaxGenerator, node As SyntaxNode, memberName As String) As SyntaxNode
            Dim actualNode = node
            Dim argument = TryCast(node, ArgumentSyntax)
            If argument IsNot Nothing Then
                actualNode = argument.GetExpression()
            End If
            Return generator.MemberAccessExpression(actualNode.WithoutTrivia(), memberName)
        End Function
    End Class

End Namespace
