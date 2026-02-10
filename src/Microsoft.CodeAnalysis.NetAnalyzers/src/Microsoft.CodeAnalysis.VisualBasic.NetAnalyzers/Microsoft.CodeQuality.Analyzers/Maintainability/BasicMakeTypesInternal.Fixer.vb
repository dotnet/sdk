' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.Maintainability

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public Class BasicMakeTypesInternalFixer
        Inherits MakeTypesInternalFixer

        Protected Overrides Function MakeInternal(node As SyntaxNode) As SyntaxNode
            Dim type = TryCast(node, TypeStatementSyntax)
            If type IsNot Nothing
                Dim publicKeyword = type.Modifiers.First(Function(m) m.IsKind(SyntaxKind.PublicKeyword))
                Dim modifiers = type.Modifiers.Replace(publicKeyword, SyntaxFactory.Token(SyntaxKind.FriendKeyword))

                Return type.WithModifiers(modifiers)
            End If

            Dim enumStatement = TryCast(node, EnumStatementSyntax)
            If enumStatement IsNot Nothing
                Dim publicKeyword = enumStatement.Modifiers.First(Function(m) m.IsKind(SyntaxKind.PublicKeyword))
                Dim modifiers = enumStatement.Modifiers.Replace(publicKeyword, SyntaxFactory.Token(SyntaxKind.FriendKeyword))

                Return enumStatement.WithModifiers(modifiers)
            End If

            Dim delegateStatement = TryCast(node, DelegateStatementSyntax)
            If delegateStatement IsNot Nothing
                Dim publicKeyword = delegateStatement.Modifiers.First(Function(m) m.IsKind(SyntaxKind.PublicKeyword))
                Dim modifiers = delegateStatement.Modifiers.Replace(publicKeyword, SyntaxFactory.Token(SyntaxKind.FriendKeyword))

                Return delegateStatement.WithModifiers(modifiers)
            End If

            Throw New NotSupportedException()
        End Function
    End Class
End Namespace