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
            If diagnostic Is Nothing OrElse diagnostic.AdditionalLocations.Count < 0 Then
                Return
            End If

            Dim document = context.Document
            Dim root = Await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)

            Dim containsKeyInvocation = TryCast(root.FindNode(context.Span), InvocationExpressionSyntax)
            Dim containsKeyAccess = TryCast(containsKeyInvocation?.Expression, MemberAccessExpressionSyntax)
            If containsKeyInvocation Is Nothing OrElse containsKeyAccess Is Nothing Then
                Return
            End If

            Dim dictionaryAccessors As New List(Of SyntaxNode)
            Dim addStatementNode As ExecutableStatementSyntax = Nothing
            Dim changedValueNode As SyntaxNode = Nothing
            For Each location As Location In diagnostic.AdditionalLocations
                Dim node = root.FindNode(location.SourceSpan, getInnermostNodeForTie:=True)
                Select Case node.GetType()
                    Case GetType(InvocationExpressionSyntax)
                        Dim invocation = DirectCast(node, InvocationExpressionSyntax)
                        If invocation.ArgumentList.Arguments.Count = 2 Then
                            Dim add = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
                            If addStatementNode IsNot Nothing OrElse
                               add Is Nothing OrElse
                               add.Name.Identifier.Text <> PreferDictionaryTryGetValueAnalyzer.AddMethodeName Then
                                Return
                            End If

                            changedValueNode = invocation.ArgumentList.Arguments(1).GetExpression()
                            addStatementNode = invocation.FirstAncestorOrSelf(Of ExpressionStatementSyntax)
                        Else
                            dictionaryAccessors.Add(node)
                        End If
                    Case GetType(MemberAccessExpressionSyntax)
                        Dim memberAccess = DirectCast(node, MemberAccessExpressionSyntax)
                        If memberAccess.Kind() <> SyntaxKind.DictionaryAccessExpression Then
                            Return
                        End If

                        dictionaryAccessors.Add(node)
                    Case GetType(AssignmentStatementSyntax)
                        If addStatementNode IsNot Nothing Then
                            Return
                        End If

                        Dim assignment = DirectCast(node, AssignmentStatementSyntax)
                        changedValueNode = assignment.Right
                        addStatementNode = assignment
                    Case Else
                        Return
                End Select
            Next

            Dim targetAmount = diagnostic.AdditionalLocations.Count
            If addStatementNode IsNot Nothing Then
                targetAmount -= 1
            End If

            If targetAmount <> dictionaryAccessors.Count Then
                Return
            End If

            Dim semanticModel = Await context.Document.GetSemanticModelAsync(context.CancellationToken).
                    ConfigureAwait(False)
            Dim dictionaryValueType = GetDictionaryValueType(semanticModel, containsKeyAccess.Expression)

            Dim replaceFunction =
                    Async Function(ct As CancellationToken) As Task(Of Document)
                        Dim editor = Await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(False)
                        Dim generator = editor.Generator

                        Dim tryGetValueAccess = generator.MemberAccessExpression(containsKeyAccess.Expression,
                                                                                 TryGetValue)
                        Dim keyArgument = containsKeyInvocation.ArgumentList.Arguments.FirstOrDefault()
                        Dim valueAssignment =
                                generator.LocalDeclarationStatement(dictionaryValueType,
                                                                    Value,
                                                                    generator.DefaultExpression(dictionaryValueType)).
                                WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed).
                                WithoutTrailingTrivia()
                        Dim identifierName As SyntaxNode = generator.IdentifierName(Value)
                        Dim tryGetValueInvocation = generator.InvocationExpression(tryGetValueAccess,
                                                                                   keyArgument,
                                                                                   generator.Argument(identifierName))

#Disable Warning IDE0270 ' Use coalesce expression - suppressed for readability
                        Dim ifStatement As SyntaxNode = containsKeyAccess.FirstAncestorOrSelf(Of MultiLineIfBlockSyntax)
                        If ifStatement Is Nothing Then
                            ifStatement = containsKeyAccess.FirstAncestorOrSelf(Of SingleLineIfStatementSyntax)
                        End If
#Enable Warning IDE0270 ' Use coalesce expression

                        If ifStatement Is Nothing Then
                            ' For ternary expressions, we need to add the value assignment before the parent of
                            ' the expression, since the ternary expression is not an alone-standing expression.
                            ifStatement = containsKeyAccess.FirstAncestorOrSelf(Of TernaryConditionalExpressionSyntax)?.Parent
                        End If

                        If Not ifStatement.HasLeadingTrivia OrElse
                           Not ifStatement.GetLeadingTrivia().Any(Function(t) t.RawKind = SyntaxKind.EndOfLineTrivia) Then
                            valueAssignment = valueAssignment.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
                        End If

                        editor.InsertBefore(ifStatement, valueAssignment)
                        editor.ReplaceNode(containsKeyInvocation, tryGetValueInvocation)

                        If addStatementNode IsNot Nothing Then
                            Dim newValueAssignment As SyntaxNode = generator.ExpressionStatement(
                                generator.AssignmentStatement(identifierName, changedValueNode)).
                                    WithTrailingTrivia(SyntaxFactory.ElasticMarker)
                            editor.InsertBefore(addStatementNode, newValueAssignment)
                            editor.ReplaceNode(changedValueNode, identifierName)
                        End If

                        For Each dictionaryAccess In dictionaryAccessors
                            editor.ReplaceNode(dictionaryAccess, identifierName)
                        Next

                        Return editor.GetChangedDocument()
                    End Function

            Dim action = CodeAction.Create(PreferDictionaryTryGetValueCodeFixTitle, replaceFunction,
                                           PreferDictionaryTryGetValueCodeFixTitle)
            context.RegisterCodeFix(action, context.Diagnostics)
        End Function

        Private Shared Function GetDictionaryValueType(semanticModel As SemanticModel, dictionary As SyntaxNode) As ITypeSymbol
            Dim type = DirectCast(semanticModel.GetTypeInfo(dictionary).Type, INamedTypeSymbol)
            Return type.TypeArguments(1)
        End Function
    End Class
End Namespace
