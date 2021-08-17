' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <ExportCodeFixProvider(LanguageNames.VisualBasic, Name:="CA2229 CodeFix provider"), [Shared]>
    Public Class BasicMarkAllNonSerializableFieldsFixer
        Inherits MarkAllNonSerializableFieldsFixer

        Protected Overrides Function GetFieldDeclarationNode(node As SyntaxNode) As SyntaxNode
            Dim fieldNode = node
            While fieldNode IsNot Nothing AndAlso fieldNode.Kind() <> SyntaxKind.FieldDeclaration
                fieldNode = fieldNode.Parent
            End While

            Return If(fieldNode?.Kind() = SyntaxKind.FieldDeclaration, fieldNode, Nothing)
        End Function
    End Class
End Namespace
