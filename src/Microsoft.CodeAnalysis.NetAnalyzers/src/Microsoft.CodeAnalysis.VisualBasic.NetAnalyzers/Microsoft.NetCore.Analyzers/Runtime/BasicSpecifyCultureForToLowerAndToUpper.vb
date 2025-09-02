' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.NetCore.Analyzers.Runtime
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicSpecifyCultureForToLowerAndToUpperAnalyzer
        Inherits SpecifyCultureForToLowerAndToUpperAnalyzer

        Protected Overrides Function GetMethodNameLocation(node As SyntaxNode) As Location
            Debug.Assert(node.IsKind(SyntaxKind.InvocationExpression) OrElse node.IsKind(SyntaxKind.SimpleMemberAccessExpression))

            If node.IsKind(SyntaxKind.InvocationExpression) Then
                Dim invocation = DirectCast(node, InvocationExpressionSyntax)
                If invocation.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then
                    Return DirectCast(invocation.Expression, MemberAccessExpressionSyntax).Name.GetLocation()
                End If
            End If

            If node.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then
                Return DirectCast(node, MemberAccessExpressionSyntax).Name.GetLocation()
            End If

            Return node.GetLocation()
        End Function
    End Class
End Namespace
