' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.InteropServices
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.InteropServices
    <ExportCodeFixProvider(LanguageNames.CSharp, Name:=PInvokeDiagnosticAnalyzer.RuleCA2101Id), [Shared]>
    Public Class BasicSpecifyMarshalingForPInvokeStringArgumentsFixer
        Inherits SpecifyMarshalingForPInvokeStringArgumentsFixer

        Protected Overrides Function IsAttribute(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.Attribute)
        End Function

        Protected Overrides Function FindNamedArgument(arguments As IReadOnlyList(Of SyntaxNode), argumentName As String) As SyntaxNode
            Return Aggregate arg In arguments.OfType(Of SimpleArgumentSyntax)
                   Where arg.IsNamed
                   Into FirstOrDefault(String.Equals(arg.NameColonEquals.Name.Identifier.Text, argumentName, StringComparison.OrdinalIgnoreCase))
        End Function

        Protected Overrides Function IsDeclareStatement(node As SyntaxNode) As Boolean
            Return node.IsKind(SyntaxKind.DeclareFunctionStatement) OrElse
                   node.IsKind(SyntaxKind.DeclareSubStatement)
        End Function

        Protected Overrides Sub FixDeclareStatement(editor As SyntaxEditor, node As SyntaxNode)
            Dim decl = CType(node, DeclareStatementSyntax)
            Dim newCharSetKeyword = SyntaxFactory.Token(SyntaxKind.UnicodeKeyword).
                    WithLeadingTrivia(decl.CharsetKeyword.LeadingTrivia).
                    WithTrailingTrivia(decl.CharsetKeyword.TrailingTrivia).
                    WithAdditionalAnnotations(Formatter.Annotation)

            editor.ReplaceNode(decl, decl.WithCharsetKeyword(newCharSetKeyword))
        End Sub

    End Class
End Namespace
