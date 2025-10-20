﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1853: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.DoNotGuardDictionaryRemoveByContainsKeyTitle"/>
    /// CA1868: <inheritdoc cref="NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources.DoNotGuardSetAddOrRemoveByContainsTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotGuardCallFixer : DoNotGuardCallFixer
    {
        protected override bool SyntaxSupportedByFixer(SyntaxNode conditionalSyntax, SyntaxNode childStatementSyntax)
        {
            if (childStatementSyntax is not ExpressionStatementSyntax)
            {
                return false;
            }

            if (conditionalSyntax is IfStatementSyntax ifStatementSyntax)
            {
                var guardedCallInElse = childStatementSyntax.Parent is ElseClauseSyntax || childStatementSyntax.Parent?.Parent is ElseClauseSyntax;

                return guardedCallInElse
                    ? ifStatementSyntax.Else?.Statement.ChildNodes().Count() == 1
                    : ifStatementSyntax.Statement.ChildNodes().Count() == 1;
            }

            return false;
        }

        protected override Document ReplaceConditionWithChild(Document document, SyntaxNode root, SyntaxNode conditionalOperationNode, SyntaxNode childOperationNode)
        {
            SyntaxNode newRoot;

            if (conditionalOperationNode is IfStatementSyntax { Else: not null } ifStatementSyntax)
            {
                var expression = GetNegatedExpression(document, childOperationNode);
                var guardedCallInElse = childOperationNode.Parent is ElseClauseSyntax || childOperationNode.Parent?.Parent is ElseClauseSyntax;

                SyntaxNode newConditionalOperationNode = ifStatementSyntax
                    .WithCondition((ExpressionSyntax)expression)
                    .WithStatement(guardedCallInElse ? ifStatementSyntax.Statement : ifStatementSyntax.Else.Statement)
                    .WithElse(null)
                    .WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(conditionalOperationNode);

                newRoot = root.ReplaceNode(conditionalOperationNode, newConditionalOperationNode);
            }
            else
            {
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
