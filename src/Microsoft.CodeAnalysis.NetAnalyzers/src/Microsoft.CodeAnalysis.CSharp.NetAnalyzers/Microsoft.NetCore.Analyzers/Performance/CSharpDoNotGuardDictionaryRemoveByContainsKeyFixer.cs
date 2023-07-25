// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotGuardDictionaryRemoveByContainsKeyFixer : DoNotGuardDictionaryRemoveByContainsKeyFixer
    {
        protected override bool SyntaxSupportedByFixer(SyntaxNode conditionalSyntax)
        {
            if (conditionalSyntax is IfStatementSyntax ifStatementSyntax)
            {
                // The analyzer also reports a diagnostic when the condition is negated.
                // Applying the fix in this case would lead to wrong code.
                return ifStatementSyntax.Condition.RawKind != (int)SyntaxKind.LogicalNotExpression &&
                    ifStatementSyntax.Statement.ChildNodes().Count() == 1;
            }

            return false;
        }

        protected override Document ReplaceConditionWithChild(Document document, SyntaxNode root,
                                                              SyntaxNode conditionalOperationNode,
                                                              SyntaxNode childOperationNode)
        {
            SyntaxNode newRoot;

            // if there's a false (else) block, negate the condition and replace the single true statement with it

            if (conditionalOperationNode is ConditionalExpressionSyntax conditionalExpressionSyntax &&
                conditionalExpressionSyntax.WhenFalse.ChildNodes().Any())
            {
                var negatedExpression = GetNegatedExpression(document, childOperationNode);

                SyntaxNode newConditionalOperationNode = conditionalExpressionSyntax
                    .WithCondition((ExpressionSyntax)negatedExpression)
                    .WithWhenTrue(conditionalExpressionSyntax.WhenFalse)
                    .WithWhenFalse(null!)
                    .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(conditionalOperationNode);

                newRoot = root.ReplaceNode(conditionalOperationNode, newConditionalOperationNode);
            }
            else if (conditionalOperationNode is IfStatementSyntax ifStatementSyntax && ifStatementSyntax.Else != null)
            {
                var negatedExpression = GetNegatedExpression(document, childOperationNode);

                SyntaxNode newConditionalOperationNode = ifStatementSyntax
                    .WithCondition((ExpressionSyntax)negatedExpression)
                    .WithStatement(ifStatementSyntax.Else.Statement)
                    .WithElse(null)
                    .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(conditionalOperationNode);

                newRoot = root.ReplaceNode(conditionalOperationNode, newConditionalOperationNode);
            }
            else
            {
                // preserve formatting and trivia

                SyntaxNode newConditionNode = childOperationNode
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithTriviaFrom(conditionalOperationNode);

                newRoot = root.ReplaceNode(conditionalOperationNode, newConditionNode);
            }

            return document.WithSyntaxRoot(newRoot);
        }

        private static SyntaxNode GetNegatedExpression(Document document, SyntaxNode newConditionNode)
        {
            var generator = SyntaxGenerator.GetGenerator(document);
            return generator.LogicalNotExpression(((ExpressionStatementSyntax)newConditionNode).Expression.WithoutTrivia());
        }
    }
}
