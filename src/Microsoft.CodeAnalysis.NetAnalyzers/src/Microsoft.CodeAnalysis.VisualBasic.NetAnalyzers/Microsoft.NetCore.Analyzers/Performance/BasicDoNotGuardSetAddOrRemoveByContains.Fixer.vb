' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicDoNotGuardSetAddOrRemoveByContainsFixer
        Inherits DoNotGuardSetAddOrRemoveByContainsFixer

        Protected Overrides Function SyntaxSupportedByFixer(conditionalSyntax As SyntaxNode, childStatementSyntax As SyntaxNode) As Boolean
            If TypeOf childStatementSyntax IsNot ExpressionStatementSyntax Then
                Return False
            End If

            If TypeOf conditionalSyntax Is MultiLineIfBlockSyntax Then
                Dim addOrRemoveInElse = TypeOf childStatementSyntax.Parent Is ElseBlockSyntax

                If addOrRemoveInElse Then
                    Return CType(conditionalSyntax, MultiLineIfBlockSyntax).ElseBlock.Statements.Count() = 1
                Else
                    Return CType(conditionalSyntax, MultiLineIfBlockSyntax).Statements.Count() = 1
                End If
            End If

            Return TypeOf conditionalSyntax Is SingleLineIfStatementSyntax
        End Function

        Protected Overrides Function ReplaceConditionWithChild(document As Document, root As SyntaxNode, conditionalOperationNode As SyntaxNode, childOperationNode As SyntaxNode) As Document
            Dim newConditionNode As SyntaxNode = childOperationNode

            ' If there's an else block, negate the condition and replace the single true statement with it
            Dim multiLineIfBlockSyntax = TryCast(conditionalOperationNode, MultiLineIfBlockSyntax)
            If multiLineIfBlockSyntax?.ElseBlock?.ChildNodes().Any() Then
                Dim generator = SyntaxGenerator.GetGenerator(document)
                Dim negatedExpression = generator.LogicalNotExpression(CType(childOperationNode, ExpressionStatementSyntax).Expression.WithoutTrivia())
                Dim addOrRemoveInElse = TypeOf childOperationNode.Parent Is ElseBlockSyntax

                newConditionNode = multiLineIfBlockSyntax.WithIfStatement(multiLineIfBlockSyntax.IfStatement.WithCondition(CType(negatedExpression, ExpressionSyntax))) _
                    .WithStatements(If(addOrRemoveInElse, multiLineIfBlockSyntax.Statements, multiLineIfBlockSyntax.ElseBlock.Statements)) _
                    .WithElseBlock(Nothing) _
                    .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(conditionalOperationNode)
            Else
                ' if there's an else statement, negate the condition and replace the single true statement with it
                Dim singleLineIfBlockSyntax = TryCast(conditionalOperationNode, SingleLineIfStatementSyntax)
                If singleLineIfBlockSyntax?.ElseClause?.ChildNodes().Any() Then
                    Dim generator = SyntaxGenerator.GetGenerator(document)
                    Dim negatedExpression = generator.LogicalNotExpression(CType(childOperationNode, ExpressionStatementSyntax).Expression.WithoutTrivia())
                    Dim addOrRemoveInElse = TypeOf childOperationNode.Parent Is SingleLineElseClauseSyntax

                    newConditionNode = singleLineIfBlockSyntax.WithCondition(CType(negatedExpression, ExpressionSyntax)) _
                        .WithStatements(If(addOrRemoveInElse, singleLineIfBlockSyntax.Statements, singleLineIfBlockSyntax.ElseClause.Statements)) _
                        .WithElseClause(Nothing) _
                        .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(conditionalOperationNode)
                Else
                    newConditionNode = newConditionNode.WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(conditionalOperationNode)
                End If
            End If

            Dim newRoot = root.ReplaceNode(conditionalOperationNode, newConditionNode)

            Return document.WithSyntaxRoot(newRoot)
        End Function
    End Class
End Namespace
