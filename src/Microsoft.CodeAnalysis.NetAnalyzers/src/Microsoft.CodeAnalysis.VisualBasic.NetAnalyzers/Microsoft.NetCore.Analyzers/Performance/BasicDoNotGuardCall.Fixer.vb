' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.NetCore.Analyzers.Performance

Namespace Microsoft.NetCore.VisualBasic.Analyzers.Performance
    ''' <summary>
    ''' CA1853: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.DoNotGuardDictionaryRemoveByContainsKeyTitle"/>
    ''' CA1868: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.DoNotGuardSetAddOrRemoveByContainsTitle"/>
    ''' </summary>
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Public NotInheritable Class BasicDoNotGuardCallFixer
        Inherits DoNotGuardCallFixer

        Protected Overrides Function SyntaxSupportedByFixer(conditionalSyntax As SyntaxNode, childStatementSyntax As SyntaxNode) As Boolean
            If TypeOf childStatementSyntax IsNot ExpressionStatementSyntax Then
                Return False
            End If

            If TypeOf conditionalSyntax Is MultiLineIfBlockSyntax Then
                If IsInElseBranch(childStatementSyntax) Then
                    Return CType(conditionalSyntax, MultiLineIfBlockSyntax).ElseBlock.Statements.Count() = 1
                Else
                    Return CType(conditionalSyntax, MultiLineIfBlockSyntax).Statements.Count() = 1
                End If
            End If

            Return TypeOf conditionalSyntax Is SingleLineIfStatementSyntax
        End Function

        Protected Overrides Function IsInElseBranch(childStatementSyntax As SyntaxNode) As Boolean
            Return TypeOf childStatementSyntax.Parent Is ElseBlockSyntax OrElse TypeOf childStatementSyntax.Parent Is SingleLineElseClauseSyntax
        End Function

        Protected Overrides Function ReplaceConditionWithChild(currentConditional As SyntaxNode, guardedCallInElse As Boolean, generator As SyntaxGenerator) As SyntaxNode
            Dim multiLineIfBlockSyntax = TryCast(currentConditional, MultiLineIfBlockSyntax)
            If multiLineIfBlockSyntax IsNot Nothing Then
                Dim hasElse = multiLineIfBlockSyntax.ElseBlock IsNot Nothing AndAlso multiLineIfBlockSyntax.ElseBlock.ChildNodes().Any()
                Dim guardedStatement = GetGuardedStatement(If(guardedCallInElse, multiLineIfBlockSyntax.ElseBlock?.Statements, multiLineIfBlockSyntax.Statements))

                If guardedStatement Is Nothing Then
                    Return currentConditional
                End If

                If Not hasElse Then
                    Return guardedStatement.WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(currentConditional)
                End If

                ' Negate the condition and keep the branch the guarded call is not in.
                Dim negatedExpression = generator.LogicalNotExpression(guardedStatement.Expression.WithoutTrivia())

                Return multiLineIfBlockSyntax.WithIfStatement(multiLineIfBlockSyntax.IfStatement.WithCondition(CType(negatedExpression, ExpressionSyntax))) _
                    .WithStatements(If(guardedCallInElse, multiLineIfBlockSyntax.Statements, multiLineIfBlockSyntax.ElseBlock.Statements)) _
                    .WithElseBlock(Nothing) _
                    .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(currentConditional)
            End If

            Dim singleLineIfStatementSyntax = TryCast(currentConditional, SingleLineIfStatementSyntax)
            If singleLineIfStatementSyntax Is Nothing Then
                Return currentConditional
            End If

            Dim singleLineHasElse = singleLineIfStatementSyntax.ElseClause IsNot Nothing AndAlso singleLineIfStatementSyntax.ElseClause.ChildNodes().Any()
            Dim singleLineGuardedStatement = GetGuardedStatement(If(guardedCallInElse, singleLineIfStatementSyntax.ElseClause?.Statements, singleLineIfStatementSyntax.Statements))

            If singleLineGuardedStatement Is Nothing Then
                Return currentConditional
            End If

            If Not singleLineHasElse Then
                Return singleLineGuardedStatement.WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(currentConditional)
            End If

            Dim singleLineNegatedExpression = generator.LogicalNotExpression(singleLineGuardedStatement.Expression.WithoutTrivia())

            Return singleLineIfStatementSyntax.WithCondition(CType(singleLineNegatedExpression, ExpressionSyntax)) _
                .WithStatements(If(guardedCallInElse, singleLineIfStatementSyntax.Statements, singleLineIfStatementSyntax.ElseClause.Statements)) _
                .WithElseClause(Nothing) _
                .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(currentConditional)
        End Function

        Private Shared Function GetGuardedStatement(statements As SyntaxList(Of StatementSyntax)?) As ExpressionStatementSyntax
            If Not statements.HasValue Then
                Return Nothing
            End If

            Return TryCast(statements.Value.FirstOrDefault(), ExpressionStatementSyntax)
        End Function
    End Class
End Namespace
