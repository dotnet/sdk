' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage

    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicUseVolatileReadWriteFixer
        Inherits UseVolatileReadWriteFixer
        Protected Overrides Function GetArguments(invocationSyntax As SyntaxNode) As ImmutableArray(Of SyntaxNode)
            Return ImmutableArray.CreateRange(Of SyntaxNode)(DirectCast(invocationSyntax, InvocationExpressionSyntax).ArgumentList.Arguments)
        End Function

        Protected Overrides Function WithParameterName(argumentSyntax As SyntaxNode, parameterName As String) As SyntaxNode
            Dim argument = DirectCast(argumentSyntax, SimpleArgumentSyntax)
            If argument.NameColonEquals Is Nothing Then
                Return argument
            End If

            Return argument.WithNameColonEquals(SyntaxFactory.NameColonEquals(SyntaxFactory.IdentifierName(parameterName)))
        End Function
    End Class

End Namespace