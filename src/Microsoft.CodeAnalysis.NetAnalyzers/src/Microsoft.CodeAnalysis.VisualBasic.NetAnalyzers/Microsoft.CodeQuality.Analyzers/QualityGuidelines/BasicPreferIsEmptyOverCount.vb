' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Analyzer.Utilities
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    ''' <summary>
    ''' CA1836: Prefer IsEmpty over Count when available.
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicPreferIsEmptyOverCountFixer
        Inherits PreferIsEmptyOverCountFixer

        Protected Overrides Function GetExpressionFromMemberAccessor(node As SyntaxNode) As SyntaxNode
            While TypeOf node Is ParenthesizedExpressionSyntax
                node = CType(node, ParenthesizedExpressionSyntax).Expression
            End While

            Dim basicMemberAccessor = TryCast(node, MemberAccessExpressionSyntax)
            If basicMemberAccessor IsNot Nothing Then
                Return basicMemberAccessor.Expression
            End If

            RoslynDebug.Assert(TypeOf node Is IdentifierNameSyntax)
            Return Nothing
        End Function

        Protected Overrides Function GetMemberAccessorFromBinary(binaryExpression As SyntaxNode, useRightSide As Boolean) As SyntaxNode
            Dim basicBinaryExpression = CType(binaryExpression, BinaryExpressionSyntax)
            Return If(useRightSide, basicBinaryExpression.Right, basicBinaryExpression.Left)
        End Function
    End Class
End Namespace
