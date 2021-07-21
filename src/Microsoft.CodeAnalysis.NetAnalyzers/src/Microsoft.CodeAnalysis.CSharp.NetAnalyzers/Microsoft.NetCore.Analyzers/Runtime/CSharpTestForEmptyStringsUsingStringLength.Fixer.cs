// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA1820: Test for empty strings using string length
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpTestForEmptyStringsUsingStringLengthFixer : TestForEmptyStringsUsingStringLengthFixer
    {
        protected override SyntaxNode GetExpression(SyntaxNode node)
        {
            return node is ArgumentSyntax argumentSyntax ? argumentSyntax.Expression : node;
        }
        protected override bool IsEqualsOperator(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.EqualsExpression);
        }
        protected override bool IsNotEqualsOperator(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.NotEqualsExpression);
        }

        protected override SyntaxNode GetLeftOperand(SyntaxNode binaryExpressionSyntax)
        {
            return ((BinaryExpressionSyntax)binaryExpressionSyntax).Left;
        }

        protected override SyntaxNode GetRightOperand(SyntaxNode binaryExpressionSyntax)
        {
            return ((BinaryExpressionSyntax)binaryExpressionSyntax).Right;
        }

        protected override bool IsFixableBinaryExpression(SyntaxNode node)
        {
            return (node is BinaryExpressionSyntax) && (IsEqualsOperator(node) || IsNotEqualsOperator(node));
        }

        protected override bool IsFixableInvocationExpression(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.InvocationExpression);
        }

        protected override SyntaxNode? GetInvocationTarget(SyntaxNode node)
        {
            if (node is InvocationExpressionSyntax invocationExpression && invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression)
            {
                return memberAccessExpression.Expression;
            }

            return default;
        }
    }
}