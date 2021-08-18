' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports System.Threading
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.NetCore.Analyzers

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferDictionaryContainsMethodsFixer : Inherits PreferDictionaryContainsMethodsFixer

        Public Overrides Async Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim doc = context.Document
            Dim root = Await doc.GetSyntaxRootAsync().ConfigureAwait(False)

            Dim invocation = TryCast(root.FindNode(context.Span), InvocationExpressionSyntax)
            If invocation Is Nothing Then
                Return
            End If

            Dim containsMemberAccess = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
            If containsMemberAccess Is Nothing Then
                Return
            End If

            Dim keysOrValuesMember = TryCast(containsMemberAccess.Expression, MemberAccessExpressionSyntax)
            If keysOrValuesMember Is Nothing Then
                Return
            End If

            If keysOrValuesMember.Name.Identifier.ValueText = PreferDictionaryContainsMethods.KeysPropertyName Then
                Dim ReplaceWithContainsKey =
                    Async Function(ct As CancellationToken) As Task(Of Document)
                        Dim editor = Await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(False)
                        Dim containsKeyMemberExpression = editor.Generator.MemberAccessExpression(keysOrValuesMember.Expression, PreferDictionaryContainsMethods.ContainsKeyMethodName)
                        Dim newInvocation = editor.Generator.InvocationExpression(containsKeyMemberExpression, invocation.ArgumentList.Arguments)
                        editor.ReplaceNode(invocation, newInvocation)

                        Return editor.GetChangedDocument()
                    End Function
                Dim codeFixTitle = MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyCodeFixTitle
                Dim action = CodeAction.Create(codeFixTitle, ReplaceWithContainsKey, codeFixTitle)
                context.RegisterCodeFix(action, context.Diagnostics)

            ElseIf keysOrValuesMember.Name.Identifier.ValueText = PreferDictionaryContainsMethods.ValuesPropertyName Then
                Dim ReplaceWithContainsValue =
                    Async Function(ct As CancellationToken) As Task(Of Document)
                        Dim editor = Await DocumentEditor.CreateAsync(doc, ct).ConfigureAwait(False)
                        Dim containsValueMemberExpression = editor.Generator.MemberAccessExpression(keysOrValuesMember.Expression, PreferDictionaryContainsMethods.ContainsValueMethodName)
                        Dim newInvocation = editor.Generator.InvocationExpression(containsValueMemberExpression, invocation.ArgumentList.Arguments)
                        editor.ReplaceNode(invocation, newInvocation)

                        Return editor.GetChangedDocument()
                    End Function
                Dim codeFixTitle = MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueCodeFixTitle
                Dim action = CodeAction.Create(codeFixTitle, ReplaceWithContainsValue, codeFixTitle)
                context.RegisterCodeFix(action, context.Diagnostics)
            End If
        End Function
    End Class
End Namespace
