' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicUseOrdinalStringComparisonAnalyzer
        Inherits UseOrdinalStringComparisonAnalyzer

        Protected Overrides Function GetMethodNameLocation(invocationNode As SyntaxNode) As Location
            Debug.Assert(invocationNode.IsKind(SyntaxKind.InvocationExpression))

            Dim invocation = CType(invocationNode, InvocationExpressionSyntax)
            If invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then
                Return DirectCast(invocation.Expression, MemberAccessExpressionSyntax).Name.GetLocation()
            ElseIf invocation.Expression.IsKind(SyntaxKind.ConditionalAccessExpression) Then
                Return DirectCast(invocation.Expression, ConditionalAccessExpressionSyntax).WhenNotNull.GetLocation()
            End If

            Return invocation.GetLocation()
        End Function
    End Class
End Namespace
