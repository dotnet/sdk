// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                return IsInElseBranch(childStatementSyntax)
                    ? ifStatementSyntax.Else?.Statement.ChildNodes().Count() == 1
                    : ifStatementSyntax.Statement.ChildNodes().Count() == 1;
            }

            return false;
        }

        protected override bool IsInElseBranch(SyntaxNode childStatementSyntax)
            => childStatementSyntax.Parent is ElseClauseSyntax || childStatementSyntax.Parent?.Parent is ElseClauseSyntax;

        protected override SyntaxNode ReplaceConditionWithChild(SyntaxNode currentConditional, bool guardedCallInElse, SyntaxGenerator generator)
        {
            if (currentConditional is not IfStatementSyntax ifStatementSyntax ||
                GetGuardedStatement(guardedCallInElse ? ifStatementSyntax.Else?.Statement : ifStatementSyntax.Statement) is not ExpressionStatementSyntax guardedStatement)
            {
                return currentConditional;
            }

            if (ifStatementSyntax.Else is null)
            {
                return guardedStatement
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithTriviaFrom(currentConditional);
            }

            return ifStatementSyntax
                .WithCondition((ExpressionSyntax)generator.LogicalNotExpression(guardedStatement.Expression.WithoutTrivia()))
                .WithStatement(guardedCallInElse ? ifStatementSyntax.Statement : ifStatementSyntax.Else.Statement)
                .WithElse(null)
                .WithAdditionalAnnotations(Formatter.Annotation)
                .WithTriviaFrom(currentConditional);
        }

        private static ExpressionStatementSyntax? GetGuardedStatement(StatementSyntax? branch)
            => branch as ExpressionStatementSyntax ?? branch?.ChildNodes().SingleOrDefault() as ExpressionStatementSyntax;
    }
}
