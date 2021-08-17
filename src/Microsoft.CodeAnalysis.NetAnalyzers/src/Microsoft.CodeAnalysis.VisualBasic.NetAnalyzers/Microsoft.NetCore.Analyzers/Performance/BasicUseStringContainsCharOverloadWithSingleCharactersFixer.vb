' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance

    Public NotInheritable Class BasicUseStringContainsCharOverloadWithSingleCharactersFixer
        Inherits UseStringContainsCharOverloadWithSingleCharactersCodeFix

        Protected Overrides Function TryGetArgumentName(violatingNode As SyntaxNode, ByRef argumentName As String) As Boolean
            If TypeOf violatingNode IsNot SimpleArgumentSyntax Then
                Return False
            End If
            Dim argumentSyntax = CType(violatingNode, SimpleArgumentSyntax)
            If argumentSyntax.NameColonEquals Is Nothing Then
                Return False
            End If
            argumentName = argumentSyntax.NameColonEquals.Name.Identifier.ValueText
            Return True
        End Function

        Protected Overrides Function TryGetLiteralValueFromNode(violatingNode As SyntaxNode, ByRef charLiteral As Char) As Boolean
            If TypeOf violatingNode Is LiteralExpressionSyntax Then
                Return TryGetCharFromLiteralExpressionSyntax(CType(violatingNode, LiteralExpressionSyntax), charLiteral)
            ElseIf TypeOf violatingNode Is SimpleArgumentSyntax Then
                Dim argumentSyntaxNode = CType(violatingNode, SimpleArgumentSyntax)
                If TypeOf argumentSyntaxNode.Expression Is LiteralExpressionSyntax Then
                    Return TryGetCharFromLiteralExpressionSyntax(CType(argumentSyntaxNode.Expression, LiteralExpressionSyntax), charLiteral)
                End If
                Return False
            End If
            Return False
        End Function

        Private Shared Function TryGetCharFromLiteralExpressionSyntax(sourceLiteralExpressionSyntax As LiteralExpressionSyntax, ByRef parsedCharLiteral As Char) As Boolean
            If TypeOf sourceLiteralExpressionSyntax.Token.Value IsNot String Then
                Return False
            End If
            Dim sourceLiteralValue = CType(sourceLiteralExpressionSyntax.Token.Value, String)
            If Char.TryParse(sourceLiteralValue, parsedCharLiteral) Then
                Return True
            End If
            Return False
        End Function

    End Class

End Namespace
