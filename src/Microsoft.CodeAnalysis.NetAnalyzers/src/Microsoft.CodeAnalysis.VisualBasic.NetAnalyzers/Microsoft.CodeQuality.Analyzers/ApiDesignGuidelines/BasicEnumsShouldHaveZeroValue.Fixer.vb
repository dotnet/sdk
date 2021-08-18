' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    ' <summary>
    ' CA1008: Enums should have zero value
    ' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public Class BasicEnumsShouldHaveZeroValueFixer
        Inherits EnumsShouldHaveZeroValueFixer

        Protected Overrides Function GetParentNodeOrSelfToFix(nodeToFix As SyntaxNode) As SyntaxNode
            If nodeToFix.IsKind(SyntaxKind.EnumStatement) And nodeToFix.Parent IsNot Nothing Then
                Return nodeToFix.Parent
            End If

            Return nodeToFix
        End Function
    End Class
End Namespace
