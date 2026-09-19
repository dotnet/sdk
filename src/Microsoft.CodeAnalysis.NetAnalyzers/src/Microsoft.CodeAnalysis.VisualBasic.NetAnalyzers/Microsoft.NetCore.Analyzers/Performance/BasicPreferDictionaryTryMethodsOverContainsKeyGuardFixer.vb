' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports System.Threading
Imports Analyzer.Utilities
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicPreferDictionaryTryMethodsOverContainsKeyGuardFixer
        Inherits PreferDictionaryTryMethodsOverContainsKeyGuardFixer

        Public Overrides Async Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim diagnostic = context.Diagnostics.FirstOrDefault()
            If diagnostic Is Nothing OrElse diagnostic.AdditionalLocations.Count = 0 Then
                Return
            End If

            Dim document = context.Document
            Dim root = Await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(False)

            If diagnostic.Id = PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId Then
                Dim semanticModel = Await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(False)
                If GetTryGetValueFix(diagnostic, root, semanticModel) IsNot Nothing Then
                    RegisterCodeFix(context, PreferDictionaryTryGetValueCodeFixTitle, TryGetValueEquivalenceKey)
                End If
            ElseIf GetTryAddFix(diagnostic, root) IsNot Nothing Then
                RegisterCodeFix(context, PreferDictionaryTryAddValueCodeFixTitle, TryAddEquivalenceKey)
            End If
        End Function

        Protected Overrides Async Function ApplyFixAsync(document As Document, diagnostic As Diagnostic, editor As SyntaxEditor, state As FixAllState, cancellationToken As CancellationToken) As Task
            If diagnostic.Id = PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId Then
                If state.EquivalenceKey IsNot Nothing AndAlso state.EquivalenceKey <> TryGetValueEquivalenceKey Then
                    Return
                End If

                Dim semanticModel = Await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(False)
                Dim fix = GetTryGetValueFix(diagnostic, editor.OriginalRoot, semanticModel)
                If fix IsNot Nothing Then
                    ApplyTryGetValueFix(editor, semanticModel, state, fix, cancellationToken)
                End If
            Else
                If state.EquivalenceKey IsNot Nothing AndAlso state.EquivalenceKey <> TryAddEquivalenceKey Then
                    Return
                End If

                Dim fix = GetTryAddFix(diagnostic, editor.OriginalRoot)
                If fix IsNot Nothing Then
                    ApplyTryAddFix(editor, fix)
                End If
            End If
        End Function

        Private Shared Function GetTryGetValueFix(diagnostic As Diagnostic, root As SyntaxNode, semanticModel As SemanticModel) As TryGetValueFix
            Dim containsKeyInvocation = TryCast(root.FindNode(diagnostic.Location.SourceSpan), InvocationExpressionSyntax)
            Dim containsKeyAccess = TryCast(containsKeyInvocation?.Expression, MemberAccessExpressionSyntax)
            If containsKeyInvocation Is Nothing OrElse containsKeyAccess Is Nothing Then
                Return Nothing
            End If

            Dim dictionaryAccessors As New List(Of SyntaxNode)
            Dim addStatementNode As ExecutableStatementSyntax = Nothing
            Dim changedValueNode As SyntaxNode = Nothing
            Dim variableName As String = Nothing
            Dim additionalNodes = 0
            Dim localDeclarationStatement As LocalDeclarationStatementSyntax = Nothing
            Dim variableDeclarator As VariableDeclaratorSyntax = Nothing
            For Each location As Location In diagnostic.AdditionalLocations
                Dim node = root.FindNode(location.SourceSpan, getInnermostNodeForTie:=True)
                Select Case node.GetType()
                    Case GetType(InvocationExpressionSyntax)
                        Dim invocation = DirectCast(node, InvocationExpressionSyntax)
                        If invocation.ArgumentList.Arguments.Count = 2 Then
                            Dim add = TryCast(invocation.Expression, MemberAccessExpressionSyntax)
                            If addStatementNode IsNot Nothing OrElse
                               add Is Nothing OrElse
                               add.Name.Identifier.Text <> PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.Add Then
                                Return Nothing
                            End If

                            changedValueNode = invocation.ArgumentList.Arguments(1).GetExpression()
                            addStatementNode = invocation.FirstAncestorOrSelf(Of ExpressionStatementSyntax)
                            additionalNodes += 1
                        Else
                            dictionaryAccessors.Add(node)
                        End If
                    Case GetType(MemberAccessExpressionSyntax)
                        Dim memberAccess = DirectCast(node, MemberAccessExpressionSyntax)
                        If memberAccess.Kind() <> SyntaxKind.DictionaryAccessExpression Then
                            Return Nothing
                        End If

                        dictionaryAccessors.Add(node)
                    Case GetType(AssignmentStatementSyntax)
                        If addStatementNode IsNot Nothing Then
                            Return Nothing
                        End If

                        Dim assignment = DirectCast(node, AssignmentStatementSyntax)
                        changedValueNode = assignment.Right
                        addStatementNode = assignment
                        additionalNodes += 1
                    Case GetType(LocalDeclarationStatementSyntax)
                        localDeclarationStatement = DirectCast(node, LocalDeclarationStatementSyntax)
                        variableName = localDeclarationStatement.Declarators.Item(0).Names.Item(0).Identifier.ValueText
                        additionalNodes += 1
                    Case GetType(VariableDeclaratorSyntax)
                        variableDeclarator = DirectCast(node, VariableDeclaratorSyntax)
                        If variableDeclarator.Parent.GetType() <> GetType(LocalDeclarationStatementSyntax) Then
                            Return Nothing
                        End If

                        localDeclarationStatement = DirectCast(variableDeclarator.Parent, LocalDeclarationStatementSyntax)
                        variableName = variableDeclarator.Names.Item(0).Identifier.ValueText
                        additionalNodes += 1
                    Case Else
                        Return Nothing
                End Select
            Next

            If diagnostic.AdditionalLocations.Count <> dictionaryAccessors.Count + additionalNodes Then
                Return Nothing
            End If

            ' The value assignment is inserted before the statement the guard belongs to, so the fix only
            ' applies to a shape that has one.
            Dim anchor As SyntaxNode = containsKeyAccess.FirstAncestorOrSelf(Of MultiLineIfBlockSyntax)
            If anchor Is Nothing Then
                anchor = containsKeyAccess.FirstAncestorOrSelf(Of SingleLineIfStatementSyntax)
            End If

            If anchor Is Nothing Then
                ' For ternary expressions, we need to add the value assignment before the parent of
                ' the expression, since the ternary expression is not an alone-standing expression.
                anchor = containsKeyAccess.FirstAncestorOrSelf(Of TernaryConditionalExpressionSyntax)?.Parent
            End If

            If anchor Is Nothing Then
                Return Nothing
            End If

            Return New TryGetValueFix(containsKeyInvocation, containsKeyAccess, anchor, dictionaryAccessors,
                                      addStatementNode, changedValueNode, variableName, localDeclarationStatement,
                                      variableDeclarator, GetDictionaryValueType(semanticModel, containsKeyAccess.Expression))
        End Function

        Private Shared Sub ApplyTryGetValueFix(editor As SyntaxEditor, semanticModel As SemanticModel, state As FixAllState, fix As TryGetValueFix, cancellationToken As CancellationToken)
            Dim generator = editor.Generator

            Dim position = fix.ContainsKeyAccess.SpanStart
            Dim identifierName = DirectCast(If(fix.VariableName Is Nothing,
                                               generator.FirstUnusedIdentifierName(semanticModel,
                                                                                   position,
                                                                                   Value,
                                                                                   reservedNames:=state.GetReservedNames(semanticModel, position, cancellationToken)),
                                               generator.IdentifierName(fix.VariableName)),
                                            IdentifierNameSyntax)
            state.RecordIntroducedName(semanticModel, position, identifierName.Identifier.ValueText, cancellationToken)

            Dim tryGetValueAccess = generator.MemberAccessExpression(fix.ContainsKeyAccess.Expression,
                                                                     TryGetValue)
            Dim keyArgument = fix.ContainsKeyInvocation.ArgumentList.Arguments.FirstOrDefault()
            Dim valueAssignment =
                    generator.LocalDeclarationStatement(fix.DictionaryValueType,
                                                        identifierName.Identifier.ValueText,
                                                        generator.DefaultExpression(fix.DictionaryValueType)).
                    WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed).
                    WithoutTrailingTrivia()
            Dim tryGetValueInvocation = generator.InvocationExpression(tryGetValueAccess,
                                                                       keyArgument,
                                                                       generator.Argument(identifierName))

            If Not fix.ValueAssignmentAnchor.HasLeadingTrivia OrElse
               Not fix.ValueAssignmentAnchor.GetLeadingTrivia().Any(Function(t) t.RawKind = SyntaxKind.EndOfLineTrivia) Then
                valueAssignment = valueAssignment.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
            End If

            editor.InsertBefore(fix.ValueAssignmentAnchor, valueAssignment)
            editor.ReplaceNode(fix.ContainsKeyInvocation, tryGetValueInvocation)

            If fix.AddStatementNode IsNot Nothing Then
                Dim newValueAssignment As SyntaxNode = generator.ExpressionStatement(
                    generator.AssignmentStatement(identifierName, fix.ChangedValueNode)).
                        WithTrailingTrivia(SyntaxFactory.ElasticMarker)
                editor.InsertBefore(fix.AddStatementNode, newValueAssignment)
                editor.ReplaceNode(fix.ChangedValueNode, identifierName)
            End If

            For Each dictionaryAccess In fix.DictionaryAccessors
                editor.ReplaceNode(dictionaryAccess, identifierName)
            Next

            If fix.LocalDeclarationStatement IsNot Nothing Then
                If fix.VariableDeclarator Is Nothing Then
                    editor.RemoveNode(fix.LocalDeclarationStatement)
                Else
                    editor.RemoveNode(fix.VariableDeclarator)
                End If
            End If
        End Sub

        Private Shared Function GetTryAddFix(diagnostic As Diagnostic, root As SyntaxNode) As TryAddFix
            Dim containsKeyInvocation = TryCast(root.FindNode(diagnostic.Location.SourceSpan), InvocationExpressionSyntax)
            Dim containsKeyAccess = TryCast(containsKeyInvocation?.Expression, MemberAccessExpressionSyntax)
            If containsKeyInvocation Is Nothing OrElse containsKeyAccess Is Nothing Then
                Return Nothing
            End If

            Dim dictionaryAddInvocation = TryCast(root.FindNode(diagnostic.AdditionalLocations(0).SourceSpan, getInnermostNodeForTie:=True), InvocationExpressionSyntax)
            If dictionaryAddInvocation Is Nothing Then
                Return Nothing
            End If

            Dim ifStatement = containsKeyInvocation.AncestorsAndSelf().OfType(Of MultiLineIfBlockSyntax).FirstOrDefault()
            If ifStatement Is Nothing Then
                Return Nothing
            End If

            Return New TryAddFix(containsKeyInvocation, containsKeyAccess, dictionaryAddInvocation, ifStatement)
        End Function

        Private Shared Sub ApplyTryAddFix(editor As SyntaxEditor, fix As TryAddFix)
            Dim generator = editor.Generator

            Dim tryAddValueAccess = generator.MemberAccessExpression(fix.ContainsKeyAccess.Expression, TryAdd)
            Dim dictionaryAddArguments = fix.DictionaryAddInvocation.ArgumentList.Arguments
            Dim tryAddInvocation = generator.InvocationExpression(tryAddValueAccess, dictionaryAddArguments(0), dictionaryAddArguments(1))
            Dim ifStatement = fix.IfStatement

            Dim unary = TryCast(ifStatement.IfStatement.Condition, UnaryExpressionSyntax)
            If unary IsNot Nothing And unary.IsKind(SyntaxKind.NotExpression) Then
                If ifStatement.Statements.Count = 1 Then
                    If ifStatement.ElseBlock Is Nothing Then
                        Dim invocationWithTrivia = tryAddInvocation.WithTriviaFrom(ifStatement)
                        editor.ReplaceNode(ifStatement, generator.ExpressionStatement(invocationWithTrivia))
                    Else
                        Dim newIf = ifStatement.WithStatements(ifStatement.ElseBlock.Statements).
                                WithElseBlock(Nothing).
                                WithIfStatement(ifStatement.IfStatement.ReplaceNode(fix.ContainsKeyInvocation, tryAddInvocation))
                        editor.ReplaceNode(ifStatement, newIf)
                    End If
                Else
                    editor.RemoveNode(fix.DictionaryAddInvocation.Parent, SyntaxRemoveOptions.KeepNoTrivia)
                    editor.ReplaceNode(unary, tryAddInvocation)
                End If
            ElseIf ifStatement.IfStatement.Condition.IsKind(SyntaxKind.InvocationExpression) And ifStatement.ElseBlock IsNot Nothing Then
                Dim negatedTryAddInvocation = generator.LogicalNotExpression(tryAddInvocation)
                editor.ReplaceNode(fix.ContainsKeyInvocation, negatedTryAddInvocation)
                If ifStatement.ElseBlock.Statements.Count = 1 Then
                    editor.RemoveNode(ifStatement.ElseBlock, SyntaxRemoveOptions.KeepNoTrivia)
                Else
                    editor.RemoveNode(fix.DictionaryAddInvocation.Parent, SyntaxRemoveOptions.KeepNoTrivia)
                End If
            End If
        End Sub

        Private Shared Function GetDictionaryValueType(semanticModel As SemanticModel, dictionary As SyntaxNode) As ITypeSymbol
            Dim type = DirectCast(semanticModel.GetTypeInfo(dictionary).Type, INamedTypeSymbol)
            Return type.TypeArguments(1)
        End Function

        Private NotInheritable Class TryGetValueFix
            Public Sub New(containsKeyInvocation As InvocationExpressionSyntax, containsKeyAccess As MemberAccessExpressionSyntax,
                           valueAssignmentAnchor As SyntaxNode, dictionaryAccessors As List(Of SyntaxNode),
                           addStatementNode As ExecutableStatementSyntax, changedValueNode As SyntaxNode, variableName As String,
                           localDeclarationStatement As LocalDeclarationStatementSyntax, variableDeclarator As VariableDeclaratorSyntax,
                           dictionaryValueType As ITypeSymbol)
                Me.ContainsKeyInvocation = containsKeyInvocation
                Me.ContainsKeyAccess = containsKeyAccess
                Me.ValueAssignmentAnchor = valueAssignmentAnchor
                Me.DictionaryAccessors = dictionaryAccessors
                Me.AddStatementNode = addStatementNode
                Me.ChangedValueNode = changedValueNode
                Me.VariableName = variableName
                Me.LocalDeclarationStatement = localDeclarationStatement
                Me.VariableDeclarator = variableDeclarator
                Me.DictionaryValueType = dictionaryValueType
            End Sub

            Public ReadOnly Property ContainsKeyInvocation As InvocationExpressionSyntax

            Public ReadOnly Property ContainsKeyAccess As MemberAccessExpressionSyntax

            ''' <summary>
            ''' The statement the declaration of the value local is inserted before.
            ''' </summary>
            Public ReadOnly Property ValueAssignmentAnchor As SyntaxNode

            Public ReadOnly Property DictionaryAccessors As List(Of SyntaxNode)

            Public ReadOnly Property AddStatementNode As ExecutableStatementSyntax

            Public ReadOnly Property ChangedValueNode As SyntaxNode

            ''' <summary>
            ''' The name of the local the value is already read into, or Nothing when the fix has to introduce one.
            ''' </summary>
            Public ReadOnly Property VariableName As String

            Public ReadOnly Property LocalDeclarationStatement As LocalDeclarationStatementSyntax

            Public ReadOnly Property VariableDeclarator As VariableDeclaratorSyntax

            Public ReadOnly Property DictionaryValueType As ITypeSymbol
        End Class

        Private NotInheritable Class TryAddFix
            Public Sub New(containsKeyInvocation As InvocationExpressionSyntax, containsKeyAccess As MemberAccessExpressionSyntax,
                           dictionaryAddInvocation As InvocationExpressionSyntax, ifStatement As MultiLineIfBlockSyntax)
                Me.ContainsKeyInvocation = containsKeyInvocation
                Me.ContainsKeyAccess = containsKeyAccess
                Me.DictionaryAddInvocation = dictionaryAddInvocation
                Me.IfStatement = ifStatement
            End Sub

            Public ReadOnly Property ContainsKeyInvocation As InvocationExpressionSyntax

            Public ReadOnly Property ContainsKeyAccess As MemberAccessExpressionSyntax

            Public ReadOnly Property DictionaryAddInvocation As InvocationExpressionSyntax

            Public ReadOnly Property IfStatement As MultiLineIfBlockSyntax
        End Class
    End Class
End Namespace
