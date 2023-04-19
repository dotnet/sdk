' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Runtime

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Runtime
    <ExportCodeFixProvider(LanguageNames.VisualBasic)>
    Public NotInheritable Class BasicPreferDictionaryTryGetValueFixer
        Inherits PreferDictionaryTryGetValueFixer

        Public Overrides Async Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim diagnostic = context.Diagnostics.FirstOrDefault()
            Dim dictionaryAccessLocation = diagnostic?.AdditionalLocations(0)
            If dictionaryAccessLocation Is Nothing
                Return
            End If

            Dim document = context.Document
            Dim root = Await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)

            Dim dictionaryAccess = TryCast(root.FindNode(dictionaryAccessLocation.SourceSpan, getInnermostNodeForTie:=True), InvocationExpressionSyntax)
            Dim containsKeyInvocation = TryCast(root.FindNode(context.Span), InvocationExpressionSyntax)
            Dim containsKeyAccess = TryCast(containsKeyInvocation?.Expression, MemberAccessExpressionSyntax)
            If _
                dictionaryAccess Is Nothing Or containsKeyInvocation Is Nothing Or
                containsKeyAccess Is Nothing Then
                Return
            End If

            Dim semanticModel = Await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(False)
            Dim dictionaryValueType = GetDictionaryValueType(semanticModel, dictionaryAccess.Expression)
            Dim replaceFunction = Async Function(ct as CancellationToken) As Task(Of Document)
                                      Dim editor = Await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(False)
                                      Dim generator = editor.Generator

                                      Dim tryGetValueAccess = generator.MemberAccessExpression(containsKeyAccess.Expression, TryGetValue)
                                      Dim keyArgument = containsKeyInvocation.ArgumentList.Arguments.FirstOrDefault()
                                      Dim valueAssignment = generator.LocalDeclarationStatement(dictionaryValueType, Value).WithLeadingTrivia(SyntaxFactory.CarriageReturn).WithoutTrailingTrivia()
                                      Dim tryGetValueInvocation = generator.InvocationExpression(tryGetValueAccess, keyArgument, generator.Argument(generator.IdentifierName(Value)))

#Disable Warning IDE0270 ' Use coalesce expression - suppressed for readability
                                      Dim ifStatement As SyntaxNode = containsKeyAccess.AncestorsAndSelf().OfType(Of MultiLineIfBlockSyntax).FirstOrDefault()
                                      If ifStatement Is Nothing Then
                                          ifStatement = containsKeyAccess.AncestorsAndSelf().OfType(Of SingleLineIfStatementSyntax).FirstOrDefault()
                                      End If
#Enable Warning IDE0270 ' Use coalesce expression

                                      If ifStatement Is Nothing Then
                                          ' For ternary expressions, we need to add the value assignment before the parent of the expression, since the ternary expression is not an alone-standing expression. 
                                          ifStatement = containsKeyAccess.AncestorsAndSelf().OfType(Of TernaryConditionalExpressionSyntax).FirstOrDefault()?.Parent
                                      End If

                                      editor.InsertBefore(ifStatement, valueAssignment)
                                      editor.ReplaceNode(containsKeyInvocation, tryGetValueInvocation)
                                      editor.ReplaceNode(dictionaryAccess, generator.IdentifierName(Value))

                                      Return editor.GetChangedDocument()
                                  End Function

            Dim action = CodeAction.Create(PreferDictionaryTryGetValueCodeFixTitle, replaceFunction, PreferDictionaryTryGetValueCodeFixTitle)
            context.RegisterCodeFix(action, context.Diagnostics)
        End Function

        Private Shared Function GetDictionaryValueType(semanticModel As SemanticModel, dictionary As SyntaxNode) As ITypeSymbol
            Dim symbol = DirectCast(semanticModel.GetSymbolInfo(dictionary).Symbol, ILocalSymbol)
            Dim type = DirectCast(symbol.Type, INamedTypeSymbol)
            Dim arguments = type.TypeArguments
            Return arguments(1)
        End Function
    End Class
End Namespace