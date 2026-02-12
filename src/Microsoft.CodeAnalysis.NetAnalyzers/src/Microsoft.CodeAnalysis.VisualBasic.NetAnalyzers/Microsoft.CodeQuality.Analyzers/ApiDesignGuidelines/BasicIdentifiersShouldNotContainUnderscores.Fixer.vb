' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ''' <summary>
    ''' CA1707: Identifiers should not contain underscores
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicIdentifiersShouldNotContainUnderscoresFixer
        Inherits IdentifiersShouldNotContainUnderscoresFixer

        Protected Overrides Function GetNewName(name As String) As String
            Dim result = RemoveUnderscores(name)
            If result.Length = 0 Then
                Return String.Empty
            End If

            If Not SyntaxFacts.IsValidIdentifier(result) Then
                Return $"[{result}]"
            End If

            Return result
        End Function

        Protected Overrides Function GetDeclarationNode(node As SyntaxNode) As SyntaxNode
            If node.IsKind(SyntaxKind.IdentifierName) Then
                Return node.Parent
            Else
                Return node
            End If
        End Function
    End Class
End Namespace
