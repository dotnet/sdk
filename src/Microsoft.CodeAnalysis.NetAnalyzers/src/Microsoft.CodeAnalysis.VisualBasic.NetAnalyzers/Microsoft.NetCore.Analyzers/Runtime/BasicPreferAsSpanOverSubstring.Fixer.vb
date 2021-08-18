' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferAsSpanOverSubstringFixer : Inherits PreferAsSpanOverSubstringFixer

        Private Protected Overrides Sub ReplaceNonConditionalInvocationMethodName(editor As SyntaxEditor, memberInvocation As SyntaxNode, newName As String)

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
    End Class
End Namespace
