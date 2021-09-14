' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicForwardCancellationTokenToInvocationsAnalyzer

        Inherits ForwardCancellationTokenToInvocationsAnalyzer

        Protected Overrides Function GetInvocationMethodNameNode(invocationNode As SyntaxNode) As SyntaxNode

            Dim invocationExpression = TryCast(invocationNode, InvocationExpressionSyntax)

            If invocationExpression IsNot Nothing Then

                Dim memberBindingExpression = TryCast(invocationExpression.Expression, MemberAccessExpressionSyntax)

                If memberBindingExpression IsNot Nothing Then

                    Return memberBindingExpression.Name

                End If

                Return invocationExpression.Expression

            End If

            Return Nothing

        End Function

        Protected Overrides Function ArgumentsImplicitOrNamed(cancellationTokenType As INamedTypeSymbol, arguments As ImmutableArray(Of IArgumentOperation)) As Boolean

            Return arguments.Any(Function(a)

                                     If a.IsImplicit AndAlso Not a.Parameter.Type.Equals(cancellationTokenType) Then
                                         Return True
                                     End If

                                     Dim argumentNode As ArgumentSyntax = TryCast(a.Syntax, ArgumentSyntax)

                                     Return argumentNode IsNot Nothing AndAlso argumentNode.IsNamed

                                 End Function)

        End Function

    End Class

End Namespace
