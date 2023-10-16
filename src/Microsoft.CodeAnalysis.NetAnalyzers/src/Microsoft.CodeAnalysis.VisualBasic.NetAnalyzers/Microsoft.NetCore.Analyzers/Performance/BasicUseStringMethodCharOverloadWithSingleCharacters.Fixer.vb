' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance

    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicUseStringMethodCharOverloadWithSingleCharactersFixer
        Inherits UseStringMethodCharOverloadWithSingleCharactersFixer

        Protected Overrides Function TryGetChar(model As SemanticModel, argumentListNode As SyntaxNode, ByRef c As Char) As Boolean
            If TypeOf argumentListNode IsNot ArgumentListSyntax Then
                Return False
            End If

            Dim argumentList = CType(argumentListNode, ArgumentListSyntax)

            Dim stringArgumentNode As ArgumentSyntax = Nothing
            For Each argument In argumentList.Arguments
                Dim argumentOperation = TryCast(model.GetOperation(argument), IArgumentOperation)

                If argumentOperation?.Parameter IsNot Nothing AndAlso argumentOperation.Parameter.Ordinal = 0 Then
                    stringArgumentNode = argument
                    Exit For
                End If
            Next

            If stringArgumentNode IsNot Nothing And TypeOf stringArgumentNode.GetExpression() Is LiteralExpressionSyntax Then
                Return TryGetCharFromLiteralExpressionSyntax(CType(stringArgumentNode.GetExpression(), LiteralExpressionSyntax), c)
            End If

            Return False
        End Function

        Protected Overrides Function CreateCodeAction(document As Document, argumentListNode As SyntaxNode, sourceCharLiteral As Char) As CodeAction
            Return New BasicReplaceStringLiteralWithCharLiteralCodeAction(document, argumentListNode, sourceCharLiteral)
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

        Private NotInheritable Class BasicReplaceStringLiteralWithCharLiteralCodeAction
            Inherits ReplaceStringLiteralWithCharLiteralCodeAction

            Public Sub New(document As Document, argumentListNode As SyntaxNode, sourceCharLiteral As Char)
                MyBase.New(document, argumentListNode, sourceCharLiteral)
            End Sub

            Protected Overrides Sub ApplyFix(editor As DocumentEditor, model As SemanticModel, oldArgumentListNode As SyntaxNode, c As Char)
                Dim argumentNode = editor.Generator.Argument(editor.Generator.LiteralExpression(c))
                Dim arguments = {argumentNode}.Concat(
                    CType(oldArgumentListNode, ArgumentListSyntax).Arguments.
                        [Select](Function(arg) TryCast(model.GetOperation(arg), IArgumentOperation)).Where(Function(arg) PreserveArgument(arg)).[Select](Function(arg) arg.Syntax))
                Dim argumentListNode = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments))

                editor.ReplaceNode(oldArgumentListNode, argumentListNode.WithTriviaFrom(oldArgumentListNode))
            End Sub
        End Class
    End Class

End Namespace
