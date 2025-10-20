' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeQuality.Analyzers.QualityGuidelines

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines
    ''' <summary>
    ''' CA1822: Mark members as static
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicMarkMembersAsStaticFixer
        Inherits MarkMembersAsStaticFixer

        Protected Overrides Function GetTypeArguments(node As SyntaxNode) As IEnumerable(Of SyntaxNode)
            Return TryCast(node, GenericNameSyntax)?.TypeArgumentList.Arguments
        End Function

        Protected Overrides Function GetExpressionOfInvocation(invocation As SyntaxNode) As SyntaxNode
            Return TryCast(invocation, InvocationExpressionSyntax)?.Expression
        End Function

        Protected Overrides Function GetSyntaxNodeToReplace(memberReference As IMemberReferenceOperation) As SyntaxNode
            Dim syntax = MyBase.GetSyntaxNodeToReplace(memberReference)

            ' VB operation tree seems to have an incorrect syntax node association.
            Return If(syntax.IsKind(SyntaxKind.AddressOfExpression), DirectCast(syntax, UnaryExpressionSyntax).Operand, syntax)
        End Function
    End Class
End Namespace
