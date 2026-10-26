' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Usage

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Usage

    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicUseVolatileReadWriteFixer
        Inherits UseVolatileReadWriteFixer
        Protected Overrides Function GetArgumentForVolatileReadCall(argument As IArgumentOperation, volatileReadParameter as IParameterSymbol) As SyntaxNode
            Dim argumentSyntax = DirectCast(argument.Syntax, SimpleArgumentSyntax)
            If argumentSyntax.NameColonEquals Is Nothing Then
                Return argumentSyntax
            End If

            Return argumentSyntax.WithNameColonEquals(SyntaxFactory.NameColonEquals(SyntaxFactory.IdentifierName(volatileReadParameter.Name)))
        End Function

        Protected Overrides Iterator Function GetArgumentForVolatileWriteCall(arguments As ImmutableArray(Of IArgumentOperation), volatileWriteParameters As ImmutableArray(Of IParameterSymbol)) As IEnumerable(Of SyntaxNode)
            For Each argument In arguments
                Dim argumentSyntax = DirectCast(argument.Syntax, SimpleArgumentSyntax)
                If argumentSyntax.NameColonEquals Is Nothing Then
                    Yield argumentSyntax
                Else
                    Dim parameterName = volatileWriteParameters(argument.Parameter.Ordinal).Name
                    Yield argumentSyntax.WithNameColonEquals(SyntaxFactory.NameColonEquals(SyntaxFactory.IdentifierName(parameterName)))
                End If
            Next
        End Function
    End Class

End Namespace