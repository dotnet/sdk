' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicPreferDictionaryContainsMethodsFixer : Inherits PreferDictionaryContainsMethodsFixer

        Protected Overrides Function GetPropertyName(invocation As SyntaxNode) As String
            Dim keysOrValuesMember = GetKeysOrValuesMemberAccess(invocation)
            If keysOrValuesMember Is Nothing Then
                Return Nothing
            End If

            Return keysOrValuesMember.Name.Identifier.ValueText
        End Function

        Protected Overrides Function Rewrite(invocation As SyntaxNode, methodName As String, generator As SyntaxGenerator) As SyntaxNode
            Dim keysOrValuesMember = GetKeysOrValuesMemberAccess(invocation)
            If keysOrValuesMember Is Nothing Then
                Return Nothing
            End If

            Dim containsMemberExpression = generator.MemberAccessExpression(keysOrValuesMember.Expression, methodName)
            Return generator.InvocationExpression(containsMemberExpression, DirectCast(invocation, InvocationExpressionSyntax).ArgumentList.Arguments)
        End Function

        Private Shared Function GetKeysOrValuesMemberAccess(node As SyntaxNode) As MemberAccessExpressionSyntax
            Dim invocation = TryCast(node, InvocationExpressionSyntax)
            If invocation Is Nothing Then
                Return Nothing
            End If

            Dim containsMemberAccess = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
            If containsMemberAccess Is Nothing Then
                Return Nothing
            End If

            Return TryCast(containsMemberAccess.Expression, MemberAccessExpressionSyntax)
        End Function
    End Class
End Namespace
