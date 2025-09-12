' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    ''' <summary>
    ''' CA2242: Test for NaN correctly
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicTestForNaNCorrectlyFixer
        Inherits TestForNaNCorrectlyFixer

        Protected Overrides Function GetBinaryExpression(node As SyntaxNode) As SyntaxNode
            Dim argumentSyntax = TryCast(node, ArgumentSyntax)
            Return If(argumentSyntax IsNot Nothing, argumentSyntax.GetExpression(), node)
        End Function

        Protected Overrides Function IsEqualsOperator(node As SyntaxNode) As Boolean
            return node.IsKind(SyntaxKind.EqualsExpression)
        End Function

        Protected Overrides Function IsNotEqualsOperator(node As SyntaxNode) As Boolean
            return node.IsKind(SyntaxKind.NotEqualsExpression)
        End Function

        Protected Overrides Function GetLeftOperand(binaryExpressionSyntax As SyntaxNode) As SyntaxNode
            Return DirectCast(binaryExpressionSyntax, BinaryExpressionSyntax).Left
        End Function

        Protected Overrides Function GetRightOperand(binaryExpressionSyntax As SyntaxNode) As SyntaxNode
            Return DirectCast(binaryExpressionSyntax, BinaryExpressionSyntax).Right
        End Function
    End Class
End Namespace
