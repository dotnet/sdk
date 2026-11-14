// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer : DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer<InvocationExpressionSyntax>
    {
        // The call is either a statement of its own or the whole expression body of a member or local function.
        protected override bool CanRewrite(SyntaxNode statement)
            => statement is ExpressionStatementSyntax or ArrowExpressionClauseSyntax;

        protected override void RemoveStatement(SyntaxNode statement, SyntaxEditor editor)
        {
            if (!TryReplaceExpressionBody(statement, Array.Empty<SyntaxNode>(), editor))
            {
                base.RemoveStatement(statement, editor);
            }
        }

        protected override void ReplaceWithNullableStructCheck(InvocationExpressionSyntax invocation, SyntaxNode statement, SyntaxEditor editor)
        {
            SyntaxGenerator generator = editor.Generator;
            var nullableStructExpression = invocation.ArgumentList.Arguments[0].Expression;
            var condition = generator.LogicalNotExpression(generator.MemberAccessExpression(nullableStructExpression, HasValue));
            var nameOfExpression = generator.NameOfExpression(nullableStructExpression);
            var argumentNullException = generator.ObjectCreationExpression(generator.IdentifierName(ArgumentNullException), nameOfExpression);
            var throwExpression = generator.ThrowStatement(argumentNullException);
            var ifStatement = generator.IfStatement(condition, new[] { throwExpression });
            if (!TryReplaceExpressionBody(statement, new[] { ifStatement }, editor))
            {
                editor.ReplaceNode(statement, ifStatement);
            }
        }

        /// <summary>
        /// When <paramref name="statement"/> is the expression body of a declaration, gives the declaration a block
        /// body holding <paramref name="statements"/> instead: rewriting the arrow clause alone would leave the
        /// declaration without a valid body.
        /// </summary>
        private static bool TryReplaceExpressionBody(SyntaxNode statement, SyntaxNode[] statements, SyntaxEditor editor)
        {
            if (statement is not ArrowExpressionClauseSyntax { Parent: SyntaxNode declaration })
            {
                return false;
            }

            editor.ReplaceNode(declaration, (currentDeclaration, generator) => generator.WithStatements(currentDeclaration, statements));
            return true;
        }
    }
}