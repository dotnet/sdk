' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Diagnostics.CodeAnalysis
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime

    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Partial Public NotInheritable Class BasicForwardCancellationTokenToInvocationsFixer
        Inherits ForwardCancellationTokenToInvocationsFixer(Of ArgumentSyntax)

        Protected Overrides Function TryGetInvocation(model As SemanticModel, node As SyntaxNode, ct As CancellationToken, <NotNullWhen(True)> ByRef invocation As IInvocationOperation) As Boolean

            Dim operation As IOperation

            Dim parentSyntax As MemberAccessExpressionSyntax = TryCast(node.Parent, MemberAccessExpressionSyntax)

            If parentSyntax IsNot Nothing Then
                operation = model.GetOperation(node.Parent.Parent, ct)
            Else
                operation = model.GetOperation(node.Parent, ct)
            End If

            invocation = TryCast(operation, IInvocationOperation)

            Return invocation IsNot Nothing

        End Function

        Protected Overrides Function IsArgumentNamed(argumentOperation As IArgumentOperation) As Boolean
            Dim argument As SimpleArgumentSyntax = TryCast(argumentOperation.Syntax, SimpleArgumentSyntax)
            Return argument IsNot Nothing AndAlso argument.NameColonEquals IsNot Nothing
        End Function

        Protected Overrides Function GetConditionalOperationInvocationExpression(invocationNode As SyntaxNode) As SyntaxNode

            Dim invocationExpression As InvocationExpressionSyntax = CType(invocationNode, InvocationExpressionSyntax)
            Return invocationExpression.Expression

        End Function

        Protected Overrides Function TryGetExpressionAndArguments(invocationNode As SyntaxNode, ByRef expression As SyntaxNode, ByRef arguments As ImmutableArray(Of ArgumentSyntax)) As Boolean

            Dim invocationExpression As InvocationExpressionSyntax = TryCast(invocationNode, InvocationExpressionSyntax)

            If invocationExpression IsNot Nothing Then

                expression = invocationExpression.Expression
                arguments = invocationExpression.ArgumentList.Arguments.ToImmutableArray
                Return True

            End If

            expression = Nothing
            arguments = ImmutableArray(Of ArgumentSyntax).Empty
            Return False

        End Function

        Protected Overrides Function GetTypeSyntaxForArray(type As IArrayTypeSymbol) As SyntaxNode
            Return TypeNameVisitor.GetTypeSyntaxForSymbol(type.ElementType)
        End Function

        Protected Overrides Function GetExpressions(newArguments As ImmutableArray(Of ArgumentSyntax)) As IEnumerable(Of SyntaxNode)
            Return From argument In newArguments
                   Select argument.GetExpression()
        End Function
    End Class
End Namespace
