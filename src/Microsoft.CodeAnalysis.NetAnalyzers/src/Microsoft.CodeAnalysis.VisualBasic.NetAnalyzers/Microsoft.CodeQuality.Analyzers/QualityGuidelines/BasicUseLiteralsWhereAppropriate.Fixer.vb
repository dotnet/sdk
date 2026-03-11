' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    ''' <summary>
    ''' CA1802: Use literals where appropriate
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicUseLiteralsWhereAppropriateFixer
        Inherits UseLiteralsWhereAppropriateFixer

        Protected Overrides Function GetFieldDeclaration(syntaxNode As SyntaxNode) As SyntaxNode
            While syntaxNode IsNot Nothing AndAlso Not (TypeOf syntaxNode Is FieldDeclarationSyntax)
                syntaxNode = syntaxNode.Parent
            End While

            Dim field = DirectCast(syntaxNode, FieldDeclarationSyntax)

            ' Multiple declarators are not supported, as one of them may not be constant.
            Return If(field IsNot Nothing AndAlso field.Declarators.Count > 1, Nothing, field)
        End Function

        Protected Overrides Function IsStaticKeyword(syntaxToken As SyntaxToken) As Boolean
            Return syntaxToken.IsKind(SyntaxKind.SharedKeyword)
        End Function

        Protected Overrides Function IsReadonlyKeyword(syntaxToken As SyntaxToken) As Boolean
            Return syntaxToken.IsKind(SyntaxKind.ReadOnlyKeyword)
        End Function

        Protected Overrides Function GetConstKeywordToken() As SyntaxToken
            Return SyntaxFactory.Token(SyntaxKind.ConstKeyword)
        End Function

        Protected Overrides Function GetModifiers(fieldSyntax As SyntaxNode) As SyntaxTokenList
            Dim field = DirectCast(fieldSyntax, FieldDeclarationSyntax)
            Return field.Modifiers
        End Function

        Protected Overrides Function WithModifiers(fieldSyntax As SyntaxNode, modifiers As SyntaxTokenList) As SyntaxNode
            Dim field = DirectCast(fieldSyntax, FieldDeclarationSyntax)
            Return field.WithModifiers(modifiers)
        End Function
    End Class
End Namespace
