' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance

    ''' <summary>
    ''' CA1829: C# implementation Of use Property instead Of <see cref="Enumerable.Count(Of TSource)(IEnumerable(Of TSource))"/>, When available.
    ''' Implements the <see cref="CodeFixProvider" />
    ''' </summary>
    ''' <seealso cref="UsePropertyInsteadOfCountMethodWhenAvailableFixer"/>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicUsePropertyInsteadOfCountMethodWhenAvailableFixer
        Inherits UsePropertyInsteadOfCountMethodWhenAvailableFixer

        ''' <summary>
        ''' Gets the expression from the specified <paramref name="invocationNode" /> where to replace the invocation of the
        ''' <see cref="Enumerable.Count(Of TSource)(IEnumerable(Of TSource))" /> method with a property invocation.
        ''' </summary>
        ''' <param name="invocationNode">The invocation node to get a fixer for.</param>
        ''' <param name="memberAccessNode">The member access node for the invocation node.</param>
        ''' <param name="nameNode">The name node for the invocation node.</param>
        ''' <returns><see langword="true" /> if a <paramref name="memberAccessNode" /> and <paramref name="nameNode" /> were found;
        ''' <see langword="false" /> otherwise.</returns>
        Protected Overrides Function TryGetExpression(invocationNode As SyntaxNode, ByRef memberAccessNode As SyntaxNode, ByRef nameNode As SyntaxNode) As Boolean

            Dim invocationExpression = TryCast(invocationNode, InvocationExpressionSyntax)

            If invocationExpression Is Nothing Then

                Return False

            End If

            Dim memberAccessExpression = TryCast(invocationExpression.Expression, MemberAccessExpressionSyntax)

            If memberAccessExpression IsNot Nothing Then

                memberAccessNode = memberAccessExpression
                nameNode = memberAccessExpression.Name

                Return True

            End If

            Return False

        End Function

    End Class

End Namespace
